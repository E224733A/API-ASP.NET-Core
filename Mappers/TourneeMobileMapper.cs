using API_ASP.NET_Core.Constants;
using API_ASP.NET_Core.Models;
using API_ASP.NET_Core.Repositories;

namespace API_ASP.NET_Core.Mappers;

/// <summary>
/// Mapper chargé de construire le contrat JSON de chargement mobile à partir des données SQL.
/// </summary>
/// <remarks>
/// Ce mapper assemble les vues métier ABSSolute, les commentaires exceptionnels,
/// les préremplissages Expédition et les liens d'adresse de livraison. Il ne charge pas
/// les données lui-même : il transforme les records fournis par le service en DTO mobiles.
/// </remarks>
public sealed class TourneeMobileMapper
{
    /// <summary>
    /// Construit la réponse complète de tournée envoyée au mobile le matin.
    /// </summary>
    /// <remarks>
    /// La réponse reste en schemaVersion 1.2 côté chargement. Les lignes sont initialisées
    /// avec le statut A_FAIRE et les quantités préremplies issues de l'Expédition si elles existent.
    /// </remarks>
    public TourneeMobileDto Map(
        DateOnly dateTournee,
        LivreurRecord livreur,
        IReadOnlyList<TourneeLigneRecord> lignes,
        IReadOnlyList<ArticleSaisissableRecord> articlesSaisissables,
        IReadOnlyList<CommentaireExceptionnelRecord> commentairesExceptionnels,
        IReadOnlyList<PreRemplissageQuantiteRecord> preRemplissages,
        IReadOnlyDictionary<string, string?>? liensAdresseLivraisonParClientEtPdl = null)
    {
        if (lignes.Count == 0)
        {
            throw new ArgumentException("Impossible de mapper une tournée sans lignes.", nameof(lignes));
        }

        var premiereLigne = lignes[0];
        var articles = BuildArticlesSaisissables(articlesSaisissables);

        var lignesDto = lignes
            .Select(ligne => MapLigne(
                dateTournee,
                ligne,
                articles,
                commentairesExceptionnels,
                preRemplissages,
                liensAdresseLivraisonParClientEtPdl))
            .ToList();

        return new TourneeMobileDto
        {
            SchemaVersion = SchemaVersions.SynchronisationActuelle,
            DateTournee = dateTournee.ToString("yyyy-MM-dd"),
            DateModifiable = false,
            JourTournee = premiereLigne.JourTournee,
            JourLibelle = GetJourLibelle(premiereLigne.JourTournee),
            CodeTournee = premiereLigne.CodeTournee,
            LibelleTournee = premiereLigne.LibelleTournee,
            StatutSynchronisation = "NON_ENVOYEE",
            Livreur = new LivreurDto
            {
                CodeLivreur = livreur.CodeLivreur,
                NomLivreur = string.IsNullOrWhiteSpace(livreur.NomLivreur)
                    ? "Inconnu"
                    : livreur.NomLivreur
            },
            Chargement = new ChargementDto
            {
                DateGenerationApi = DateTimeOffset.Now,
                NombrePointsEnvoyes = lignesDto.Count
            },
            ArticlesSaisissables = articles,
            Lignes = lignesDto
        };
    }

