using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using API_ASP.NET_Core.Constants;
using API_ASP.NET_Core.Mappers;
using API_ASP.NET_Core.Models;
using API_ASP.NET_Core.Repositories;
using Microsoft.AspNetCore.Http;

namespace API_ASP.NET_Core.Services;

public sealed class ExpeditionService
{
    private const string SchemaVersionExpedition = "1.2";
    private const string SourceAttendue = "APPLICATION_WEB_EXPEDITION";
    private const string FuseauHoraireMetier = "Europe/Paris";

    private static readonly HashSet<string> ArticlesAutorises = new(StringComparer.OrdinalIgnoreCase)
    {
        "ROLLS",
        "ROLLS_VIDES",
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
    private readonly DateMetierService _dateMetierService;
    private readonly ILogger<ExpeditionService> _logger;

    public ExpeditionService(
        TourneesRepository tourneesRepository,
        ExpeditionRepository expeditionRepository,
        DateMetierService dateMetierService,
        ILogger<ExpeditionService> logger)
    {
        _tourneesRepository = tourneesRepository;
        _expeditionRepository = expeditionRepository;
        _dateMetierService = dateMetierService;
        _logger = logger;
    }

    /// <summary>
    /// GET global Expédition.
    /// La date est calculée côté API avec la date métier Europe/Paris.
    /// Règle métier : l'Expédition prépare le prochain jour ouvré métier.
    /// Version actuelle : le samedi et le dimanche sont ignorés.
    /// </summary>
    public async Task<ExpeditionPreparationResponseDto> GetPreparationsAPreparerAsync(
        CancellationToken cancellationToken = default)
    {
        var dateTournee = _dateMetierService.GetDateTourneeExpeditionPreparable();

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
            DateGenerationApi = _dateMetierService.GetNowParis().ToString("yyyy-MM-dd'T'HH:mm:sszzz"),
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
            response.Message = "Aucune tournée préparable pour la date métier Expédition calculée par l'API.";
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
    /// La date autorisée est la même que celle du GET : prochain jour ouvré métier Expédition.
    /// </summary>
    public async Task<(int StatusCode, object Body)> VerrouillerPreparationLotAsync(
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

        var dateTournee = DateTime.Parse(request.DateTournee, CultureInfo.InvariantCulture).Date;
        var dateTourneePayload = DateOnly.FromDateTime(dateTournee);
        var dateTourneeAutorisee = _dateMetierService.GetDateTourneeExpeditionPreparable();

        if (dateTourneePayload != dateTourneeAutorisee)
        {
            return (
                StatusCodes.Status409Conflict,
                BuildDateTourneeNonAutoriseeResponse(
                    dateTourneePayload,
                    dateTourneeAutorisee,
                    "verrouillée"));
        }

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
        DateTime? dateTourneeValide = null;
        DateTimeOffset? dateVerrouillageDemandeeValide = null;

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

        if (!DateTime.TryParse(request.DateTournee, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateTournee))
        {
            errors.Add("dateTournee est obligatoire et doit être une date valide au format yyyy-MM-dd.");
        }
        else
        {
            dateTourneeValide = dateTournee.Date;
        }

        if (!DateTimeOffset.TryParse(request.DateVerrouillageDemandee, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateVerrouillageDemandee))
        {
            errors.Add("dateVerrouillageDemandee est obligatoire et doit être une date ISO 8601 avec offset.");
        }
        else if (!HasExplicitOffset(request.DateVerrouillageDemandee))
        {
            errors.Add("dateVerrouillageDemandee doit contenir un offset horaire explicite.");
        }
        else
        {
            dateVerrouillageDemandeeValide = dateVerrouillageDemandee;
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

        if (dateTourneeValide.HasValue)
        {
            var dateAutorisee = _dateMetierService.GetDateTourneeExpeditionPreparable().ToDateTime(TimeOnly.MinValue).Date;

            if (dateTourneeValide.Value == dateAutorisee)
            {
                var lignesPreparables = (await _tourneesRepository.GetTourneeLinesAsync(
                    DateOnly.FromDateTime(dateTourneeValide.Value),
                    codeLivreur: string.Empty,
                    codeTournee: null)).ToList();

                var idLignesPreparables = lignesPreparables
                    .Select(ligne => TourneeMobileMapper.BuildIdLigneSource(DateOnly.FromDateTime(dateTourneeValide.Value), ligne))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                ValidateTournees(request, idLignesPreparables, errors, dateVerrouillageDemandeeValide);
            }
            else
            {
                ValidateTourneesSansControleLignes(request, errors, dateVerrouillageDemandeeValide);
            }
        }
        else
        {
            ValidateTourneesSansControleLignes(request, errors, dateVerrouillageDemandeeValide);
        }

        return errors;
    }

    private static void ValidateTournees(
        ExpeditionVerrouillageLotRequest request,
        HashSet<string> idLignesPreparables,
        List<string> errors,
        DateTimeOffset? dateVerrouillageDemandee)
    {
        ValidateTourneesCore(request, errors, dateVerrouillageDemandee, idLignesPreparables);
    }

    private static void ValidateTourneesSansControleLignes(
        ExpeditionVerrouillageLotRequest request,
        List<string> errors,
        DateTimeOffset? dateVerrouillageDemandee)
    {
        ValidateTourneesCore(request, errors, dateVerrouillageDemandee, idLignesPreparables: null);
    }

    private static void ValidateTourneesCore(
        ExpeditionVerrouillageLotRequest request,
        List<string> errors,
        DateTimeOffset? dateVerrouillageDemandee,
        HashSet<string>? idLignesPreparables)
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

            ValidateDateModificationTournee(tournee, prefixTournee, errors, dateVerrouillageDemandee);

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

                    if (idLignesPreparables is not null && !idLignesPreparables.Contains(idLigne))
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

                    if (!IsArticleAutoriseExpedition(codeArticle))
                    {
                        errors.Add($"{prefixQuantite}.codeArticle doit être ROLLS, ROLLS_VIDES, TAPIS ou SACS.");
                    }

                    if (quantite.QuantiteLivreePrevue.HasValue && quantite.QuantiteLivreePrevue.Value < 0)
                    {
                        errors.Add($"{prefixQuantite}.quantiteLivreePrevue doit être positive ou nulle.");
                    }
                }
            }
        }
    }

    private static void ValidateDateModificationTournee(
        ExpeditionVerrouillageTourneeRequest tournee,
        string prefixTournee,
        List<string> errors,
        DateTimeOffset? dateVerrouillageDemandee)
    {
        if (string.IsNullOrWhiteSpace(tournee.DateModification))
        {
            return;
        }

        if (!DateTimeOffset.TryParse(tournee.DateModification, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateModification))
        {
            errors.Add($"{prefixTournee}.dateModification doit être une date ISO 8601 valide avec offset.");
            return;
        }

        if (!HasExplicitOffset(tournee.DateModification))
        {
            errors.Add($"{prefixTournee}.dateModification doit contenir un offset horaire explicite.");
            return;
        }

        if (dateVerrouillageDemandee.HasValue && dateModification > dateVerrouillageDemandee.Value.AddMinutes(5))
        {
            errors.Add($"{prefixTournee}.dateModification ne peut pas être postérieure à dateVerrouillageDemandee.");
        }
    }

    private static ExpeditionReglesDto BuildRegles()
    {
        return new ExpeditionReglesDto
        {
            HeureVerrouillageMetier = "22:35",
            FuseauHoraireMetier = FuseauHoraireMetier,
            FenetreModification = "Les préparations sont modifiables avant le verrouillage automatique entre 22:35 et 22:55.",
            ArticlesAutorises = ArticlesAutorises.OrderBy(code => code).ToList(),
            ArticlesInterdits = new List<string>(),
            ExclureRollsVides = false,
            QuantitesNullesAutorisees = true
        };
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

    private static string ComputePayloadFingerprint(ExpeditionVerrouillageLotRequest request)
    {
        var normalizedPayload = new
        {
            schemaVersion = SchemaVersionExpedition,
            idLotVerrouillage = NormalizeNullable(request.IdLotVerrouillage),
            source = NormalizeNullable(request.Source),
            dateTournee = NormalizeNullable(request.DateTournee),
            dateVerrouillageDemandee = NormalizeDateTimeOffsetForFingerprint(request.DateVerrouillageDemandee),
            fuseauHoraireMetier = NormalizeNullable(request.FuseauHoraireMetier),
            tournees = request.Tournees
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
                            numClient = NormalizeNullable(ligne.Client?.NumClient),
                            codePDL = NormalizeNullable(ligne.PointLivraison?.CodePDL),
                            commentaireExceptionnel = NormalizeNullable(ligne.CommentaireExceptionnel),
                            quantitesPrevues = (ligne.QuantitesPrevues ?? new List<ExpeditionQuantitePrevueRequest>())
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

    private static bool HasExplicitOffset(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();
        if (trimmed.EndsWith("Z", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return trimmed.Length >= 6
            && (trimmed[^6] == '+' || trimmed[^6] == '-')
            && char.IsDigit(trimmed[^5])
            && char.IsDigit(trimmed[^4])
            && trimmed[^3] == ':'
            && char.IsDigit(trimmed[^2])
            && char.IsDigit(trimmed[^1]);
    }
}
