using API_ASP.NET_Core.Constants;
using API_ASP.NET_Core.Mappers;
using API_ASP.NET_Core.Models;
using API_ASP.NET_Core.Repositories;

namespace API_ASP.NET_Core.Services;

/// <summary>
/// Service de lecture du flux Expédition à préparer.
/// </summary>
/// <remarks>
/// Ce service construit le contrat JSON du GET global Expédition. Il calcule la date
/// préparable via <see cref="DateMetierService"/>, lit les lignes de tournée, rattache
/// les quantités prévues déjà préparées et expose les règles nécessaires à ServeWeb.
/// </remarks>
public sealed class ExpeditionService
{
    private const string SchemaVersionExpedition = "1.2";
    private const string FuseauHoraireMetier = "Europe/Paris";

    // Articles autorisés dans la préparation Expédition. ROLLS_VIDES est volontairement conservé.
    private static readonly HashSet<string> ArticlesAutorises = new(StringComparer.OrdinalIgnoreCase)
    {
        "ROLLS",
        "ROLLS_VIDES",
        "TAPIS",
        "SACS"
    };

    private readonly TourneesRepository _tourneesRepository;
    private readonly ExpeditionRepository _expeditionRepository;
    private readonly DateMetierService _dateMetierService;

    public ExpeditionService(
        TourneesRepository tourneesRepository,
        ExpeditionRepository expeditionRepository,
        DateMetierService dateMetierService)
    {
        _tourneesRepository = tourneesRepository;
        _expeditionRepository = expeditionRepository;
        _dateMetierService = dateMetierService;
    }

    /// <summary>
    /// Charge toutes les préparations Expédition à préparer pour la date métier calculée par l'API.
    /// </summary>
    /// <remarks>
    /// Règle métier : l'Expédition prépare le prochain jour ouvré métier. Le GET est global :
    /// ServeWeb ne choisit pas la date, la tournée ou le livreur. Les week-ends sont ignorés
    /// dans la version actuelle de la règle de date.
    /// </remarks>
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
                // Ce statut décrit l'état central connu par l'API au moment du chargement.
                // L'éligibilité finale au verrouillage reste décidée côté SERVWEB par l'état local créé après le clic humain "prêt".
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
    /// Retourne les articles affichés dans l'interface Expédition pour la saisie des quantités prévues.
    /// </summary>
    /// <remarks>
    /// Le référentiel SQL est prioritaire. Si aucun article actif n'est disponible, le service
    /// utilise la liste applicative de secours afin que le GET Expédition reste exploitable.
    /// </remarks>
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

    /// <summary>
    /// Construit les règles déclaratives renvoyées à ServeWeb avec le contrat Expédition.
    /// </summary>
    /// <remarks>
    /// Ces informations documentent côté client les contraintes principales : heure de
    /// verrouillage, fuseau métier, articles autorisés et acceptation des quantités nulles.
    /// </remarks>
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

    /// <summary>
    /// Retrouve une quantité prévue déjà sauvegardée pour préremplir la préparation Expédition.
    /// </summary>
    /// <remarks>
    /// La recherche privilégie idLigneSource. Le repli NumClient + CodePDL conserve la compatibilité
    /// avec les données qui ne portent pas encore l'identifiant stable.
    /// </remarks>
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

    /// <summary>
    /// Retrouve le commentaire exceptionnel à afficher dans la préparation Expédition.
    /// </summary>
    /// <remarks>
    /// La priorité suit le même principe que le mobile : commentaire par idLigneSource,
    /// puis commentaire exact NumClient + CodePDL, puis commentaire global client.
    /// </remarks>
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

    /// <summary>
    /// Indique si un article est autorisé dans le flux de préparation Expédition.
    /// </summary>
    private static bool IsArticleAutoriseExpedition(string? codeArticle)
    {
        return ArticlesAutorises.Contains(NormalizeArticleCode(codeArticle));
    }

    /// <summary>
    /// Normalise un code article avant comparaison ou exposition dans le contrat Expédition.
    /// </summary>
    private static string NormalizeArticleCode(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToUpperInvariant();
    }

    /// <summary>
    /// Normalise une partie d'identifiant stable en conservant une valeur de remplacement explicite.
    /// </summary>
    private static string NormalizeIdPart(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "NA"
            : value.Trim();
    }

    /// <summary>
    /// Normalise une chaîne optionnelle pour éviter d'exposer des valeurs vides dans le contrat JSON.
    /// </summary>
    private static string? NormalizeNullable(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}
