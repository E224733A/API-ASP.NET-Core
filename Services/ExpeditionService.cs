using API_ASP.NET_Core.Models;
using API_ASP.NET_Core.Repositories;

namespace API_ASP.NET_Core.Services;

public sealed class ExpeditionService
{
    private const string SchemaVersionSupportee = "1.0";
    private const string CodeArticleRollsVides = "ROLLS_VIDES";

    private readonly TourneesRepository _tourneesRepository;
    private readonly ExpeditionRepository _expeditionRepository;
    private readonly ILogger<ExpeditionService> _logger;

    public ExpeditionService(
        TourneesRepository tourneesRepository,
        ExpeditionRepository expeditionRepository,
        ILogger<ExpeditionService> logger)
    {
        _tourneesRepository = tourneesRepository;
        _expeditionRepository = expeditionRepository;
        _logger = logger;
    }

    public async Task<ExpeditionPreparationsApreparerResponse> GetPreparationsApreparerAsync(
        CancellationToken cancellationToken)
    {
        var nowParis = GetParisNow();
        var datePreparable = DateOnly.FromDateTime(nowParis.DateTime.Date).AddDays(1);

        var lignes = (await _tourneesRepository.GetTourneeLinesAsync(
            datePreparable,
            codeLivreur: "EXPEDITION",
            codeTournee: null)).ToList();

        var articles = (await _tourneesRepository.GetArticlesSaisissablesAsync())
            .Where(article => !IsArticleInterditExpedition(article.CodeArticle))
            .OrderBy(article => article.OrdreAffichage)
            .ThenBy(article => article.CodeArticle)
            .ToList();

        var verrouillages = await _expeditionRepository.GetTourneesVerrouilleesAsync(
            datePreparable,
            cancellationToken);

        var tournees = lignes
            .GroupBy(ligne => NormalizeIdPart(ligne.CodeTournee), StringComparer.OrdinalIgnoreCase)
            .Where(group => !string.IsNullOrWhiteSpace(group.Key))
            .OrderBy(group => TryParseInt(group.Key))
            .ThenBy(group => group.Key)
            .Select(group =>
            {
                var premiereLigne = group.First();
                var estVerrouillee = verrouillages.Contains(group.Key);

                return new ExpeditionTourneeApreparerDto
                {
                    CodeTournee = group.Key,
                    LibelleTournee = premiereLigne.LibelleTournee,
                    EtatPreparation = estVerrouillee ? "VERROUILLEE_BD" : "NON_PREPAREE",
                    EstVerrouilleeBd = estVerrouillee,
                    NombreLignes = group.Count(),
                    Lignes = group
                        .OrderBy(ligne => ligne.OrdreArret ?? int.MaxValue)
                        .ThenBy(ligne => ligne.NumClient)
                        .ThenBy(ligne => ligne.CodePDL)
                        .Select(ligne => MapLigneApreparer(datePreparable, ligne, articles))
                        .ToList()
                };
            })
            .ToList();

        return new ExpeditionPreparationsApreparerResponse
        {
            Statut = "SUCCESS",
            SchemaVersion = SchemaVersionSupportee,
            DateTournee = datePreparable.ToString("yyyy-MM-dd"),
            DateModifiable = false,
            DateGenerationApi = nowParis,
            FuseauHoraireMetier = "Europe/Paris",
            Tournees = tournees
        };
    }

    public async Task<ExpeditionOperationResult> VerrouillerPreparationsAsync(
        ExpeditionVerrouillageRequest? request,
        string? adresseIp,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return ExpeditionOperationResult.BadRequest("VALIDATION_ERROR", "BODY_REQUIRED",
                "Le corps JSON du verrouillage Expédition est obligatoire.",
                new[] { "Le corps de la requête est vide ou invalide." });
        }

        var nowParis = GetParisNow();
        var dateMetierApi = DateOnly.FromDateTime(nowParis.DateTime.Date);
        var errors = ValidateRequestShape(request, dateMetierApi).ToList();

