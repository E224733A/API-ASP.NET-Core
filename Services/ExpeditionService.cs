using API_ASP.NET_Core.Constants;
using API_ASP.NET_Core.Mappers;
using API_ASP.NET_Core.Models;
using API_ASP.NET_Core.Repositories;
using Microsoft.AspNetCore.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace API_ASP.NET_Core.Services;

public sealed class ExpeditionService
{
    private const string SchemaVersionExpedition = "1.2";
    private const string SourceAttendue = "APPLICATION_WEB_EXPEDITION";
    private const string FuseauHoraireMetier = "Europe/Paris";

    private static readonly HashSet<string> ArticlesAutorises = new(StringComparer.OrdinalIgnoreCase)
    {
        "ROLLS",
        "TAPIS",
        "SACS"
    };

    private static readonly HashSet<string> StatutsPreparationWebAutorises = new(StringComparer.OrdinalIgnoreCase)
    {
        "PRETE_VERROUILLAGE",
        "EN_PREPARATION_WEB"
    };

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

    /// <summary>
    /// GET global Expédition.
    /// La date est calculée côté API. En première version, elle correspond au lendemain calendaire.
    /// </summary>
    public async Task<ExpeditionPreparationResponseDto> GetPreparationsAPreparerAsync(
        CancellationToken cancellationToken = default)
    {
        var dateTournee = GetDatePreparable();

        var lignes = (await _tourneesRepository.GetTourneeLinesAsync(
            dateTournee,
            codeLivreur: string.Empty,
            codeTournee: null)).ToList();

        var articles = await GetArticlesPreparablesAsync();

        var response = new ExpeditionPreparationResponseDto
        {
            Statut = "SUCCESS",
            SchemaVersion = SchemaVersionExpedition,
            DateTournee = dateTournee.ToString("yyyy-MM-dd"),
            DatePreparable = dateTournee.ToString("yyyy-MM-dd"),
            DateModifiable = false,
            FuseauHoraireMetier = FuseauHoraireMetier,
            DateGenerationApi = DateTimeOffset.Now.ToString("yyyy-MM-dd'T'HH:mm:sszzz"),
            ArticlesPreparables = articles
                .Select(article => new ExpeditionArticlePreparableDto
                {
                    CodeArticle = NormalizeArticleCode(article.CodeArticle),
                    Libelle = article.LibelleArticle,
                    TypeQuantite = "LIVREE_PREVUE",
                    QuantiteNullable = true,
                    OrdreAffichage = article.OrdreAffichage
                })
                .ToList(),
            Regles = BuildRegles()
        };

        if (lignes.Count == 0)
        {
            response.Message = "Aucune tournée préparable pour la date calculée.";
            return response;
        }

        foreach (var groupeTournee in lignes
            .Where(ligne => !string.IsNullOrWhiteSpace(ligne.CodeTournee))
            .GroupBy(ligne => ligne.CodeTournee, StringComparer.OrdinalIgnoreCase)
            .OrderBy(groupe => groupe.Key))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var codeTournee = groupeTournee.Key.Trim();
            var lignesTournee = groupeTournee.ToList();

            var preRemplissages = (await _tourneesRepository.GetPreRemplissagesAsync(
                dateTournee,
                codeTournee)).ToList();

            var commentaires = (await _tourneesRepository.GetCommentairesExceptionnelsAsync(
                dateTournee,
                codeTournee)).ToList();

            var etatPreparation = await _expeditionRepository.GetPreparationEtatAsync(
                dateTournee.ToDateTime(TimeOnly.MinValue),
                codeTournee,
                cancellationToken);

            response.Tournees.Add(new ExpeditionPreparationTourneeDto
            {
                CodeTournee = codeTournee,
                LibelleTournee = lignesTournee.FirstOrDefault()?.LibelleTournee,
                StatutPreparationWeb = etatPreparation is not null && !etatPreparation.EstVerrouille
                    ? "EN_PREPARATION_WEB"
                    : "PRETE_VERROUILLAGE",
                Lignes = lignesTournee.Select(ligne =>
                {
                    var idLigneSource = TourneeMobileMapper.BuildIdLigneSource(dateTournee, ligne);

                    return new ExpeditionPreparationLigneDto
                    {
                        IdLigneSource = idLigneSource,
                        OrdreArret = ligne.OrdreArret,
                        Client = new ExpeditionClientDto
                        {
                            NumClient = ligne.NumClient,
                            NomClient = ligne.NomClient,
                            NomAffiche = ligne.NomAffiche
                        },
                        PointLivraison = new ExpeditionPointLivraisonDto
                        {
                            CodePDL = ligne.CodePDL,
                            DescriptionPDL = ligne.DescriptionPDL,
                            AdresseLigne1 = ligne.AdresseLigne1,
                            AdresseLigne2 = ligne.AdresseLigne2,
                            AdresseLigne3 = ligne.AdresseLigne3,
                            Ville = ligne.Ville,
                            CodePostal = ligne.CodePostal
                        },
                        InfosLecture = new ExpeditionInfosLectureDto
                        {
                            Horaire = ligne.Horaire?.ToString(),
                            CodeTournee = ligne.CodeTournee,
                            LibelleTournee = ligne.LibelleTournee,
                            Instructions = ligne.Instructions,
                            EstFerme = ligne.EstFerme,
                            DateFermeture = ligne.DateFermeture?.ToString("yyyy-MM-dd"),
                            MotifFermeture = ligne.MotifFermeture,
                            ZoneDechargement = ligne.ZoneDechargement
                        },
                        PreparationInitiale = new ExpeditionPreparationInitialeDto
                        {
                            CommentaireExceptionnel = FindCommentaireExceptionnel(idLigneSource, ligne, commentaires),
                            QuantitesPrevues = articles.Select(article =>
                            {
                                var preRemplissage = FindPreRemplissage(
                                    idLigneSource,
                                    ligne,
                                    article.CodeArticle,
                                    preRemplissages);

                                return new ExpeditionQuantitePrevueDto
                                {
                                    CodeArticle = NormalizeArticleCode(article.CodeArticle),
                                    Libelle = preRemplissage?.LibelleArticle ?? article.LibelleArticle,
                                    QuantiteLivreePrevue = preRemplissage?.QuantiteLivreePrevue
                                };
                            }).ToList()
                        }
                    };
                }).ToList()
            });
        }

