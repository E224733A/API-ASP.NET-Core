namespace API_ASP.NET_Core.Application.Mobile;

public sealed class DisabledLienAdresseLivraisonProvider : ILienAdresseLivraisonProvider
{
    public Task<string?> GetLienAdresseLivraisonAsync(
        string? codePdl,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<string?>(null);
    }
}
