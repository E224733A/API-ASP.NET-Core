namespace API_ASP.NET_Core.Models;

/// <summary>
/// Ligne de tournée envoyée par l'application mobile lors de la synchronisation.
/// </summary>
/// <remarks>
/// Une ligne correspond à un arrêt de tournée, généralement associé à un client
/// et à un point de livraison.
/// </remarks>
public class SynchronisationLigneRequest
{
    /// <summary>
    /// Identifiant métier stable de la ligne source.
    /// </summary>
    /// <remarks>
    /// Cet identifiant permet de retrouver précisément la ligne chargée le matin
    /// et renvoyée le soir par le mobile.
    ///
    /// Il doit être unique dans la requête.
    ///
    /// Exemple : 2026-04-28|2001|2|1058|1|1
    /// </remarks>
    public string IdLigneSource { get; set; } = string.Empty;

    /// <summary>
    /// Ordre de passage dans la tournée.
    /// </summary>
    /// <remarks>
    /// Doit être positif ou nul.
    ///
    /// Exemple : 1
    /// </remarks>
    public int OrdreArret { get; set; }

    /// <summary>
    /// Informations du client concerné par cette ligne.
    /// </summary>
    public SynchronisationClientRequest? Client { get; set; }

    /// <summary>
    /// Informations du point de livraison concerné par cette ligne.
    /// </summary>
    public SynchronisationPointLivraisonRequest? PointLivraison { get; set; }

    /// <summary>
    /// Données saisies ou validées par le livreur pour cette ligne.
    /// </summary>
    public SynchronisationSaisieRequest? Saisie { get; set; }
}