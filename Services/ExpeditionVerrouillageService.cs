using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using API_ASP.NET_Core.Mappers;
using API_ASP.NET_Core.Models;
using API_ASP.NET_Core.Repositories;
using API_ASP.NET_Core.Validators;
using Microsoft.AspNetCore.Http;

namespace API_ASP.NET_Core.Services;

/// <summary>
/// Service d'orchestration métier pour le verrouillage des lots Expédition.
/// Les validations de payload sont centralisées dans <see cref="ExpeditionVerrouillageValidator"/>.
/// </summary>
public sealed class ExpeditionVerrouillageService
{
    private const string SchemaVersionExpedition = "1.2";

    private readonly ExpeditionVerrouillageValidator _validator;
    private readonly ExpeditionMapper _mapper;
    private readonly ExpeditionRepository _expeditionRepository;
    private readonly DateMetierService _dateMetierService;
    private readonly ILogger<ExpeditionVerrouillageService> _logger;

    public ExpeditionVerrouillageService(
        ExpeditionVerrouillageValidator validator,
        ExpeditionMapper mapper,
        ExpeditionRepository expeditionRepository,
        DateMetierService dateMetierService,
        ILogger<ExpeditionVerrouillageService> logger)
    {
        _validator = validator;
        _mapper = mapper;
        _expeditionRepository = expeditionRepository;
        _dateMetierService = dateMetierService;
        _logger = logger;
    }

