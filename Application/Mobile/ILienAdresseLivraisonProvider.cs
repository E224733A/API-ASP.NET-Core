namespace API_ASP.NET_Core.Application.Mobile;

/// <summary>
/// Fournit le lien d'adresse de livraison optionnel associé au couple métier NUM_CLI + CodePDL.
/// La source finale est une vue SQL réutilisable qui expose NUM_CLI, CodePDL et AdresseLivraison.
/// </summary>
public interface ILienAdresseLivraisonProvider
{
    Task<string?> GetLienAdresseLivraisonAsync(
        string? numCli,
        string? codePdl,
        CancellationToken cancellationToken = default);
}
