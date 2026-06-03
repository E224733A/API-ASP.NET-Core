namespace API_ASP.NET_Core.Models;

/// <summary>
/// Informations du point de livraison associé à une ligne de synchronisation.
/// </summary>
public class SynchronisationPointLivraisonRequest
{
    /// <summary>
    /// Code du point de livraison.
    /// Exemple : 1.
    /// </summary>
    public string CodePDL { get; set; } = string.Empty;

    /// <summary>
    /// Description lisible du point de livraison.
    /// Exemple : EHPAD EQUAIZIERE GARNACHE.
    /// </summary>
    public string DescriptionPDL { get; set; } = string.Empty;

    /// <summary>
    /// Première ligne d'adresse du point de livraison.
    /// </summary>
    public string? AdresseLigne1 { get; set; }

    /// <summary>
    /// Deuxième ligne d'adresse du point de livraison.
    /// </summary>
    public string? AdresseLigne2 { get; set; }

    /// <summary>
    /// Troisième ligne d'adresse du point de livraison.
    /// </summary>
    public string? AdresseLigne3 { get; set; }

    /// <summary>
    /// Ville du point de livraison.
    /// </summary>
    public string? Ville { get; set; }

    /// <summary>
    /// Code postal du point de livraison.
    /// </summary>
    public string? CodePostal { get; set; }
}
