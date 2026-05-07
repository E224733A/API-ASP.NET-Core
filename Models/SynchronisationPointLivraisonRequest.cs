namespace API_ASP.NET_Core.Models;

/// <summary>
/// Informations du point de livraison associé à une ligne de synchronisation.
/// </summary>
public class SynchronisationPointLivraisonRequest
{
    /// <summary>
    /// Code du point de livraison.
    /// </summary>
    /// <remarks>
    /// Ce code identifie le point de livraison du client.
    ///
    /// Exemple : 1
    /// </remarks>
    public string CodePDL { get; set; } = string.Empty;

    /// <summary>
    /// Description lisible du point de livraison.
    /// </summary>
    /// <remarks>
    /// Exemple : EHPAD EQUAIZIERE GARNACHE
    /// </remarks>
    public string DescriptionPDL { get; set; } = string.Empty;
}