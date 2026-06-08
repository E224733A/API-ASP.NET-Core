namespace API_ASP.NET_Core.Application.Mobile;

public sealed class DisabledLienAdresseLivraisonProvider : ILienAdresseLivraisonProvider
{
    public Task<AdresseLivraisonInfo?> GetAdresseLivraisonAsync(
        string? codePdl,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<AdresseLivraisonInfo?>(null);
    }
}
