namespace API_ASP.NET_Core.Application.Mobile;

/// <summary>
/// Informations de navigation associées à un point de livraison.
/// Les coordonnées GPS WGS84 sont la source finale prioritaire.
/// Le lien est conservé comme compatibilité ou comme valeur générée à partir des coordonnées.
/// </summary>
public sealed record AdresseLivraisonInfo(
    double? LatitudeLivraison,
    double? LongitudeLivraison,
    string? LienAdresseLivraison);

public interface ILienAdresseLivraisonProvider
{
    Task<AdresseLivraisonInfo?> GetAdresseLivraisonAsync(
        string? codePdl,
        CancellationToken cancellationToken = default);
}
