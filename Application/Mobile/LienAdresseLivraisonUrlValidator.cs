using System.Globalization;

namespace API_ASP.NET_Core.Application.Mobile;

internal static class LienAdresseLivraisonUrlValidator
{
    public static AdresseLivraisonInfo? CreateInfo(
        double? latitudeLivraison,
        double? longitudeLivraison,
        string? lienAdresseLivraison = null)
    {
        var normalizedUrl = NormalizeUrl(lienAdresseLivraison);

        if (AreValidGpsCoordinates(latitudeLivraison, longitudeLivraison))
        {
            var generatedUrl = BuildGoogleMapsDirectionsUrl(
                latitudeLivraison!.Value,
                longitudeLivraison!.Value);

            return new AdresseLivraisonInfo(
                latitudeLivraison.Value,
                longitudeLivraison.Value,
                normalizedUrl ?? generatedUrl);
        }

        return normalizedUrl is null
            ? null
            : new AdresseLivraisonInfo(null, null, normalizedUrl);
    }

    public static bool AreValidGpsCoordinates(double? latitude, double? longitude)
    {
        if (!latitude.HasValue || !longitude.HasValue)
        {
            return false;
        }

        if (double.IsNaN(latitude.Value)
            || double.IsInfinity(latitude.Value)
            || double.IsNaN(longitude.Value)
            || double.IsInfinity(longitude.Value))
        {
            return false;
        }

        return latitude.Value >= -90
            && latitude.Value <= 90
            && longitude.Value >= -180
            && longitude.Value <= 180
            && !(latitude.Value == 0 && longitude.Value == 0);
    }

    public static string BuildGoogleMapsDirectionsUrl(double latitude, double longitude)
    {
        var latitudeText = latitude.ToString(CultureInfo.InvariantCulture);
        var longitudeText = longitude.ToString(CultureInfo.InvariantCulture);

        return $"https://www.google.com/maps/dir/?api=1&destination={latitudeText},{longitudeText}&travelmode=driving";
    }

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
