namespace API_ASP.NET_Core.Application.Mobile;

/// <summary>
/// Normalise et filtre les liens d'adresse de livraison avant exposition au mobile.
/// </summary>
/// <remarks>
/// Le lien est optionnel : une valeur absente ou invalide doit devenir null plutôt que
/// bloquer le chargement de la tournée. Seuls les schémas utiles au guidage ou à l'ouverture
/// d'une carte sont acceptés.
/// </remarks>
internal static class LienAdresseLivraisonUrlValidator
{
    /// <summary>
    /// Retourne l'URL normalisée si elle est exploitable par le mobile, sinon null.
    /// </summary>
    public static string? NormalizeUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            return null;
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(uri.Scheme, "geo", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(uri.Scheme, "google.navigation", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return trimmed;
    }
}