        return response;
    }

    /// <summary>
    /// POST global Expédition v1.2.
    /// Vérifie le lot, contrôle les lignes et articles, puis verrouille les tournées dans une transaction SQL.
    /// </summary>
    public async Task<(int StatusCode, ExpeditionApiResult Body)> VerrouillerPreparationLotAsync(
        ExpeditionVerrouillageLotRequest? request,
        string? adresseIp,
        CancellationToken cancellationToken = default)
    {
        var errors = await ValidateVerrouillageLotRequestAsync(request, cancellationToken);

        if (errors.Count > 0 || request is null)
        {
            return (
                StatusCodes.Status400BadRequest,
                new ExpeditionApiResult
                {
                    Statut = "VALIDATION_ERROR",
                    Code = "EXPEDITION_VALIDATION_ERROR",
                    Message = "Le lot de préparation Expédition contient des données invalides.",
                    Errors = errors
                });
        }

        var dateTournee = DateTime.Parse(request.DateTournee).Date;
        var idLotVerrouillageTechnique = BuildDeterministicGuid(request.IdLotVerrouillage);
        var empreintePayload = ComputePayloadFingerprint(request);

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
                        IdLotVerrouillage = request.IdLotVerrouillage,
                        DateTournee = request.DateTournee,
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
                    IdLotVerrouillage = request.IdLotVerrouillage,
                    DateTournee = request.DateTournee
                });
        }

        try
        {
            var result = await _expeditionRepository.EnregistrerVerrouillageLotAsync(
                request,
                dateTournee,
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
                    IdLotVerrouillage = request.IdLotVerrouillage,
                    DateTournee = request.DateTournee,
                    StatutVerrouillage = "VERROUILLEE_BD",
                    DateReceptionApi = result.DateReceptionApi.ToString("yyyy-MM-dd'T'HH:mm:sszzz"),
                    DateSauvegardeSql = result.DateSauvegardeSql.ToString("yyyy-MM-dd'T'HH:mm:sszzz"),
                    NombreTourneesVerrouillees = result.NombreTourneesVerrouillees,
                    NombreLignesVerrouillees = result.NombreLignesVerrouillees
                });
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Erreur lors du verrouillage du lot Expédition {IdLotVerrouillage}.",
                request.IdLotVerrouillage);

            return (
                StatusCodes.Status500InternalServerError,
                new ExpeditionApiResult
                {
                    Statut = "ERROR",
                    Code = "SERVER_ERROR",
                    Message = "Une erreur technique est survenue pendant le verrouillage Expédition.",
                    IdLotVerrouillage = request.IdLotVerrouillage,
                    DateTournee = request.DateTournee
                });
        }
    }

    private async Task<List<ArticleSaisissableRecord>> GetArticlesPreparablesAsync()
    {
        var articles = (await _tourneesRepository.GetArticlesSaisissablesAsync())
            .Where(article => IsArticleAutoriseExpedition(article.CodeArticle))
            .OrderBy(article => article.OrdreAffichage)
            .ThenBy(article => article.CodeArticle)
            .ToList();

        if (articles.Count > 0)
        {
            return articles;
        }

        return ArticlesSaisissables.ActifsV1
            .Where(article => IsArticleAutoriseExpedition(article.CodeArticle))
            .Select((article, index) => new ArticleSaisissableRecord
            {
                CodeArticle = article.CodeArticle,
                LibelleArticle = article.Libelle,
                OrdreAffichage = index + 1
            })
            .ToList();
    }

    private async Task<List<string>> ValidateVerrouillageLotRequestAsync(
        ExpeditionVerrouillageLotRequest? request,
        CancellationToken cancellationToken)
    {
        var errors = new List<string>();

        if (request is null)
        {
            errors.Add("Le corps JSON du lot Expédition est obligatoire.");
            return errors;
        }

        if (!string.Equals(request.SchemaVersion?.Trim(), SchemaVersionExpedition, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("schemaVersion doit être égal à \"1.2\".");
        }

        if (string.IsNullOrWhiteSpace(request.IdLotVerrouillage))
        {
            errors.Add("idLotVerrouillage est obligatoire.");
        }

        if (!string.Equals(request.Source?.Trim(), SourceAttendue, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add($"source doit être égal à \"{SourceAttendue}\".");
        }

        if (!DateTime.TryParse(request.DateTournee, out var dateTournee))
        {
            errors.Add("dateTournee est obligatoire et doit être une date valide au format yyyy-MM-dd.");
        }
        else
        {
            dateTournee = dateTournee.Date;
            var datePreparable = GetDatePreparable().ToDateTime(TimeOnly.MinValue).Date;
            if (dateTournee != datePreparable)
            {
                errors.Add($"dateTournee doit correspondre à la date préparable calculée par l'API : {datePreparable:yyyy-MM-dd}.");
            }
        }

        if (!DateTimeOffset.TryParse(request.DateVerrouillageDemandee, out var dateVerrouillageDemandee))
        {
            errors.Add("dateVerrouillageDemandee est obligatoire et doit être une date ISO 8601 avec offset.");
        }
        else if (dateVerrouillageDemandee.Offset == TimeSpan.Zero && !request.DateVerrouillageDemandee.EndsWith("+00:00", StringComparison.Ordinal))
        {
            errors.Add("dateVerrouillageDemandee doit contenir un offset horaire explicite.");
        }

        if (!string.Equals(request.FuseauHoraireMetier?.Trim(), FuseauHoraireMetier, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add($"fuseauHoraireMetier doit être égal à \"{FuseauHoraireMetier}\".");
        }

        if (request.Tournees is null || request.Tournees.Count == 0)
        {
            errors.Add("tournees doit contenir au moins une tournée.");
            return errors;
        }

        if (DateTime.TryParse(request.DateTournee, out var validDate))
        {
            var lignesPreparables = (await _tourneesRepository.GetTourneeLinesAsync(
                DateOnly.FromDateTime(validDate.Date),
                codeLivreur: string.Empty,
                codeTournee: null)).ToList();

            var idLignesPreparables = lignesPreparables
                .Select(ligne => TourneeMobileMapper.BuildIdLigneSource(DateOnly.FromDateTime(validDate.Date), ligne))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            ValidateTournees(request, idLignesPreparables, errors);
        }

        return errors;
    }

    private static void ValidateTournees(
        ExpeditionVerrouillageLotRequest request,
        HashSet<string> idLignesPreparables,
        List<string> errors)
    {
        var idLignesGlobal = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var indexTournee = 0; indexTournee < request.Tournees.Count; indexTournee++)
        {
            var tournee = request.Tournees[indexTournee];
            var prefixTournee = $"tournees[{indexTournee}]";

            if (string.IsNullOrWhiteSpace(tournee.CodeTournee))
            {
                errors.Add($"{prefixTournee}.codeTournee est obligatoire.");
            }

            if (!string.IsNullOrWhiteSpace(tournee.StatutPreparationWeb) &&
                !StatutsPreparationWebAutorises.Contains(tournee.StatutPreparationWeb))
            {
                errors.Add($"{prefixTournee}.statutPreparationWeb doit être PRETE_VERROUILLAGE ou EN_PREPARATION_WEB.");
            }

            if (tournee.Lignes is null || tournee.Lignes.Count == 0)
            {
                errors.Add($"{prefixTournee}.lignes doit contenir au moins une ligne.");
                continue;
            }

            var idLignesTournee = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (var indexLigne = 0; indexLigne < tournee.Lignes.Count; indexLigne++)
            {
                var ligne = tournee.Lignes[indexLigne];
                var prefixLigne = $"{prefixTournee}.lignes[{indexLigne}]";

                if (string.IsNullOrWhiteSpace(ligne.IdLigneSource))
                {
                    errors.Add($"{prefixLigne}.idLigneSource est obligatoire.");
                }
                else
                {
                    var idLigne = ligne.IdLigneSource.Trim();

                    if (!idLignesTournee.Add(idLigne))
                    {
                        errors.Add($"{prefixLigne}.idLigneSource est présent plusieurs fois dans la même tournée.");
                    }

                    if (!idLignesGlobal.Add(idLigne))
                    {
                        errors.Add($"{prefixLigne}.idLigneSource est présent dans plusieurs tournées du lot.");
                    }

                    if (!idLignesPreparables.Contains(idLigne))
                    {
                        errors.Add($"{prefixLigne}.idLigneSource n'existe pas dans les lignes préparables de la date.");
                    }
                }

                if (string.IsNullOrWhiteSpace(ligne.Client?.NumClient))
                {
                    errors.Add($"{prefixLigne}.client.numClient est obligatoire.");
                }

                if (ligne.QuantitesPrevues is null)
                {
                    continue;
                }

                var codesArticles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (var indexQuantite = 0; indexQuantite < ligne.QuantitesPrevues.Count; indexQuantite++)
                {
                    var quantite = ligne.QuantitesPrevues[indexQuantite];
                    var prefixQuantite = $"{prefixLigne}.quantitesPrevues[{indexQuantite}]";

                    if (string.IsNullOrWhiteSpace(quantite.CodeArticle))
                    {
                        errors.Add($"{prefixQuantite}.codeArticle est obligatoire.");
                        continue;
                    }

                    var codeArticle = NormalizeArticleCode(quantite.CodeArticle);

                    if (!codesArticles.Add(codeArticle))
                    {
                        errors.Add($"{prefixQuantite}.codeArticle est présent plusieurs fois dans la même ligne.");
                    }

                    if (IsRollsVides(codeArticle))
                    {
                        errors.Add($"{prefixQuantite}.codeArticle ROLLS_VIDES est interdit côté Expédition.");
                    }

                    if (!IsArticleAutoriseExpedition(codeArticle))
                    {
                        errors.Add($"{prefixQuantite}.codeArticle doit être ROLLS, TAPIS ou SACS.");
                    }

                    if (quantite.QuantiteLivreePrevue.HasValue && quantite.QuantiteLivreePrevue.Value < 0)
                    {
                        errors.Add($"{prefixQuantite}.quantiteLivreePrevue doit être positive ou nulle.");
                    }
                }
            }
        }
    }

    private static ExpeditionReglesDto BuildRegles()
    {
        return new ExpeditionReglesDto
        {
            HeureVerrouillageMetier = "00:05",
            FuseauHoraireMetier = FuseauHoraireMetier,
            FenetreModification = "Les préparations sont modifiables avant le verrouillage automatique autour de 00:05.",
            ArticlesAutorises = ArticlesAutorises.OrderBy(code => code).ToList(),
            ArticlesInterdits = new List<string> { ArticlesSaisissables.RollsVides },
            ExclureRollsVides = true,
            QuantitesNullesAutorisees = true
        };
    }

    private static DateOnly GetDatePreparable()
    {
        return DateOnly.FromDateTime(DateTime.Today.AddDays(1));
    }

    private static string ComputePayloadFingerprint(ExpeditionVerrouillageLotRequest request)
    {
        var normalizedPayload = new
        {
            schemaVersion = SchemaVersionExpedition,
            idLotVerrouillage = NormalizeNullable(request.IdLotVerrouillage),
            source = NormalizeNullable(request.Source),
            dateTournee = NormalizeNullable(request.DateTournee),
            dateVerrouillageDemandee = NormalizeNullable(request.DateVerrouillageDemandee),
            fuseauHoraireMetier = NormalizeNullable(request.FuseauHoraireMetier),
            tournees = request.Tournees
                .OrderBy(tournee => tournee.CodeTournee, StringComparer.OrdinalIgnoreCase)
                .Select(tournee => new
                {
                    codeTournee = NormalizeNullable(tournee.CodeTournee),
                    libelleTournee = NormalizeNullable(tournee.LibelleTournee),
                    statutPreparationWeb = NormalizeNullable(tournee.StatutPreparationWeb),
                    lignes = tournee.Lignes
                        .OrderBy(ligne => ligne.IdLigneSource, StringComparer.OrdinalIgnoreCase)
                        .Select(ligne => new
                        {
                            idLigneSource = NormalizeNullable(ligne.IdLigneSource),
                            ordreArret = ligne.OrdreArret,
                            numClient = NormalizeNullable(ligne.Client?.NumClient),
                            codePDL = NormalizeNullable(ligne.PointLivraison?.CodePDL),
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

    private static PreRemplissageQuantiteRecord? FindPreRemplissage(
        string idLigneSource,
        TourneeLigneRecord ligne,
        string codeArticle,
        IReadOnlyList<PreRemplissageQuantiteRecord> preRemplissages)
    {
        var byIdLigneSource = preRemplissages.FirstOrDefault(preRemplissage =>
            string.Equals(preRemplissage.IdLigneSource, idLigneSource, StringComparison.OrdinalIgnoreCase)
            && string.Equals(preRemplissage.CodeArticle, codeArticle, StringComparison.OrdinalIgnoreCase));

        if (byIdLigneSource is not null)
        {
            return byIdLigneSource;
        }

        return preRemplissages.FirstOrDefault(preRemplissage =>
            string.Equals(preRemplissage.NumClient, ligne.NumClient, StringComparison.OrdinalIgnoreCase)
            && string.Equals(NormalizeIdPart(preRemplissage.CodePDL), NormalizeIdPart(ligne.CodePDL), StringComparison.OrdinalIgnoreCase)
            && string.Equals(preRemplissage.CodeArticle, codeArticle, StringComparison.OrdinalIgnoreCase));
    }

    private static string? FindCommentaireExceptionnel(
        string idLigneSource,
        TourneeLigneRecord ligne,
        IReadOnlyList<CommentaireExceptionnelRecord> commentaires)
    {
        var commentaireLigne = commentaires.FirstOrDefault(commentaire =>
            !string.IsNullOrWhiteSpace(commentaire.IdLigneSource)
            && string.Equals(commentaire.IdLigneSource, idLigneSource, StringComparison.OrdinalIgnoreCase));

        if (commentaireLigne is not null)
        {
            return NormalizeNullable(commentaireLigne.Commentaire);
        }

        var commentaireExact = commentaires.FirstOrDefault(commentaire =>
            string.Equals(commentaire.NumClient, ligne.NumClient, StringComparison.OrdinalIgnoreCase)
            && string.Equals(NormalizeIdPart(commentaire.CodePDL), NormalizeIdPart(ligne.CodePDL), StringComparison.OrdinalIgnoreCase));

        if (commentaireExact is not null)
        {
            return NormalizeNullable(commentaireExact.Commentaire);
        }

        var commentaireClient = commentaires.FirstOrDefault(commentaire =>
            string.Equals(commentaire.NumClient, ligne.NumClient, StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(commentaire.CodePDL));

        return NormalizeNullable(commentaireClient?.Commentaire);
    }

    private static bool IsArticleAutoriseExpedition(string? codeArticle)
    {
        return ArticlesAutorises.Contains(NormalizeArticleCode(codeArticle));
    }

    private static bool IsRollsVides(string? codeArticle)
    {
        return string.Equals(NormalizeArticleCode(codeArticle), ArticlesSaisissables.RollsVides, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeArticleCode(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToUpperInvariant();
    }

    private static string NormalizeIdPart(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "NA"
            : value.Trim();
    }

    private static string? NormalizeNullable(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}
