namespace API_ASP.NET_Core.Application.Mobile;

public interface ILienAdresseLivraisonProvider
{
    Task<string?> GetLienAdresseLivraisonAsync(string? codePdl, CancellationToken cancellationToken = default);
}
