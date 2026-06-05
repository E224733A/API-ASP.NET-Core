using Microsoft.Extensions.Options;

namespace API_ASP.NET_Core.Application.Mobile;

/// <summary>
/// Provider temporaire utilisé uniquement pour valider le flux API + mobile avant disponibilité de la source métier finale.
/// </summary>
public sealed class HardcodedLienAdresseLivraisonProvider : ILienAdresseLivraisonProvider
{
    private readonly LiensAdresseLivraisonOptions _options;

    public HardcodedLienAdresseLivraisonProvider(IOptions<LiensAdresseLivraisonOptions> options)
    {
        _options = options.Value;
    }

    public Task<string?> GetLienAdresseLivraisonAsync(
        string? codePdl,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled || string.IsNullOrWhiteSpace(codePdl))
        {
            return Task.FromResult<string?>(null);
        }

        var url = LienAdresseLivraisonUrlValidator.NormalizeUrl(_options.HardcodedUrl);
        return Task.FromResult(url);
    }
}
