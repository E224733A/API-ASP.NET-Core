namespace API_ASP.NET_Core.Models;

/// <summary>
/// Ligne de tournée envoyée par l'application mobile lors de la synchronisation.
/// </summary>
/// <remarks>
/// Une ligne correspond à un arrêt de tournée, généralement associé à un client
/// et à un point de livraison.
/// Le contrat v1.2 permet aussi de renvoyer le snapshot des informations chargées le matin
/// afin de conserver en base l'état exact présenté au livreur.
/// </remarks>
public class SynchronisationLigneRequest
{
    /// <summary>
    /// Identifiant métier stable de la ligne source.
    /// Exemple : 2026-05-07|4006|4|1058|1|1.
    /// </summary>
    public string IdLigneSource { get; set; } = string.Empty;

    /// <summary>
    /// Ordre de passage dans la tournée.
    /// Doit être positif ou nul.
    /// </summary>
    public int OrdreArret { get; set; }

    /// <summary>
    /// Horaire ou ordre horaire renvoyé par le mobile, lorsqu'il existe.
    /// </summary>
    public int? Horaire { get; set; }

    /// <summary>
    /// Informations du client concerné par cette ligne.
    /// </summary>
    public SynchronisationClientRequest? Client { get; set; }

    /// <summary>
    /// Informations du point de livraison concerné par cette ligne.
    /// </summary>
    public SynchronisationPointLivraisonRequest? PointLivraison { get; set; }

    /// <summary>
    /// Snapshot des informations de tournée principale chargées le matin.
    /// </summary>
    public TourneeInfoDto? Tournee { get; set; }

    /// <summary>
    /// Snapshot des informations de tournée retour chargées le matin.
    /// </summary>
    public RetourInfoDto? Retour { get; set; }

    /// <summary>
    /// Snapshot des informations utiles au livreur chargées le matin.
    /// </summary>
    public InfosLivreurDto? InfosLivreur { get; set; }

    /// <summary>
    /// Données saisies ou validées par le livreur pour cette ligne.
    /// </summary>
    public SynchronisationSaisieRequest? Saisie { get; set; }
}
