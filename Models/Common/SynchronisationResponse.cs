namespace API_ASP.NET_Core.Models;

/// <summary>
/// Réponse simple retournée après une opération de synchronisation.
/// </summary>
/// <remarks>
/// Ce modèle peut être utilisé pour indiquer si une opération a réussi
/// ou pour retourner un message lisible.
/// </remarks>
public class SynchronisationResponse
{
    /// <summary>
    /// Statut de la réponse.
    /// </summary>
    /// <remarks>
    /// Exemple : SUCCESS
    /// </remarks>
    public string Statut { get; set; } = string.Empty;

    /// <summary>
    /// Message lisible décrivant le résultat de l'opération.
    /// </summary>
    /// <remarks>
    /// Exemple : Synchronisation enregistrée avec succès.
    /// </remarks>
    public string Message { get; set; } = string.Empty;
}