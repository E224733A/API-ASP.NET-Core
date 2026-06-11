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
/// </summary>
/// <remarks>
/// Ce service valide le payload ServeWeb, contrôle la date métier Expédition, vérifie
/// l'idempotence du lot et délègue l'écriture transactionnelle au repository SQL.
/// Les validations de structure restent centralisées dans <see cref="ExpeditionVerrouillageValidator"/>.
/// </remarks>
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
    /// <remarks>
    /// Règle métier : ServeWeb envoie une préparation globale pour la date préparable calculée
    /// par l'API. Un même lot avec le même contenu est considéré comme déjà traité, tandis
    /// qu'un même identifiant de lot avec un contenu différent est refusé.
    /// </remarks>
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

        // Règle métier : l'Expédition verrouille uniquement la prochaine date ouvrée préparable par l'API.
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
            // Idempotence : une relance identique renvoie Statut=SUCCESS et Code=ALREADY_PROCESSED.
            // Le même identifiant de lot avec un contenu différent reste refusé en 409.
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

    /// <summary>
    /// Construit la réponse de conflit lorsque ServeWeb envoie une date Expédition non autorisée.
    /// </summary>
    /// <remarks>
    /// Le code DATE_TOURNEE_EXPIREE signale une préparation trop ancienne. Le code
    /// DATE_TOURNEE_NON_AUTORISEE couvre les autres écarts avec la date préparable calculée.
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
    /// Calcule l'empreinte stable du contenu métier d'un lot Expédition.
    /// </summary>
    /// <remarks>
    /// L'empreinte permet de distinguer un retry identique d'un réemploi dangereux du même
    /// identifiant de lot avec un contenu différent. Les collections sont triées pour éviter
    /// qu'un simple changement d'ordre dans le JSON modifie artificiellement l'empreinte.
    /// </remarks>
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

    /// <summary>
    /// Génère un identifiant technique stable à partir de l'identifiant métier du lot ServeWeb.
    /// </summary>
    /// <remarks>
    /// Cette stratégie permet de reconnaître le même lot lors d'un retry sans dépendre
    /// d'un GUID généré aléatoirement à chaque appel.
    /// </remarks>
    private static Guid BuildDeterministicGuid(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value.Trim().ToUpperInvariant()));
        var guidBytes = bytes.Take(16).ToArray();
        return new Guid(guidBytes);
    }

    /// <summary>
    /// Normalise un code article avant calcul d'empreinte.
    /// </summary>
    private static string NormalizeArticleCode(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToUpperInvariant();
    }

    /// <summary>
    /// Normalise une chaîne optionnelle avant calcul d'empreinte.
    /// </summary>
    private static string? NormalizeNullable(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    /// <summary>
    /// Normalise une date optionnelle avant calcul d'empreinte du lot.
    /// </summary>
    /// <remarks>
    /// Les dates valides sont converties en UTC pour éviter qu'une différence de représentation
    /// horaire produise une empreinte différente pour le même instant métier.
    /// </remarks>
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