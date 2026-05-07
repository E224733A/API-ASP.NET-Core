namespace API_ASP.NET_Core.Models;

/// <summary>
/// Détail complet d'une synchronisation enregistrée.
/// </summary>
/// <remarks>
/// Ce modèle est utilisé pour consulter l'ensemble des données reçues
/// pour une tournée mobile synchronisée.
/// </remarks>
public sealed class SynchronisationDetailDto
{
    /// <summary>
    /// En-tête ou résumé général de la synchronisation.
    /// </summary>
    public SynchronisationResumeDto Entete { get; set; } = new();

    /// <summary>
    /// Liste des lignes de tournée enregistrées.
    /// </summary>
    public List<SynchronisationLigneDetailDto> Lignes { get; set; } = new();

    /// <summary>
    /// Liste détaillée des quantités enregistrées par article.
    /// </summary>
    public List<SynchronisationQuantiteDetailDto> Quantites { get; set; } = new();

    /// <summary>
    /// Logs techniques associés à la synchronisation.
    /// </summary>
    public List<SynchronisationLogDto> Logs { get; set; } = new();
}