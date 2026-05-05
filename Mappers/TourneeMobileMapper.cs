using API_ASP.NET_Core.Constants;
using API_ASP.NET_Core.Models;
using API_ASP.NET_Core.Repositories;

namespace API_ASP.NET_Core.Mappers;

public sealed class TourneeMobileMapper
{
    public TourneeMobileDto Map(
        DateOnly dateTournee,
        LivreurRecord livreur,
        IReadOnlyList<TourneeLigneRecord> lignes)
    {
        if (lignes.Count == 0)
        {
            throw new ArgumentException("Impossible de mapper une tournée sans lignes.", nameof(lignes));
        }

        var premiereLigne = lignes[0];
        var articlesSaisissables = BuildArticlesSaisissables();

        var lignesDto = lignes
            .Select(ligne => MapLigne(dateTournee, ligne, articlesSaisissables))
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

            ArticlesSaisissables = articlesSaisissables,
            Lignes = lignesDto
        };
    }

    private static TourneeLigneMobileDto MapLigne(
        DateOnly dateTournee,
        TourneeLigneRecord ligne,
        IReadOnlyList<ArticleSaisissableDto> articlesSaisissables)
    {
        return new TourneeLigneMobileDto
        {
            IdLigneSource = BuildIdLigneSource(dateTournee, ligne),
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
                CodePostal = ligne.CodePostal
            },

            Tournee = new TourneeInfoDto
            {
                CodeTournee = ligne.CodeTournee,
                LibelleTournee = ligne.LibelleTournee,
                JourTournee = ligne.JourTournee,
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
                CommentaireFiche = NormalizeNullable(ligne.CommentaireFiche),
                ZoneDechargement = NormalizeNullable(ligne.ZoneDechargement),
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
                    .Select(article => new QuantiteSaisieMobileDto
                    {
                        CodeArticle = article.CodeArticle,
                        Libelle = article.Libelle,
                        QuantiteLivree = 0,
                        QuantiteRecuperee = 0
                    })
                    .ToList()
            }
        };
    }

    private static List<ArticleSaisissableDto> BuildArticlesSaisissables()
    {
        return ArticlesSaisissables.ActifsV1
            .Select(article => new ArticleSaisissableDto
            {
                CodeArticle = article.CodeArticle,
                Libelle = article.Libelle
            })
            .ToList();
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