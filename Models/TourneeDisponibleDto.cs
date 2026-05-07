namespace API_ASP.NET_Core.Models;

/// <summary>
/// Tournée disponible pour une date donnée.
/// </summary>
/// <remarks>
/// Ce modèle est renvoyé par GET /api/tournees/disponibles.
/// Il sert à alimenter l'écran mobile de choix de tournée.
/// </remarks>
public sealed class TourneeDisponibleDto
{
    /// <summary>
    /// Date de la tournée.
    /// </summary>
    /// <remarks>
    /// Format : yyyy-MM-dd.
    ///
    /// Exemple : 2026-05-06
    /// </remarks>
    public string DateTournee { get; set; } = string.Empty;

    /// <summary>
    /// Numéro du jour de tournée.
    /// </summary>
    /// <remarks>
    /// Exemple : 1 pour lundi, 2 pour mardi, etc.
    /// </remarks>
    public int JourTournee { get; set; }

    /// <summary>
    /// Libellé du jour de tournée.
    /// </summary>
    /// <remarks>
    /// Exemple : Mardi
    /// </remarks>
    public string JourLibelle { get; set; } = string.Empty;

    /// <summary>
    /// Code de la tournée disponible.
    /// </summary>
    /// <remarks>
    /// Exemple : 2001
    /// </remarks>
    public string CodeTournee { get; set; } = string.Empty;

    /// <summary>
    /// Libellé lisible de la tournée.
    /// </summary>
    /// <remarks>
    /// Exemple : MDR VENDEE
    /// </remarks>
    public string LibelleTournee { get; set; } = string.Empty;
}