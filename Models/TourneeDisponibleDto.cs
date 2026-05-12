namespace API_ASP.NET_Core.Models;

/// <summary>
/// Réponse de la route GET /api/tournees/disponibles.
/// </summary>
public sealed class TourneesDisponiblesResponseDto
{
    /// <summary>
    /// Version du contrat JSON.
    /// </summary>
    public string SchemaVersion { get; set; } = "1.2";

    /// <summary>
    /// Date de tournée concernée.
    /// Format : yyyy-MM-dd.
    /// </summary>
    public string DateTournee { get; set; } = string.Empty;

    /// <summary>
    /// La date est affichée en lecture seule côté mobile.
    /// </summary>
    public bool DateModifiable { get; set; } = false;

    /// <summary>
    /// Livreur connecté.
    /// </summary>
    public LivreurDto Livreur { get; set; } = default!;

    /// <summary>
    /// Tournées disponibles pour cette date.
    /// </summary>
    public IList<TourneeDisponibleDto> Tournees { get; set; } = new List<TourneeDisponibleDto>();
}

/// <summary>
/// Tournée disponible pour une date donnée.
/// </summary>
public sealed class TourneeDisponibleDto
{
    /// <summary>
    /// Code de la tournée disponible.
    /// Exemple : 4006.
    /// </summary>
    public string CodeTournee { get; set; } = string.Empty;

    /// <summary>
    /// Libellé lisible de la tournée.
    /// </summary>
    public string LibelleTournee { get; set; } = string.Empty;

    /// <summary>
    /// Nombre de points de livraison trouvés pour cette tournée.
    /// </summary>
    public int NombrePoints { get; set; }
}