    /// <summary>
    /// Convertit une ligne SQL de tournée en ligne mobile complète.
    /// </summary>
    /// <remarks>
    /// Cette méthode ajoute les informations utiles au livreur : client, point de livraison,
    /// retour, instructions, commentaire exceptionnel, zone de déchargement et saisie initiale.
    /// </remarks>
    private static TourneeLigneMobileDto MapLigne(
        DateOnly dateTournee,
        TourneeLigneRecord ligne,
        IReadOnlyList<ArticleSaisissableDto> articlesSaisissables,
        IReadOnlyList<CommentaireExceptionnelRecord> commentairesExceptionnels,
        IReadOnlyList<PreRemplissageQuantiteRecord> preRemplissages,
        IReadOnlyDictionary<string, string?>? liensAdresseLivraisonParClientEtPdl)
    {
        var idLigneSource = BuildIdLigneSource(dateTournee, ligne);
        var commentaireExceptionnel = FindCommentaireExceptionnel(idLigneSource, ligne, commentairesExceptionnels);
        var zoneDechargement = NormalizeNullable(ligne.ZoneDechargement);

        return new TourneeLigneMobileDto
        {
            IdLigneSource = idLigneSource,
            OrdreArret = ligne.OrdreArret,
            Horaire = ligne.Horaire,
            Client = new ClientDto
            {
                NumClient = ligne.NumClient,
                NomClient = ligne.NomClient,
                NomAffiche = ligne.NomAffiche
            },
            PointLivraison = new PointLivraisonDto
            {
                CodePDL = ligne.CodePDL,
                DescriptionPDL = ligne.DescriptionPDL,
                AdresseLigne1 = ligne.AdresseLigne1,
                AdresseLigne2 = ligne.AdresseLigne2,
                AdresseLigne3 = ligne.AdresseLigne3,
                Ville = ligne.Ville,
                CodePostal = ligne.CodePostal,
                LienAdresseLivraison = FindLienAdresseLivraison(
                    ligne.NumClient,
                    ligne.CodePDL,
                    liensAdresseLivraisonParClientEtPdl)
            },
            Tournee = new TourneeInfoDto
            {
                CodeTournee = ligne.CodeTournee,
                LibelleTournee = ligne.LibelleTournee,
                JourTournee = ligne.JourTournee,
                JourLibelle = GetJourLibelle(ligne.JourTournee),
                SchemaLivraison = ligne.SchemaLivraison
            },
            Retour = new RetourInfoDto
            {
                JourTourneeRetour = ligne.JourTourneeRetour,
                JourRetourLibelle = GetJourLibelle(ligne.JourTourneeRetour),
                CodeTourneeRetour = ligne.CodeTourneeRetour,
                LibelleTourneeRetour = ligne.LibelleTourneeRetour
            },
            InfosLivreur = new InfosLivreurDto
            {
                Instructions = NormalizeNullable(ligne.Instructions),
                CommentaireExceptionnel = commentaireExceptionnel,
                ZoneDechargement = zoneDechargement,
                ZoneDechargementAffichee = BuildZoneDechargementAffichee(
                    ligne.JourTourneeRetour,
                    zoneDechargement),
                Zone = NormalizeNullable(ligne.Zone),
                Precision = NormalizeNullable(ligne.Precision),
                Cle = NormalizeNullable(ligne.Cle),
                EstFerme = ligne.EstFerme,
                DateFermeture = ligne.DateFermeture.HasValue
                    ? DateOnly.FromDateTime(ligne.DateFermeture.Value)
                    : null,
                MotifFermeture = NormalizeNullable(ligne.MotifFermeture)
            },
            Saisie = new SaisieMobileDto
            {
                PrecisionLivreur = null,
                StatutPassage = StatutsPassage.AFaire,
                CommentaireLivreur = null,
                HeureValidation = null,
                EstValidee = false,
                Quantites = articlesSaisissables
                    .Select(article => MapQuantiteInitiale(article, ligne, idLigneSource, preRemplissages))
                    .ToList()
            }
        };
    }

    /// <summary>
    /// Recherche le lien d'adresse de livraison correspondant au client et au point de livraison.
    /// </summary>
    private static string? FindLienAdresseLivraison(
        string? numClient,
        string? codePdl,
        IReadOnlyDictionary<string, string?>? liensAdresseLivraisonParClientEtPdl)
    {
        if (string.IsNullOrWhiteSpace(numClient)
            || string.IsNullOrWhiteSpace(codePdl)
            || liensAdresseLivraisonParClientEtPdl is null)
        {
            return null;
        }

        var key = BuildAdresseLivraisonKey(numClient, codePdl);
        return liensAdresseLivraisonParClientEtPdl.TryGetValue(key, out var lien)
            ? NormalizeNullable(lien)
            : null;
    }

    /// <summary>
    /// Construit la clé de recherche du lien d'adresse de livraison.
    /// </summary>
    private static string BuildAdresseLivraisonKey(string? numClient, string? codePdl)
    {
        return $"{NormalizeKeyPart(numClient)}|{NormalizeKeyPart(codePdl)}";
    }

    /// <summary>
    /// Normalise une partie de clé sans transformer une absence de valeur en NULL.
    /// </summary>
    private static string NormalizeKeyPart(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim();
    }

    /// <summary>
    /// Initialise une quantité mobile avec le préremplissage Expédition lorsqu'il existe.
    /// </summary>
    /// <remarks>
    /// La quantité livrée démarre avec la quantité prévue pour faciliter la saisie livreur,
    /// tandis que la quantité récupérée reste à 0 au chargement.
    /// </remarks>
    private static QuantiteSaisieMobileDto MapQuantiteInitiale(
        ArticleSaisissableDto article,
        TourneeLigneRecord ligne,
        string idLigneSource,
        IReadOnlyList<PreRemplissageQuantiteRecord> preRemplissages)
    {
        var preRemplissage = FindPreRemplissage(
            article.CodeArticle,
            ligne,
            idLigneSource,
            preRemplissages);

        return new QuantiteSaisieMobileDto
        {
            CodeArticle = article.CodeArticle,
            Libelle = preRemplissage?.LibelleArticle ?? article.Libelle,
            QuantiteLivreePrevue = preRemplissage?.QuantiteLivreePrevue,
            QuantiteLivree = preRemplissage?.QuantiteLivreePrevue ?? 0,
            QuantiteRecuperee = 0
        };
    }