    /// <summary>
    /// Verrouille un lot global de préparations Expédition.
    /// </summary>
    public async Task<(int StatusCode, object Body)> VerrouillerPreparationLotAsync(
        ExpeditionVerrouillageLotRequest? request,
        string? adresseIp,
        CancellationToken cancellationToken = default)
    {
        var validationErrors = await _validator.ValidateAsync(request, cancellationToken);
        if (validationErrors.Count > 0 || request is null)
        {
            return (
                StatusCodes.Status400BadRequest,
                new ExpeditionApiResult
                {
                    Statut = "VALIDATION_ERROR",
                    Code = "EXPEDITION_VALIDATION_ERROR",
                    Message = "Le lot de préparation Expédition contient des données invalides.",
                    Errors = validationErrors.ToList(),
                    IdLotVerrouillage = request?.IdLotVerrouillage,
                    DateTournee = request?.DateTournee
                });
        }

        var command = _mapper.ToVerrouillageCommand(request);
        var dateTourneeAutorisee = _dateMetierService.GetDateTourneeExpeditionPreparable();

        if (command.DateTournee != dateTourneeAutorisee)
        {
            return (
                StatusCodes.Status409Conflict,
                BuildDateTourneeNonAutoriseeResponse(
                    command.DateTournee,
                    dateTourneeAutorisee,
                    "verrouillée"));
        }

        var idLotVerrouillageTechnique = BuildDeterministicGuid(command.IdLotVerrouillage);
        var empreintePayload = ComputePayloadFingerprint(command);

        var existingLot = await _expeditionRepository.GetLotVerrouillageAsync(
            idLotVerrouillageTechnique,
            cancellationToken);

        if (existingLot is not null)
        {
            if (string.Equals(existingLot.EmpreintePayload, empreintePayload, StringComparison.OrdinalIgnoreCase))
            {
                return (
                    StatusCodes.Status200OK,
                    new ExpeditionApiResult
                    {
                        Statut = "SUCCESS",
                        Code = "ALREADY_PROCESSED",
                        Message = "Lot Expédition déjà traité avec le même contenu.",
                        IdLotVerrouillage = command.IdLotVerrouillage,
                        DateTournee = command.DateTourneeTexte,
                        StatutVerrouillage = "DEJA_TRAITE"
                    });
            }

            return (
                StatusCodes.Status409Conflict,
                new ExpeditionApiResult
                {
                    Statut = "CONFLICT",
                    Code = "EXPEDITION_LOT_PAYLOAD_MISMATCH",
                    Message = "Ce lot de verrouillage a déjà été reçu avec un contenu différent.",
                    IdLotVerrouillage = command.IdLotVerrouillage,
                    DateTournee = command.DateTourneeTexte
                });
        }

        try
        {
            var result = await _expeditionRepository.EnregistrerVerrouillageLotAsync(
                command,
                command.DateTournee.ToDateTime(TimeOnly.MinValue),
                idLotVerrouillageTechnique,
                empreintePayload,
                adresseIp,
                cancellationToken);

            return (
                StatusCodes.Status200OK,
                new ExpeditionApiResult
                {
                    Statut = "SUCCESS",
                    Code = "EXPEDITION_LOT_LOCKED",
                    Message = "Préparations Expédition sauvegardées et verrouillées avec succès.",
                    IdLotVerrouillage = command.IdLotVerrouillage,
                    DateTournee = command.DateTourneeTexte,
                    StatutVerrouillage = "VERROUILLEE_BD",
                    DateReceptionApi = result.DateReceptionApi.ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture),
                    DateSauvegardeSql = result.DateSauvegardeSql.ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture),
                    NombreTourneesVerrouillees = result.NombreTourneesVerrouillees,
                    NombreLignesVerrouillees = result.NombreLignesVerrouillees
                });
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Erreur lors du verrouillage du lot Expédition {IdLotVerrouillage}.",
                command.IdLotVerrouillage);

            return (
                StatusCodes.Status500InternalServerError,
                new ExpeditionApiResult
                {
                    Statut = "ERROR",
                    Code = "SERVER_ERROR",
                    Message = "Une erreur technique est survenue pendant le verrouillage Expédition.",
                    IdLotVerrouillage = command.IdLotVerrouillage,
                    DateTournee = command.DateTourneeTexte
                });
        }
    }

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

    private static string ComputePayloadFingerprint(ExpeditionVerrouillageLotCommand command)
    {
        var normalizedPayload = new
        {
            schemaVersion = SchemaVersionExpedition,
            idLotVerrouillage = NormalizeNullable(command.IdLotVerrouillage),
            source = NormalizeNullable(command.Source),
            dateTournee = NormalizeNullable(command.DateTourneeTexte),
            dateVerrouillageDemandee = NormalizeDateTimeOffsetForFingerprint(command.DateVerrouillageDemandee),
            fuseauHoraireMetier = NormalizeNullable(command.FuseauHoraireMetier),
            tournees = command.Tournees
                .OrderBy(tournee => tournee.CodeTournee, StringComparer.OrdinalIgnoreCase)
                .Select(tournee => new
                {
                    codeTournee = NormalizeNullable(tournee.CodeTournee),
                    libelleTournee = NormalizeNullable(tournee.LibelleTournee),
                    statutPreparationWeb = NormalizeNullable(tournee.StatutPreparationWeb),
                    dateModification = NormalizeDateTimeOffsetForFingerprint(tournee.DateModification),
                    lignes = tournee.Lignes
                        .OrderBy(ligne => ligne.IdLigneSource, StringComparer.OrdinalIgnoreCase)
                        .Select(ligne => new
                        {
                            idLigneSource = NormalizeNullable(ligne.IdLigneSource),
                            ordreArret = ligne.OrdreArret,
                            numClient = NormalizeNullable(ligne.Client.NumClient),
                            codePDL = NormalizeNullable(ligne.PointLivraison.CodePDL),
                            commentaireExceptionnel = NormalizeNullable(ligne.CommentaireExceptionnel),
                            quantitesPrevues = ligne.QuantitesPrevues
                                .OrderBy(quantite => quantite.CodeArticle, StringComparer.OrdinalIgnoreCase)
                                .Select(quantite => new
                                {
                                    codeArticle = NormalizeArticleCode(quantite.CodeArticle),
                                    quantiteLivreePrevue = quantite.QuantiteLivreePrevue
                                })
                                .ToList()
                        })
                        .ToList()
                })
                .ToList()
        };

        var json = JsonSerializer.Serialize(
            normalizedPayload,
            new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.Never,
                WriteIndented = false
            });

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(bytes);
    }

    private static Guid BuildDeterministicGuid(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value.Trim().ToUpperInvariant()));
        var guidBytes = bytes.Take(16).ToArray();
        return new Guid(guidBytes);
    }

    private static string NormalizeArticleCode(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToUpperInvariant();
    }

    private static string? NormalizeNullable(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    private static string? NormalizeDateTimeOffsetForFingerprint(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var parsed)
            ? parsed.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)
            : NormalizeNullable(value);
    }
}
