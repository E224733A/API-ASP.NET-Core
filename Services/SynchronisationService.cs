using API_ASP.NET_Core.Models;
using API_ASP.NET_Core.Repositories;
using API_ASP.NET_Core.Validators;
using Microsoft.Data.SqlClient;

namespace API_ASP.NET_Core.Services;

/// <summary>
/// Service applicatif responsable du traitement métier du POST final mobile.
/// </summary>
/// <remarks>
/// Ce service applique les règles avant l'écriture SQL : validation du contrat JSON,
/// contrôle de la date métier, idempotence technique, doublon métier et conversion
/// des erreurs SQL de doublon en réponses API stables pour le mobile.
/// </remarks>
public sealed class SynchronisationService
{
    private readonly SynchronisationTourneeValidator _validator;
    private readonly SynchronisationsRepository _repository;
    private readonly DateMetierService _dateMetierService;
    private readonly ILogger<SynchronisationService> _logger;

    public SynchronisationService(
        SynchronisationTourneeValidator validator,
        SynchronisationsRepository repository,
        DateMetierService dateMetierService,
        ILogger<SynchronisationService> logger)
    {
        _validator = validator;
        _repository = repository;
        _dateMetierService = dateMetierService;
        _logger = logger;
    }

    /// <summary>
    /// Valide et enregistre la synchronisation finale envoyée par le mobile.
    /// </summary>
    /// <remarks>
    /// La date de tournée est imposée par le serveur et les doublons sont bloqués avant
    /// l'insertion. Cette méthode retourne directement le statut HTTP et le corps JSON
    /// attendus par le contrôleur.
    /// </remarks>
    public async Task<SynchronisationServiceResult> EnregistrerSynchronisationAsync(
        SynchronisationTourneeRequest request,
        string? adresseIp,
        CancellationToken cancellationToken = default)
    {
        var validationResult = _validator.Validate(request);

        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors
                .Select(error => error.Message)
                .ToList();

            var details = validationResult.Errors
                .Select(error => new
                {
                    champ = error.Field,
                    message = error.Message
                })
                .ToList();

            return SynchronisationServiceResult.BadRequest(new
            {
                statut = "VALIDATION_ERROR",
                message = "La synchronisation contient des données invalides.",
                errors,
                details
            });
        }

        var dateTournee = SynchronisationTourneeValidator.ParseDateTournee(request.DateTournee);
        var dateTourneePayload = DateOnly.FromDateTime(dateTournee);
        var dateTourneeAutorisee = _dateMetierService.GetDateTourneeAutorisee();

        // Règle métier : le mobile ne peut synchroniser que la tournée du jour calculée par l'API.
        if (dateTourneePayload != dateTourneeAutorisee)
        {
            return SynchronisationServiceResult.Conflict(
                BuildDateTourneeNonAutoriseeResponse(
                    dateTourneePayload,
                    dateTourneeAutorisee,
                    "synchronisée"));
        }

        var codeTournee = request.CodeTournee.Trim();
        var idSynchronisation = ParseIdSynchronisation(request.IdSynchronisation);

        var livreur = request.Livreur
            ?? throw new InvalidOperationException("Le livreur a été validé mais reste null.");

        var codeLivreur = livreur.CodeLivreur.Trim();

        /*
         * Ordre volontaire des contrôles :
         *
         * 1. Blocage de la date métier : une date ancienne ou future ne doit pas être traitée.
         * 2. Doublon technique par IdSynchronisation.
         * 3. Doublon métier par DateTournee + CodeTournee.
         * 4. Insertion en base.
         *
         * Si le mobile renvoie une tournée d'hier, l'API refuse avant tout contrôle
         * d'idempotence pour éviter de considérer un vieux payload comme encore rejouable.
         */
        var synchronisationDejaRecue = await _repository.GetSynchronisationDejaRecueAsync(
            idSynchronisation,
            cancellationToken);

        if (synchronisationDejaRecue is not null)
        {
            return SynchronisationServiceResult.Conflict(new
            {
                statut = "CONFLICT",
                code = "SYNCHRONISATION_ALREADY_EXISTS",
                message = "Cette synchronisation a déjà été reçue.",
                idSynchronisation = idSynchronisation.ToString(),
                idTourneeMobileExistante = synchronisationDejaRecue.IdTourneeMobile,
                dateTournee = synchronisationDejaRecue.DateTournee.ToString("yyyy-MM-dd"),
                codeTournee = synchronisationDejaRecue.CodeTournee,
                codeLivreurDejaEnvoye = synchronisationDejaRecue.CodeLivreur,
                nomLivreurDejaEnvoye = synchronisationDejaRecue.NomLivreur,
                dateEnvoiExistante = synchronisationDejaRecue.DateEnvoi,
                dateReceptionApiExistante = synchronisationDejaRecue.DateReceptionApi
            });
        }

