using API_ASP.NET_Core.Models;
using API_ASP.NET_Core.Repositories;
using API_ASP.NET_Core.Validators;
using Microsoft.Data.SqlClient;

namespace API_ASP.NET_Core.Services;

public sealed class SynchronisationService
{
    private readonly SynchronisationTourneeValidator _validator;
    private readonly SynchronisationsRepository _repository;
    private readonly ILogger<SynchronisationService> _logger;

    public SynchronisationService(
        SynchronisationTourneeValidator validator,
        SynchronisationsRepository repository,
        ILogger<SynchronisationService> logger)
    {
        _validator = validator;
        _repository = repository;
        _logger = logger;
    }

    public async Task<SynchronisationServiceResult> EnregistrerSynchronisationAsync(
        SynchronisationTourneeRequest request,
        string? adresseIp,
        CancellationToken cancellationToken = default)
    {
        var validationResult = _validator.Validate(request);

        if (!validationResult.IsValid)
        {
            return SynchronisationServiceResult.BadRequest(new
            {
                code = "VALIDATION_ERROR",
                message = "La synchronisation contient des données invalides.",
                erreurs = validationResult.Errors.Select(error => new
                {
                    champ = error.Field,
                    message = error.Message
                }).ToList()
            });
        }

        var dateTournee = SynchronisationTourneeValidator.ParseDateTournee(request.DateTournee);
        var codeTournee = request.CodeTournee.Trim();
        var livreur = request.Livreur
            ?? throw new InvalidOperationException("Le livreur a été validé mais reste null.");
        var codeLivreur = livreur.CodeLivreur.Trim();

        /*
         * Correction règle métier stricte :
         *
         * Avant insertion, on vérifie si une tournée ENVOYEE existe déjà
         * pour DateTournee + CodeTournee.
         *
         * Important :
         * - le code livreur ne fait plus partie de la clé métier ;
         * - il sert seulement à tracer qui a déjà envoyé.
         */
        var tourneeDejaEnvoyee = await _repository.GetTourneeDejaEnvoyeeAsync(
            dateTournee,
            codeTournee,
            cancellationToken);

        if (tourneeDejaEnvoyee is not null)
        {
            await _repository.EcrireLogDoubleEnvoiAsync(
                request,
                tourneeDejaEnvoyee,
                adresseIp,
                cancellationToken);

            var message = string.Equals(
                    tourneeDejaEnvoyee.CodeLivreur,
                    codeLivreur,
                    StringComparison.OrdinalIgnoreCase)
                ? "Cette tournée a déjà été envoyée pour cette date."
                : "Cette tournée a déjà été envoyée pour cette date par un autre livreur.";

            return SynchronisationServiceResult.Conflict(new
            {
                code = "TOURNEE_ALREADY_SENT",
                message,
                dateTournee = dateTournee.ToString("yyyy-MM-dd"),
                codeTournee,
                codeLivreurDejaEnvoye = tourneeDejaEnvoyee.CodeLivreur,
                nomLivreurDejaEnvoye = tourneeDejaEnvoyee.NomLivreur,
                idTourneeMobileExistante = tourneeDejaEnvoyee.IdTourneeMobile,
                dateEnvoiExistante = tourneeDejaEnvoyee.DateEnvoi,
                dateReceptionApiExistante = tourneeDejaEnvoyee.DateReceptionApi
            });
        }

        try
        {
            var resultat = await _repository.EnregistrerSynchronisationAsync(
                request,
                adresseIp,
                cancellationToken);

            return SynchronisationServiceResult.Ok(new
            {
                code = "SUCCESS",
                message = "Synchronisation enregistrée avec succès.",
                idTourneeMobile = resultat.IdTourneeMobile,
                idSynchronisation = resultat.IdSynchronisation,
                dateReceptionApi = resultat.DateReceptionApi,
                dateTournee = dateTournee.ToString("yyyy-MM-dd"),
                codeTournee,
                codeLivreur,
                nombreLignesRecues = resultat.NombreLignesRecues,
                nombreQuantitesRecues = resultat.NombreQuantitesRecues
            });
        }
        catch (SqlException exception) when (exception.Number is 2601 or 2627)
        {
            /*
             * Deuxième niveau de sécurité.
             *
             * Si deux envois passent le contrôle applicatif au même moment,
             * la contrainte SQL unique DateTournee + CodeTournee bloque
             * l'insertion concurrente.
             */
            _logger.LogWarning(
                exception,
                "Double envoi bloqué par SQL pour la tournée {CodeTournee} du {DateTournee}.",
                codeTournee,
                dateTournee);

            return SynchronisationServiceResult.Conflict(new
            {
                code = "TOURNEE_ALREADY_SENT",
                message = "Cette tournée a déjà été envoyée pour cette date.",
                dateTournee = dateTournee.ToString("yyyy-MM-dd"),
                codeTournee
            });
        }
    }
}

public sealed class SynchronisationServiceResult
{
    private SynchronisationServiceResult(int statusCode, object body)
    {
        StatusCode = statusCode;
        Body = body;
    }

    public int StatusCode { get; }

    public object Body { get; }

    public static SynchronisationServiceResult Ok(object body)
    {
        return new SynchronisationServiceResult(StatusCodes.Status200OK, body);
    }

    public static SynchronisationServiceResult BadRequest(object body)
    {
        return new SynchronisationServiceResult(StatusCodes.Status400BadRequest, body);
    }

    public static SynchronisationServiceResult Conflict(object body)
    {
        return new SynchronisationServiceResult(StatusCodes.Status409Conflict, body);
    }
}
