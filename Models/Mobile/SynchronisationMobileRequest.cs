namespace API_ASP.NET_Core.Models;

/// <summary>
/// Informations techniques sur l'appareil mobile ayant envoyé la synchronisation.
/// </summary>
public class SynchronisationMobileRequest
{
    /// <summary>
    /// Nom de l'appareil mobile.
    /// </summary>
    /// <remarks>
    /// Exemple : Samsung A15
    /// </remarks>
    public string NomAppareil { get; set; } = string.Empty;

    /// <summary>
    /// Version de l'application mobile utilisée lors de l'envoi.
    /// </summary>
    /// <remarks>
    /// Exemple : 1.0.0
    /// </remarks>
    public string VersionApplication { get; set; } = string.Empty;

    /// <summary>
    /// Date et heure du chargement initial de la tournée sur le mobile.
    /// </summary>
    /// <remarks>
    /// Format recommandé : date ISO 8601 avec fuseau horaire.
    ///
    /// Exemple : 2026-04-28T07:30:00+02:00
    /// </remarks>
    public string DateChargementMobile { get; set; } = string.Empty;

    /// <summary>
    /// Date et heure d'envoi de la synchronisation vers l'API.
    /// </summary>
    /// <remarks>
    /// Format recommandé : date ISO 8601 avec fuseau horaire.
    ///
    /// Exemple : 2026-04-28T16:45:00+02:00
    /// </remarks>
    public string DateEnvoiMobile { get; set; } = string.Empty;
}