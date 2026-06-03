using System.Globalization;
using API_ASP.NET_Core.Models;

namespace API_ASP.NET_Core.Mappers;

/// <summary>
/// Mapper dédié au module Expédition.
/// Convertit les DTO publics en modèles internes utilisés par les services et repositories.
/// </summary>
public sealed class ExpeditionMapper
{
    /// <summary>
    /// Convertit le DTO public de verrouillage Expédition en commande interne.
    /// La validation du payload doit avoir été effectuée avant l'appel à cette méthode.
    /// </summary>
    public ExpeditionVerrouillageLotCommand ToVerrouillageCommand(ExpeditionVerrouillageLotRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new ExpeditionVerrouillageLotCommand
        {
            SchemaVersion = request.SchemaVersion,
            IdLotVerrouillage = request.IdLotVerrouillage,
            Source = request.Source,
            DateTournee = DateOnly.FromDateTime(DateTime.Parse(request.DateTournee, CultureInfo.InvariantCulture).Date),
            DateTourneeTexte = request.DateTournee,
            DateVerrouillageDemandee = request.DateVerrouillageDemandee,
            FuseauHoraireMetier = request.FuseauHoraireMetier,
            Tournees = (request.Tournees ?? new List<ExpeditionVerrouillageTourneeRequest>())
                .Select(ToVerrouillageTourneeCommand)
                .ToList()
        };
    }

    private static ExpeditionVerrouillageTourneeCommand ToVerrouillageTourneeCommand(
        ExpeditionVerrouillageTourneeRequest tournee)
    {
        return new ExpeditionVerrouillageTourneeCommand
        {
            CodeTournee = tournee.CodeTournee,
            LibelleTournee = tournee.LibelleTournee,
            StatutPreparationWeb = tournee.StatutPreparationWeb,
            DateModification = tournee.DateModification,
            Lignes = (tournee.Lignes ?? new List<ExpeditionVerrouillageLigneRequest>())
                .Select(ToVerrouillageLigneCommand)
                .ToList()
        };
    }

    private static ExpeditionVerrouillageLigneCommand ToVerrouillageLigneCommand(
        ExpeditionVerrouillageLigneRequest ligne)
    {
        return new ExpeditionVerrouillageLigneCommand
        {
            IdLigneSource = ligne.IdLigneSource,
            OrdreArret = ligne.OrdreArret,
            Client = ToVerrouillageClientCommand(ligne.Client),
            PointLivraison = ToVerrouillagePointLivraisonCommand(ligne.PointLivraison),
            CommentaireExceptionnel = ligne.CommentaireExceptionnel,
            DerniereModification = ToVerrouillageDerniereModificationCommand(ligne.DerniereModification),
            QuantitesPrevues = (ligne.QuantitesPrevues ?? new List<ExpeditionQuantitePrevueRequest>())
                .Select(ToVerrouillageQuantiteCommand)
                .ToList()
        };
    }

    private static ExpeditionVerrouillageClientCommand ToVerrouillageClientCommand(ExpeditionClientDto? client)
    {
        if (client is null)
        {
            return new ExpeditionVerrouillageClientCommand();
        }

        return new ExpeditionVerrouillageClientCommand
        {
            NumClient = client.NumClient,
            NomClient = client.NomClient,
            NomAffiche = client.NomAffiche
        };
    }

    private static ExpeditionVerrouillagePointLivraisonCommand ToVerrouillagePointLivraisonCommand(
        ExpeditionPointLivraisonDto? pointLivraison)
    {
        if (pointLivraison is null)
        {
            return new ExpeditionVerrouillagePointLivraisonCommand();
        }

        return new ExpeditionVerrouillagePointLivraisonCommand
        {
            CodePDL = pointLivraison.CodePDL,
            DescriptionPDL = pointLivraison.DescriptionPDL,
            AdresseLigne1 = pointLivraison.AdresseLigne1,
            AdresseLigne2 = pointLivraison.AdresseLigne2,
            AdresseLigne3 = pointLivraison.AdresseLigne3,
            Ville = pointLivraison.Ville,
            CodePostal = pointLivraison.CodePostal
        };
    }

    private static ExpeditionVerrouillageDerniereModificationCommand? ToVerrouillageDerniereModificationCommand(
        ExpeditionDerniereModificationDto? derniereModification)
    {
        if (derniereModification is null)
        {
            return null;
        }

        return new ExpeditionVerrouillageDerniereModificationCommand
        {
            Date = derniereModification.Date,
            Utilisateur = derniereModification.Utilisateur
        };
    }

    private static ExpeditionVerrouillageQuantiteCommand ToVerrouillageQuantiteCommand(
        ExpeditionQuantitePrevueRequest quantite)
    {
        return new ExpeditionVerrouillageQuantiteCommand
        {
            CodeArticle = quantite.CodeArticle,
            QuantiteLivreePrevue = quantite.QuantiteLivreePrevue
        };
    }
}
