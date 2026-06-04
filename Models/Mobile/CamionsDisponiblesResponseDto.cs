namespace API_ASP.NET_Core.Models;

/// <summary>
/// Réponse renvoyée au mobile pour la liste des camions disponibles.
/// </summary>
public sealed class CamionsDisponiblesResponseDto
{
    private const string SchemaVersionCamionsDisponibles = "1.3";

    /// <summary>
    /// Version du schéma JSON mobile pour la route GET /api/camions/disponibles.
    /// </summary>
    public string SchemaVersion { get; init; } = SchemaVersionCamionsDisponibles;

    /// <summary>
    /// Liste normalisée des camions disponibles.
    /// </summary>
    public IList<CamionMobileDto> Camions { get; init; } = new List<CamionMobileDto>();
}
