namespace API_ASP.NET_Core.Models;

/// <summary>
/// Informations du client associées à une ligne de synchronisation.
/// </summary>
public class SynchronisationClientRequest
{
    /// <summary>
    /// Numéro du client.
    /// </summary>
    /// <remarks>
    /// Exemple : 1058
    /// </remarks>
    public string NumClient { get; set; } = string.Empty;

    /// <summary>
    /// Nom administratif ou métier du client.
    /// </summary>
    /// <remarks>
    /// Exemple : EHPAD L EQUAIZIERE
    /// </remarks>
    public string NomClient { get; set; } = string.Empty;

    /// <summary>
    /// Nom affiché dans l'application mobile.
    /// </summary>
    /// <remarks>
    /// Ce champ peut être utilisé pour afficher un nom plus lisible ou plus adapté au livreur.
    ///
    /// Exemple : EHPAD EQUAIZIERE GARNACHE
    /// </remarks>
    public string NomAffiche { get; set; } = string.Empty;
}