    /// <summary>
    /// Retrouve la quantité prévue Expédition à appliquer à une ligne mobile.
    /// </summary>
    /// <remarks>
    /// La recherche privilégie l'identifiant stable idLigneSource. Le repli NumClient + CodePDL
    /// conserve la compatibilité avec les préremplissages qui ne portent pas encore cet identifiant.
    /// </remarks>
    private static PreRemplissageQuantiteRecord? FindPreRemplissage(
        string codeArticle,
        TourneeLigneRecord ligne,
        string idLigneSource,
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
    /// Recherche le commentaire exceptionnel à afficher au livreur pour une ligne.
    /// </summary>
    /// <remarks>
    /// La priorité est donnée au commentaire rattaché à idLigneSource, puis au couple
    /// NumClient + CodePDL, puis au commentaire client global si aucun PDL n'est précisé.
    /// </remarks>
    private static string? FindCommentaireExceptionnel(
        string idLigneSource,
        TourneeLigneRecord ligne,
        IReadOnlyList<CommentaireExceptionnelRecord> commentairesExceptionnels)
    {
        var commentaireLigne = commentairesExceptionnels.FirstOrDefault(commentaire =>
            !string.IsNullOrWhiteSpace(commentaire.IdLigneSource)
            && string.Equals(commentaire.IdLigneSource, idLigneSource, StringComparison.OrdinalIgnoreCase));

        if (commentaireLigne is not null)
        {
            return NormalizeNullable(commentaireLigne.Commentaire);
        }

        var commentaireExact = commentairesExceptionnels.FirstOrDefault(commentaire =>
            string.Equals(commentaire.NumClient, ligne.NumClient, StringComparison.OrdinalIgnoreCase)
            && string.Equals(NormalizeIdPart(commentaire.CodePDL), NormalizeIdPart(ligne.CodePDL), StringComparison.OrdinalIgnoreCase));

        if (commentaireExact is not null)
        {
            return NormalizeNullable(commentaireExact.Commentaire);
        }

        var commentaireClient = commentairesExceptionnels.FirstOrDefault(commentaire =>
            string.Equals(commentaire.NumClient, ligne.NumClient, StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(commentaire.CodePDL));

        return NormalizeNullable(commentaireClient?.Commentaire);
    }

    /// <summary>
    /// Construit la liste des articles saisissables envoyés au mobile.
    /// </summary>
    /// <remarks>
    /// Si le référentiel SQL est vide, le mapper utilise le référentiel applicatif v1
    /// pour conserver un chargement mobile exploitable.
    /// </remarks>
    private static List<ArticleSaisissableDto> BuildArticlesSaisissables(
        IReadOnlyList<ArticleSaisissableRecord> articlesSaisissables)
    {
        if (articlesSaisissables.Count > 0)
        {
            return articlesSaisissables
                .OrderBy(article => article.OrdreAffichage)
                .ThenBy(article => article.CodeArticle)
                .Select(article => new ArticleSaisissableDto
                {
                    CodeArticle = article.CodeArticle.Trim().ToUpperInvariant(),
                    Libelle = article.LibelleArticle
                })
                .ToList();
        }

        return ArticlesSaisissables.ActifsV1
            .Select(article => new ArticleSaisissableDto
            {
                CodeArticle = article.CodeArticle,
                Libelle = article.Libelle
            })
            .ToList();
    }

    /// <summary>
    /// Construit l'identifiant stable que le mobile doit renvoyer lors du POST final.
    /// </summary>
    /// <remarks>
    /// L'identifiant combine date, tournée, client, point de livraison et ordre d'arrêt.
    /// Il sert aussi à rattacher les commentaires exceptionnels et les préremplissages.
    /// </remarks>
    public static string BuildIdLigneSource(DateOnly dateTournee, TourneeLigneRecord ligne)
    {
        var date = dateTournee.ToString("yyyy-MM-dd");
        var codeTournee = NormalizeIdPart(ligne.CodeTournee);
        var jour = ligne.JourTournee?.ToString() ?? "0";
        var numClient = NormalizeIdPart(ligne.NumClient);
        var codePdl = NormalizeIdPart(ligne.CodePDL);
        var ordreArret = ligne.OrdreArret?.ToString() ?? "0";

        return $"{date}|{codeTournee}|{jour}|{numClient}|{codePdl}|{ordreArret}";
    }

    /// <summary>
    /// Construit la zone de déchargement affichée au livreur.
    /// </summary>
    /// <remarks>
    /// Une zone commençant par + est affichée avec le jour de retour afin de conserver
    /// l'information métier utile au déchargement.
    /// </remarks>
    private static string? BuildZoneDechargementAffichee(
        int? jourTourneeRetour,
        string? zoneDechargement)
    {
        var jourRetour = jourTourneeRetour?.ToString();
        var zone = NormalizeNullable(zoneDechargement);

        if (zone is null)
        {
            return jourRetour;
        }

        if (zone.StartsWith("+", StringComparison.Ordinal))
        {
            return string.IsNullOrWhiteSpace(jourRetour)
                ? zone.Trim()
                : $"{jourRetour} {zone.Trim()}";
        }

        return zone;
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
    /// Normalise une chaîne optionnelle pour le contrat mobile.
    /// </summary>
    private static string? NormalizeNullable(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    /// <summary>
    /// Convertit le numéro de jour métier en libellé lisible pour le mobile.
    /// </summary>
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
}
