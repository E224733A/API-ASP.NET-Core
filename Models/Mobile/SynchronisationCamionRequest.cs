namespace API_ASP.NET_Core.Models;

/// <summary>
/// Camion sélectionné par le livreur pour une synchronisation mobile.
/// </summary>
/// <remarks>
/// Ce modèle est utilisé dans la section trajet du contrat POST /api/synchronisations v1.3.
/// Les données sont un snapshot du camion choisi côté mobile au moment de la tournée.
/// </remarks>
public sealed class SynchronisationCamionRequest
{
    /// <summary>
    /// Identifiant source du camion.
    /// </summary>
    /// <remarks>
    /// Champ obligatoire en schemaVersion 1.3.
    /// Exemple : DY-662-QN.
    /// </remarks>
    public string IdCamion { get; set; } = string.Empty;

    /// <summary>
    /// Code métier du camion, lorsqu'il est disponible.
    /// </summary>
    public string? CodeCamion { get; set; }

    /// <summary>
    /// Libellé lisible du camion, lorsqu'il est disponible.
    /// </summary>
    public string? LibelleCamion { get; set; }

    /// <summary>
    /// Immatriculation du camion, lorsqu'elle est disponible.
    /// </summary>
    public string? Immatriculation { get; set; }
}
