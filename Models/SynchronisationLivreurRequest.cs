namespace API_ASP.NET_Core.Models;

/// <summary>
/// Informations du livreur associé à la synchronisation mobile.
/// </summary>
public class SynchronisationLivreurRequest
{
    /// <summary>
    /// Code du livreur.
    /// </summary>
    /// <remarks>
    /// Ce code identifie le livreur dans les données métier.
    ///
    /// Exemple : 2
    /// </remarks>
    public string CodeLivreur { get; set; } = string.Empty;

    /// <summary>
    /// Nom du livreur.
    /// </summary>
    /// <remarks>
    /// Exemple : DAVID LEBAS
    /// </remarks>
    public string NomLivreur { get; set; } = string.Empty;
}