using API_ASP.NET_Core.Constants;

namespace API_ASP.NET_Core.Models;

/// <summary>
/// Réponse renvoyée au mobile pour la liste des camions disponibles.
/// </summary>
public sealed class CamionsDisponiblesResponseDto
{
    /// <summary>
    /// Version du schéma JSON mobile.
    /// </summary>
    public string SchemaVersion { get; init; } = SchemaVersions.SynchronisationActuelle;

    /// <summary>
    /// Liste normalisée des camions disponibles.
    /// </summary>
    public IList<CamionMobileDto> Camions { get; init; } = new List<CamionMobileDto>();
}
