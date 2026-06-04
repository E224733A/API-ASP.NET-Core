namespace API_ASP.NET_Core.Models;

/// <summary>
/// Informations de trajet envoyées par le mobile lors de la synchronisation finale.
/// </summary>
/// <remarks>
/// Ce modèle prépare le contrat POST /api/synchronisations v1.3.
/// Dans ce lot, ces données sont validées mais ne sont pas encore sauvegardées en base.
/// </remarks>
public sealed class SynchronisationTrajetRequest
{
    /// <summary>
    /// Camion sélectionné par le livreur.
    /// </summary>
    /// <remarks>
    /// Obligatoire en schemaVersion 1.3.
    /// </remarks>
    public SynchronisationCamionRequest? Camion { get; set; }

    /// <summary>
    /// Kilométrage du camion au départ de la tournée.
    /// </summary>
    /// <remarks>
    /// Obligatoire en schemaVersion 1.3. Doit être positif ou nul.
    /// </remarks>
    public int? KilometrageDepart { get; set; }

    /// <summary>
    /// Kilométrage du camion à l'arrivée de la tournée.
    /// </summary>
    /// <remarks>
    /// Obligatoire en schemaVersion 1.3. Doit être positif ou nul et supérieur ou égal au kilométrage de départ.
    /// </remarks>
    public int? KilometrageArrivee { get; set; }

    /// <summary>
    /// Date et heure de départ saisies côté mobile.
    /// </summary>
    /// <remarks>
    /// Obligatoire en schemaVersion 1.3. Format recommandé : 2026-06-04T08:00:00+02:00.
    /// </remarks>
    public string? DateDepartMobile { get; set; }

    /// <summary>
    /// Date et heure d'arrivée saisies côté mobile.
    /// </summary>
    /// <remarks>
    /// Obligatoire en schemaVersion 1.3. Doit être supérieure ou égale à DateDepartMobile.
    /// </remarks>
    public string? DateArriveeMobile { get; set; }
}
