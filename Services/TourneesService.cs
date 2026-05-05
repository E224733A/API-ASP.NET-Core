using API_ASP.NET_Core.Models;
using API_ASP.NET_Core.Repositories;

namespace API_ASP.NET_Core.Services;

public class TourneesService
{
    private readonly TourneesRepository _repository;

    public TourneesService(TourneesRepository repository)
    {
        _repository = repository;
    }

    public async Task<TourneeMobileDto?> GetTourneeAsync(
        DateOnly dateTournee,
        string codeLivreur,
        string? codeTournee = null,
        string? nomLivreur = null)
    {
        if (string.IsNullOrWhiteSpace(codeLivreur))
        {
            return null;
        }

        var livreur = await _repository.GetLivreurAsync(codeLivreur);

        if (livreur is null)
        {
            return null;
        }

        var lignes = (await _repository.GetTourneeLinesAsync(
            dateTournee,
            codeLivreur,
            codeTournee)).ToList();

        if (lignes.Count == 0)
        {
            return null;
        }

        var first = lignes.First();

        var articlesSaisissables = GetArticlesSaisissables();

        var lignesDto = lignes
            .Select(rec => BuildLigneDto(dateTournee, rec, articlesSaisissables))
            .ToList();

        return new TourneeMobileDto
        {
            SchemaVersion = "1.1",
            DateTournee = dateTournee.ToString("yyyy-MM-dd"),
            DateModifiable = false,

            JourTournee = first.JourTournee,
            JourLibelle = GetJourLibelle(first.JourTournee),

            CodeTournee = first.CodeTournee,
            LibelleTournee = first.LibelleTournee,

            StatutSynchronisation = "NON_ENVOYEE",

            Livreur = new LivreurDto
            {
                CodeLivreur = livreur.CodeLivreur,
                NomLivreur = !string.IsNullOrWhiteSpace(livreur.NomLivreur)
                    ? livreur.NomLivreur
                    : nomLivreur ?? "Inconnu"
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

    private static TourneeLigneMobileDto BuildLigneDto(
        DateOnly dateTournee,
        TourneeLigneRecord rec,
        IList<ArticleSaisissableDto> articlesSaisissables)
    {
        var idLigneSource = BuildIdLigneSource(dateTournee, rec);

        return new TourneeLigneMobileDto
        {
            IdLigneSource = idLigneSource,

            OrdreArret = rec.OrdreArret,
            Horaire = rec.Horaire,

            Client = new ClientDto
            {
                NumClient = rec.NumClient,
                NomClient = rec.NomClient,
                NomAffiche = rec.NomAffiche
            },

            PointLivraison = new PointLivraisonDto
            {
                CodePDL = rec.CodePDL,
                DescriptionPDL = rec.DescriptionPDL,
                AdresseLigne1 = rec.AdresseLigne1,
                AdresseLigne2 = rec.AdresseLigne2,
                AdresseLigne3 = rec.AdresseLigne3,
                Ville = rec.Ville,
                CodePostal = rec.CodePostal
            },

            Tournee = new TourneeInfoDto
            {
                CodeTournee = rec.CodeTournee,
                LibelleTournee = rec.LibelleTournee,
                JourTournee = rec.JourTournee,
                SchemaLivraison = rec.SchemaLivraison
            },

            Retour = new RetourInfoDto
            {
                JourTourneeRetour = rec.JourTourneeRetour,
                JourRetourLibelle = GetJourLibelle(rec.JourTourneeRetour),
                CodeTourneeRetour = rec.CodeTourneeRetour,
                LibelleTourneeRetour = rec.LibelleTourneeRetour
            },

            InfosLivreur = new InfosLivreurDto
            {
                Instructions = rec.Instructions,
                CommentaireFiche = rec.CommentaireFiche,
                ZoneDechargement = rec.ZoneDechargement,
                Zone = rec.Zone,
                Precision = rec.Precision,
                Cle = rec.Cle,
                EstFerme = rec.EstFerme,
                DateFermeture = rec.DateFermeture.HasValue
                    ? DateOnly.FromDateTime(rec.DateFermeture.Value)
                    : null,
                MotifFermeture = rec.MotifFermeture
            },

            Saisie = new SaisieMobileDto
            {
                PrecisionLivreur = null,
                StatutPassage = "A_FAIRE",
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

    private static string BuildIdLigneSource(DateOnly dateTournee, TourneeLigneRecord rec)
    {
        var date = dateTournee.ToString("yyyy-MM-dd");
        var codeTournee = NormalizePart(rec.CodeTournee);
        var jour = rec.JourTournee?.ToString() ?? "0";
        var numClient = NormalizePart(rec.NumClient);
        var codePdl = NormalizePart(rec.CodePDL);
        var ordreArret = rec.OrdreArret?.ToString() ?? "0";

        return $"{date}|{codeTournee}|{jour}|{numClient}|{codePdl}|{ordreArret}";
    }

    private static string NormalizePart(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "NA"
            : value.Trim();
    }

    private static List<ArticleSaisissableDto> GetArticlesSaisissables()
    {
        /*
         * Pour la V1, la liste est définie côté API.
         * Elle pourra ensuite être alimentée depuis une vue ABSSolute
         * si une vue exploitable est confirmée pour les articles.
         */
        return new List<ArticleSaisissableDto>
        {
            new()
            {
                CodeArticle = "ROLLS",
                Libelle = "Rolls"
            },
            new()
            {
                CodeArticle = "TAPIS",
                Libelle = "Tapis"
            },
            new()
            {
                CodeArticle = "SACS",
                Libelle = "Sacs"
            }
        };
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