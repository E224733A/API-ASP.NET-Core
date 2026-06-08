namespace API_ASP.NET_Core.Application.Mobile;

/// <summary>
/// Configuration de l'enrichissement optionnel des points de livraison pour le mobile.
/// </summary>
public sealed class LiensAdresseLivraisonOptions
{
    public const string SectionName = "LiensAdresseLivraison";

    /// <summary>
    /// Active ou désactive complètement l'enrichissement des points de livraison.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Mode de résolution : Disabled, Hardcoded ou Repository.
    /// </summary>
    public string Mode { get; set; } = "Disabled";

    /// <summary>
    /// URL temporaire utilisée uniquement en mode Hardcoded si aucune coordonnée GPS de test n'est fournie.
    /// </summary>
    public string? HardcodedUrl { get; set; }

    /// <summary>
    /// Latitude GPS WGS84 temporaire utilisée uniquement en mode Hardcoded pour tester le mobile avant livraison de la vue ERP.
    /// Exemple : 4.9224.
    /// </summary>
    public double? HardcodedLatitude { get; set; }

    /// <summary>
    /// Longitude GPS WGS84 temporaire utilisée uniquement en mode Hardcoded pour tester le mobile avant livraison de la vue ERP.
    /// Exemple : -52.3135.
    /// </summary>
    public double? HardcodedLongitude { get; set; }
}