        if (errors.Count > 0)
        {
            return ExpeditionOperationResult.BadRequest("VALIDATION_ERROR", "REQUEST_INVALID",
                "Le lot de verrouillage Expédition est invalide.", errors);
        }

        var lotExistant = await _expeditionRepository.GetLotVerrouillageAsync(
            request.IdLotVerrouillage,
            cancellationToken);

        if (lotExistant is not null && lotExistant.Statut == "SUCCESS")
        {
            return ExpeditionOperationResult.Ok(new ExpeditionVerrouillageSuccessResponse
            {
                Statut = "SUCCESS",
                Message = "Lot de verrouillage déjà traité. Aucun doublon n'a été créé.",
                IdLotVerrouillage = request.IdLotVerrouillage,
                DateTournee = request.DateTournee,
                NombreTourneesVerrouillees = lotExistant.NombreTournees,
                NombreLignesRecues = lotExistant.NombreLignes,
                NombreQuantitesRecues = lotExistant.NombreQuantites,
                DateReceptionApi = lotExistant.DateReceptionApi,
                DateSauvegardeSql = lotExistant.DateTraitement ?? lotExistant.DateReceptionApi
            });
        }

        var dateTournee = DateOnly.Parse(request.DateTournee);
        var lockedTournees = await _expeditionRepository.GetTourneesVerrouilleesAsync(dateTournee, cancellationToken);
        var requestedCodes = request.Tournees
            .Select(tournee => NormalizeIdPart(tournee.CodeTournee))
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var conflicts = requestedCodes
            .Where(code => lockedTournees.Contains(code))
            .ToList();

        if (conflicts.Count > 0)
        {
            return ExpeditionOperationResult.Conflict("CONFLICT", "ALREADY_LOCKED",
                "Une ou plusieurs tournées sont déjà verrouillées en base.",
                conflicts.Select(code => $"Tournée déjà verrouillée : {code}"));
        }

        var articlesPreparable = (await _tourneesRepository.GetArticlesSaisissablesAsync())
            .Where(article => !IsArticleInterditExpedition(article.CodeArticle))
            .Select(article => article.CodeArticle.Trim().ToUpperInvariant())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var coherenceErrors = await ValidateBusinessCoherenceAsync(
            request,
            dateTournee,
            articlesPreparable,
            cancellationToken);

        if (coherenceErrors.Count > 0)
        {
            return ExpeditionOperationResult.BadRequest("VALIDATION_ERROR", "BUSINESS_VALIDATION_ERROR",
                "Le lot de verrouillage contient des données incohérentes.", coherenceErrors);
        }

