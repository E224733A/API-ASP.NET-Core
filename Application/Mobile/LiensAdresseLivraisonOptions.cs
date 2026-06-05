namespace API_ASP.NET_Core.Application.Mobile;

/// <summary>
/// Configuration du champ optionnel pointLivraison.lienAdresseLivraison renvoyé au mobile.
/// </summary>
public sealed class LiensAdresseLivraisonOptions
{
    public const string SectionName = "LiensAdresseLivraison";

    /// <summary>
    /// Active ou désactive complètement l'enrichissement des points de livraison.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Mode de résolution du lien : Disabled, Hardcoded ou Repository.
    /// </summary>
    public string Mode { get; set; } = "Disabled";

    /// <summary>
    /// URL temporaire utilisée uniquement en mode Hardcoded pour les tests mobile.
    /// </summary>
    public string? HardcodedUrl { get; set; }
}
