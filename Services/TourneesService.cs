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
        var lignes = (await _repository.GetTourneeLinesAsync(dateTournee, codeLivreur, codeTournee)).ToList();

        if (!lignes.Any())
        {
            return null;
        }

        // On récupère les valeurs communes depuis la première ligne.
        var first = lignes.First();

        // Construction des lignes avant la création finale du DTO.
        // Cela permet de conserver les propriétés en init dans TourneeMobileDto.
        var lignesDto = new List<TourneeLigneMobileDto>();

        foreach (var rec in lignes)
        {
            // idLigneSource = codeTournee-jourTournee-numClient-codePDL-ordreArret
            // Cet identifiant est utilisé par le mobile pour reconnaître une ligne hors connexion.
            var idLigne = $"{rec.CodeTournee}-{rec.JourTournee}-{rec.NumClient}-{rec.CodePDL}-{rec.OrdreArret}";

            var ligneDto = new TourneeLigneMobileDto
            {
                IdLigneSource = idLigne,
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
                    DateFermeture = rec.DateFermeture != null
                        ? DateOnly.FromDateTime(rec.DateFermeture.Value)
                        : null,
                    MotifFermeture = rec.MotifFermeture
                },

                // Au chargement du matin, la saisie est initialisée à zéro.
                // Le livreur la modifiera ensuite côté application mobile.
                Saisie = new SaisieDto()
            };

            lignesDto.Add(ligneDto);
        }

        var tourneeDto = new TourneeMobileDto
        {
            DateTournee = dateTournee.ToString("yyyy-MM-dd"),
            JourTournee = first.JourTournee,
            JourLibelle = GetJourLibelle(first.JourTournee),
            CodeTournee = first.CodeTournee,
            LibelleTournee = first.LibelleTournee,
            StatutSynchronisation = "NON_ENVOYEE",

            Livreur = new LivreurDto
            {
                CodeLivreur = codeLivreur,
                NomLivreur = nomLivreur ?? "Inconnu"
            },

            Chargement = new ChargementDto
            {
                DateChargement = DateTime.Now,
                NombrePointsEnvoyes = lignesDto.Count
            },

            Lignes = lignesDto
        };

        return tourneeDto;
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