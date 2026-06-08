using API_ASP.NET_Core.Application.Mobile;
using API_ASP.NET_Core.Constants;
using API_ASP.NET_Core.Models;
using API_ASP.NET_Core.Repositories;

namespace API_ASP.NET_Core.Mappers;

public sealed class TourneeMobileMapper
{
    public TourneeMobileDto Map(
        DateOnly dateTournee,
        LivreurRecord livreur,
        IReadOnlyList<TourneeLigneRecord> lignes,
        IReadOnlyList<ArticleSaisissableRecord> articlesSaisissables,
        IReadOnlyList<CommentaireExceptionnelRecord> commentairesExceptionnels,
        IReadOnlyList<PreRemplissageQuantiteRecord> preRemplissages,
        IReadOnlyDictionary<string, AdresseLivraisonInfo?>? adressesLivraisonParCodePdl = null)
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
                adressesLivraisonParCodePdl))
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

    private static TourneeLigneMobileDto MapLigne(
        DateOnly dateTournee,
        TourneeLigneRecord ligne,
        IReadOnlyList<ArticleSaisissableDto> articlesSaisissables,
        IReadOnlyList<CommentaireExceptionnelRecord> commentairesExceptionnels,
        IReadOnlyList<PreRemplissageQuantiteRecord> preRemplissages,
        IReadOnlyDictionary<string, AdresseLivraisonInfo?>? adressesLivraisonParCodePdl)
    {
        var idLigneSource = BuildIdLigneSource(dateTournee, ligne);
        var commentaireExceptionnel = FindCommentaireExceptionnel(idLigneSource, ligne, commentairesExceptionnels);
        var zoneDechargement = NormalizeNullable(ligne.ZoneDechargement);
        var adresseLivraison = FindAdresseLivraison(ligne.CodePDL, adressesLivraisonParCodePdl);

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
                LatitudeLivraison = adresseLivraison?.LatitudeLivraison,
                LongitudeLivraison = adresseLivraison?.LongitudeLivraison,
                LienAdresseLivraison = adresseLivraison?.LienAdresseLivraison
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

    private static AdresseLivraisonInfo? FindAdresseLivraison(
        string? codePdl,
        IReadOnlyDictionary<string, AdresseLivraisonInfo?>? adressesLivraisonParCodePdl)
    {
        if (string.IsNullOrWhiteSpace(codePdl) || adressesLivraisonParCodePdl is null)
        {
            return null;
        }

        return adressesLivraisonParCodePdl.TryGetValue(codePdl.Trim(), out var info)
            ? info
            : null;
    }

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
