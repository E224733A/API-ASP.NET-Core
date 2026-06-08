namespace API_ASP.NET_Core.Application.Mobile;

/// <summary>
/// Fournit le lien d'adresse de livraison optionnel associé à un CodePDL.
/// La source finale est une vue SQL réutilisable qui expose uniquement CodePDL et AdresseLivraison.
/// </summary>
public interface ILienAdresseLivraisonProvider
{
    Task<string?> GetLienAdresseLivraisonAsync(
        string? codePdl,
        CancellationToken cancellationToken = default);
}