        try
        {
            var saveResult = await _expeditionRepository.SaveVerrouillageAsync(
                request,
                dateTournee,
                nowParis,
                adresseIp,
                cancellationToken);

            return ExpeditionOperationResult.Ok(new ExpeditionVerrouillageSuccessResponse
            {
                Statut = "SUCCESS",
                Message = "Préparations Expédition sauvegardées et verrouillées avec succès.",
                IdLotVerrouillage = request.IdLotVerrouillage,
                DateTournee = dateTournee.ToString("yyyy-MM-dd"),
                NombreTourneesVerrouillees = saveResult.NombreTournees,
                NombreLignesRecues = saveResult.NombreLignes,
                NombreQuantitesRecues = saveResult.NombreQuantites,
                DateReceptionApi = nowParis,
                DateSauvegardeSql = saveResult.DateSauvegardeSql
            });
        }
        catch (Exception exception)
        {
            _logger.LogError(exception,
                "Erreur technique pendant le verrouillage Expédition du lot {IdLotVerrouillage}.",
                request.IdLotVerrouillage);

            return ExpeditionOperationResult.TechnicalError("TECHNICAL_ERROR", "SERVER_ERROR",
                "Une erreur technique est survenue pendant le verrouillage Expédition.");
        }
    }

    private static ExpeditionLigneApreparerDto MapLigneApreparer(
        DateOnly dateTournee,
        TourneeLigneRecord ligne,
        IReadOnlyList<ArticleSaisissableRecord> articles)
    {
        var zoneDechargement = NormalizeNullable(ligne.ZoneDechargement);

        return new ExpeditionLigneApreparerDto
        {
            IdLigneSource = BuildIdLigneSource(dateTournee, ligne),
            OrdreArret = ligne.OrdreArret,
            Horaire = ligne.Horaire,
            Client = new ExpeditionClientDto
            {
                NumClient = ligne.NumClient,
                NomClient = ligne.NomClient,
                NomAffiche = ligne.NomAffiche
            },
            PointLivraison = new ExpeditionPointLivraisonDto
            {
                CodePDL = NormalizeNullable(ligne.CodePDL),
                DescriptionPDL = NormalizeNullable(ligne.DescriptionPDL),
                AdresseLigne1 = NormalizeNullable(ligne.AdresseLigne1),
                AdresseLigne2 = NormalizeNullable(ligne.AdresseLigne2),
                AdresseLigne3 = NormalizeNullable(ligne.AdresseLigne3),
                Ville = NormalizeNullable(ligne.Ville),
                CodePostal = NormalizeNullable(ligne.CodePostal)
            },
            Tournee = new ExpeditionTourneeInfoDto
            {
                CodeTournee = ligne.CodeTournee,
                LibelleTournee = ligne.LibelleTournee,
                JourTournee = ligne.JourTournee,
                JourLibelle = GetJourLibelle(ligne.JourTournee),
                SchemaLivraison = NormalizeNullable(ligne.SchemaLivraison)
            },
            InfosLivreur = new ExpeditionInfosLivreurDto
            {
                Instructions = NormalizeNullable(ligne.Instructions),
                ZoneDechargement = zoneDechargement,
                ZoneDechargementAffichee = BuildZoneDechargementAffichee(ligne.JourTourneeRetour, zoneDechargement),
                Zone = NormalizeNullable(ligne.Zone),
                Precision = NormalizeNullable(ligne.Precision),
                Cle = NormalizeNullable(ligne.Cle),
                EstFerme = ligne.EstFerme,
                DateFermeture = ligne.DateFermeture.HasValue ? DateOnly.FromDateTime(ligne.DateFermeture.Value) : null,
                MotifFermeture = NormalizeNullable(ligne.MotifFermeture)
            },
            ArticlesPreparables = articles
                .Select(article => new ExpeditionArticlePreparableDto
                {
                    CodeArticle = article.CodeArticle.Trim().ToUpperInvariant(),
                    LibelleArticle = article.LibelleArticle,
                    QuantiteLivreePrevue = null
                })
                .ToList()
        };
    }

    private static IEnumerable<string> ValidateRequestShape(
        ExpeditionVerrouillageRequest request,
        DateOnly dateMetierApi)
    {
        if (!string.Equals(request.SchemaVersion, SchemaVersionSupportee, StringComparison.OrdinalIgnoreCase))
        {
            yield return $"schemaVersion non supportée. Valeur attendue : {SchemaVersionSupportee}.";
        }

        if (request.IdLotVerrouillage == Guid.Empty)
        {
            yield return "idLotVerrouillage est obligatoire et ne doit pas être vide.";
        }

        if (!DateOnly.TryParse(request.DateTournee, out var dateTournee))
        {
            yield return "dateTournee est obligatoire au format yyyy-MM-dd.";
        }
        else if (dateTournee != dateMetierApi)
        {
            yield return $"dateTournee incohérente pour le verrouillage. Date attendue côté API : {dateMetierApi:yyyy-MM-dd}.";
        }

        if (request.Tournees is null || request.Tournees.Count == 0)
        {
            yield return "tournees doit contenir au moins une tournée à verrouiller.";
            yield break;
        }

        foreach (var tournee in request.Tournees)
        {
            if (string.IsNullOrWhiteSpace(tournee.CodeTournee))
            {
                yield return "tournees[].codeTournee est obligatoire.";
            }

            if (!string.IsNullOrWhiteSpace(tournee.StatutPreparation)
                && tournee.StatutPreparation is not "PRETE_VERROUILLAGE" and not "EN_PREPARATION_WEB")
            {
                yield return $"Statut de préparation non autorisé pour la tournée {tournee.CodeTournee}.";
            }

            if (tournee.Lignes is null)
            {
                yield return $"tournees[].lignes est obligatoire pour la tournée {tournee.CodeTournee}.";
                continue;
            }

            foreach (var ligne in tournee.Lignes)
            {
                if (string.IsNullOrWhiteSpace(ligne.IdLigneSource))
                {
                    yield return $"idLigneSource obligatoire pour la tournée {tournee.CodeTournee}.";
                }

                if (string.IsNullOrWhiteSpace(ligne.NumClient))
                {
                    yield return $"numClient obligatoire pour la ligne {ligne.IdLigneSource}.";
                }

                if (ligne.CommentaireExceptionnel?.Length > 1000)
                {
                    yield return $"commentaireExceptionnel trop long pour la ligne {ligne.IdLigneSource}. Longueur maximale : 1000 caractères.";
                }

                if (ligne.Quantites is null)
                {
                    yield return $"quantites est obligatoire pour la ligne {ligne.IdLigneSource}.";
                    continue;
                }

                foreach (var quantite in ligne.Quantites)
                {
                    if (string.IsNullOrWhiteSpace(quantite.CodeArticle))
                    {
                        yield return $"codeArticle obligatoire pour la ligne {ligne.IdLigneSource}.";
                    }

                    if (quantite.QuantiteLivreePrevue < 0)
                    {
                        yield return $"quantiteLivreePrevue négative interdite pour la ligne {ligne.IdLigneSource}, article {quantite.CodeArticle}.";
                    }
                }
            }
        }
    }

    private async Task<List<string>> ValidateBusinessCoherenceAsync(
        ExpeditionVerrouillageRequest request,
        DateOnly dateTournee,
        HashSet<string> articlesPreparables,
        CancellationToken cancellationToken)
    {
        var errors = new List<string>();

        foreach (var tournee in request.Tournees)
        {
            var codeTournee = NormalizeIdPart(tournee.CodeTournee);
            var lignesSource = (await _tourneesRepository.GetTourneeLinesAsync(
                dateTournee,
                codeLivreur: "EXPEDITION",
                codeTournee: codeTournee)).ToList();

            var lignesValides = lignesSource
                .Select(ligne => BuildIdLigneSource(dateTournee, ligne))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (lignesValides.Count == 0)
            {
                errors.Add($"Tournée introuvable ou sans ligne pour la date {dateTournee:yyyy-MM-dd} : {codeTournee}.");
                continue;
            }

            if (tournee.Lignes is null)
            {
                errors.Add($"tournees[].lignes est obligatoire pour la tournée {tournee.CodeTournee}.");
                continue;
            }

            foreach (var ligne in tournee.Lignes)
            {
                if (!lignesValides.Contains(ligne.IdLigneSource))
                {
                    errors.Add($"idLigneSource inconnu pour la tournée {codeTournee} : {ligne.IdLigneSource}.");
                }

                if (ligne.Quantites is null)
                {
                    errors.Add($"quantites est obligatoire pour la ligne {ligne.IdLigneSource}.");
                    continue;
                }

                foreach (var quantite in ligne.Quantites)
                {
                    var codeArticle = NormalizeIdPart(quantite.CodeArticle).ToUpperInvariant();

                    if (IsArticleInterditExpedition(codeArticle))
                    {
                        errors.Add($"Article non autorisé côté Expédition : {codeArticle}. Les rolls vides récupérés restent une saisie mobile ou administrative.");
                    }
                    else if (!articlesPreparables.Contains(codeArticle))
                    {
                        errors.Add($"Article inconnu ou non préparables côté Expédition : {codeArticle}.");
                    }
                }
            }
        }

        return errors;
    }

    private static bool IsArticleInterditExpedition(string? codeArticle)
    {
        return string.Equals(NormalizeIdPart(codeArticle), CodeArticleRollsVides, StringComparison.OrdinalIgnoreCase);
    }

    private static DateTimeOffset GetParisNow()
    {
        var timeZone = GetParisTimeZone();
        return TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, timeZone);
    }

    private static TimeZoneInfo GetParisTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Romance Standard Time");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Europe/Paris");
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Europe/Paris");
        }
    }

    private static string BuildIdLigneSource(DateOnly dateTournee, TourneeLigneRecord ligne)
    {
        var date = dateTournee.ToString("yyyy-MM-dd");
        var codeTournee = NormalizeIdPart(ligne.CodeTournee);
        var jour = ligne.JourTournee?.ToString() ?? "0";
        var numClient = NormalizeIdPart(ligne.NumClient);
        var codePdl = NormalizeIdPart(ligne.CodePDL);
        var ordreArret = ligne.OrdreArret?.ToString() ?? "0";

        return $"{date}|{codeTournee}|{jour}|{numClient}|{codePdl}|{ordreArret}";
    }

    private static string? BuildZoneDechargementAffichee(int? jourTourneeRetour, string? zoneDechargement)
    {
        var jourRetour = jourTourneeRetour?.ToString();
        var zone = NormalizeNullable(zoneDechargement);

        if (zone is null)
        {
            return jourRetour;
        }

        if (string.IsNullOrWhiteSpace(jourRetour))
        {
            return zone;
        }

        return $"{jourRetour} - {zone}";
    }

    private static string? NormalizeNullable(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string NormalizeIdPart(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private static string? GetJourLibelle(int? jour)
    {
        return jour switch
        {
            1 => "Lundi",
            2 => "Mardi",
            3 => "Mercredi",
            4 => "Jeudi",
            5 => "Vendredi",
            6 => "Samedi",
            7 => "Dimanche",
            _ => null
        };
    }

    private static int TryParseInt(string? value)
    {
        return int.TryParse(value, out var number) ? number : int.MaxValue;
    }
}

public sealed class ExpeditionOperationResult
{
    public int StatusCode { get; private init; }
    public object Body { get; private init; } = new();

    public static ExpeditionOperationResult Ok(object body)
    {
        return new ExpeditionOperationResult
        {
            StatusCode = StatusCodes.Status200OK,
            Body = body
        };
    }

    public static ExpeditionOperationResult BadRequest(string statut, string code, string message, IEnumerable<string> errors)
    {
        return new ExpeditionOperationResult
        {
            StatusCode = StatusCodes.Status400BadRequest,
            Body = new ExpeditionErrorResponse
            {
                Statut = statut,
                Code = code,
                Message = message,
                Errors = errors
            }
        };
    }

    public static ExpeditionOperationResult Conflict(string statut, string code, string message, IEnumerable<string> errors)
    {
        return new ExpeditionOperationResult
        {
            StatusCode = StatusCodes.Status409Conflict,
            Body = new ExpeditionErrorResponse
            {
                Statut = statut,
                Code = code,
                Message = message,
                Errors = errors
            }
        };
    }

    public static ExpeditionOperationResult TechnicalError(string statut, string code, string message)
    {
        return new ExpeditionOperationResult
        {
            StatusCode = StatusCodes.Status500InternalServerError,
            Body = new ExpeditionErrorResponse
            {
                Statut = statut,
                Code = code,
                Message = message,
                Errors = Array.Empty<string>()
            }
        };
    }
}
