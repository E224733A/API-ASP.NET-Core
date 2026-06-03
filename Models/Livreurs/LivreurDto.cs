// Models/LivreurDto.cs
namespace API_ASP.NET_Core.Models;

/// <summary>
/// Livreur ou chauffeur connu de l'application mobile.
/// </summary>
/// <remarks>
/// Ce modèle est utilisé pour identifier le livreur lors du chargement
/// et de la synchronisation d'une tournée.
/// </remarks>
public class LivreurDto
{
    /// <summary>
    /// Code métier du livreur.
    /// </summary>
    /// <remarks>
    /// Exemple : 2
    /// </remarks>
    public string CodeLivreur { get; set; } = default!;

    /// <summary>
    /// Nom du livreur.
    /// </summary>
    /// <remarks>
    /// Exemple : DAVID LEBAS
    /// </remarks>
    public string NomLivreur { get; set; } = default!;
}