using Microsoft.Extensions.Options;

namespace API_ASP.NET_Core.Application.Mobile;

/// <summary>
/// Provider temporaire utilisé uniquement pour valider le flux API + mobile avant disponibilité de la source métier finale.
/// En mode Hardcoded, les coordonnées GPS de test sont prioritaires sur l'URL de test.
/// </summary>
public sealed class HardcodedLienAdresseLivraisonProvider : ILienAdresseLivraisonProvider
{
    private readonly LiensAdresseLivraisonOptions _options;

    public HardcodedLienAdresseLivraisonProvider(IOptions<LiensAdresseLivraisonOptions> options)
    {
        _options = options.Value;
    }

    public Task<AdresseLivraisonInfo?> GetAdresseLivraisonAsync(
        string? codePdl,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled || string.IsNullOrWhiteSpace(codePdl))
        {
            return Task.FromResult<AdresseLivraisonInfo?>(null);
        }

        var info = LienAdresseLivraisonUrlValidator.CreateInfo(
            _options.HardcodedLatitude,
            _options.HardcodedLongitude,
            _options.HardcodedUrl);

        return Task.FromResult(info);
    }
}