        /*
         * Règle métier finale :
         *
         * Une seule tournée ENVOYEE est autorisée par DateTournee + CodeTournee.
         * Le CodeLivreur sert uniquement à tracer qui a envoyé, mais il ne permet pas
         * d'envoyer une deuxième fois la même tournée le même jour.
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
                statut = "CONFLICT",
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
                statut = "SUCCESS",
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
             * Sécurité finale SQL.
             *
             * Même si l'API vérifie avant insertion, deux requêtes peuvent arriver
             * presque en même temps. Les contraintes SQL restent donc la vraie sécurité.
             */
            _logger.LogWarning(
                exception,
                "Doublon bloqué par SQL pour IdSynchronisation={IdSynchronisation}, CodeTournee={CodeTournee}, DateTournee={DateTournee}.",
                idSynchronisation,
                codeTournee,
                dateTournee);

            if (IsSqlDuplicateIdSynchronisation(exception))
            {
                return SynchronisationServiceResult.Conflict(new
                {
                    statut = "CONFLICT",
                    code = "SYNCHRONISATION_ALREADY_EXISTS",
                    message = "Cette synchronisation a déjà été reçue.",
                    idSynchronisation = idSynchronisation.ToString(),
                    dateTournee = dateTournee.ToString("yyyy-MM-dd"),
                    codeTournee
                });
            }

            return SynchronisationServiceResult.Conflict(new
            {
                statut = "CONFLICT",
                code = "TOURNEE_ALREADY_SENT",
                message = "Cette tournée a déjà été envoyée pour cette date.",
                dateTournee = dateTournee.ToString("yyyy-MM-dd"),
                codeTournee
            });
        }
    }

    /// <summary>
    /// Convertit l'identifiant de synchronisation déjà validé en Guid exploitable par le service.
    /// </summary>
    private static Guid ParseIdSynchronisation(object? value)
    {
        if (SynchronisationTourneeValidator.TryParseGuid(value, out var idSynchronisation))
        {
            return idSynchronisation;
        }

        throw new InvalidOperationException("L'identifiant de synchronisation a été validé mais reste invalide.");
    }

    /// <summary>
    /// Construit la réponse de conflit lorsque la date envoyée ne correspond pas à la date métier autorisée.
    /// </summary>
    /// <remarks>
    /// Les codes DATE_TOURNEE_EXPIREE et DATE_TOURNEE_NON_AUTORISEE permettent au client
    /// de distinguer un ancien payload d'une date future ou inattendue.
    /// </remarks>
    private static object BuildDateTourneeNonAutoriseeResponse(
        DateOnly dateTourneePayload,
        DateOnly dateTourneeAutorisee,
        string actionMetier)
    {
        var datePayload = dateTourneePayload.ToString("yyyy-MM-dd");
        var dateAutorisee = dateTourneeAutorisee.ToString("yyyy-MM-dd");

        if (dateTourneePayload < dateTourneeAutorisee)
        {
            return new
            {
                success = false,
                statut = "CONFLICT",
                code = "DATE_TOURNEE_EXPIREE",
                message = $"La tournée envoyée date du {datePayload}. Elle ne peut plus être {actionMetier} le {dateAutorisee}.",
                dateTourneePayload = datePayload,
                dateTourneeAutorisee = dateAutorisee
            };
        }

        return new
        {
            success = false,
            statut = "CONFLICT",
            code = "DATE_TOURNEE_NON_AUTORISEE",
            message = $"La tournée envoyée date du {datePayload}. Elle ne peut pas être {actionMetier} le {dateAutorisee}.",
            dateTourneePayload = datePayload,
            dateTourneeAutorisee = dateAutorisee
        };
    }

    /// <summary>
    /// Détermine si l'erreur SQL correspond à une contrainte d'unicité sur IdSynchronisation.
    /// </summary>
    private static bool IsSqlDuplicateIdSynchronisation(SqlException exception)
    {
        return exception.Message.Contains("IdSynchronisation", StringComparison.OrdinalIgnoreCase)
               || exception.Message.Contains("UQ_Mobile_Tournee_IdSynchronisation", StringComparison.OrdinalIgnoreCase)
               || exception.Message.Contains("UX_Mobile_Tournee_IdSynchronisation", StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>
/// Résultat applicatif retourné par le service de synchronisation au contrôleur HTTP.
/// </summary>
/// <remarks>
/// Cette enveloppe évite de mélanger la logique métier du service avec les types IActionResult
/// du contrôleur, tout en conservant le statut HTTP à retourner.
/// </remarks>
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
