namespace API_ASP.NET_Core.Application.Mobile;

/// <summary>
/// Configuration finale de l'enrichissement optionnel des points de livraison mobile
/// à partir de la vue SQL [lavinprosli].[dbo].[v_Mobile_AdresseLivraison].
/// </summary>
public sealed class LiensAdresseLivraisonOptions
{
    public const string SectionName = "LiensAdresseLivraison";

    /// <summary>
    /// Active ou désactive la lecture du lien d'adresse de livraison depuis la vue SQL finale.
    /// Si false, l'API renvoie null sans bloquer le chargement mobile.
    /// </summary>
    public bool Enabled { get; set; } = true;
}
