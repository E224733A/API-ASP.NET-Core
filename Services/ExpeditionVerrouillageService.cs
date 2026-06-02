using System.Threading;
using System.Threading.Tasks;
using API_ASP.NET_Core.Models;

namespace API_ASP.NET_Core.Services;

/// <summary>
/// Service d’orchestration pour le verrouillage des lots Expédition.
/// Ce service sépare les responsabilités du contrôleur et encapsule l’appel au
/// service existant <see cref="ExpeditionService"/>. Aucune logique métier n’est modifiée ici.
/// </summary>
public sealed class ExpeditionVerrouillageService
{
    private readonly ExpeditionService _expeditionService;

    /// <summary>
    /// Initialise une nouvelle instance du service de verrouillage Expédition.
    /// </summary>
    /// <param name="expeditionService">Service existant regroupant la logique métier actuelle.</param>
    public ExpeditionVerrouillageService(ExpeditionService expeditionService)
    {
        _expeditionService = expeditionService;
    }

    /// <summary>
    /// Verrouille un lot global de préparations Expédition.
    /// Ce service délègue l’appel à <see cref="ExpeditionService.VerrouillerPreparationLotAsync(ExpeditionVerrouillageLotRequest?, string?, CancellationToken)"/>.
    /// </summary>
    /// <param name="request">Requête du client contenant le lot à verrouiller.</param>
    /// <param name="adresseIp">Adresse IP du client appelant.</param>
    /// <param name="cancellationToken">Jeton d’annulation.</param>
    /// <returns>Tuple (statusCode, body) identique à celui retourné par le service interne.</returns>
    public Task<(int StatusCode, object Body)> VerrouillerPreparationLotAsync(
        ExpeditionVerrouillageLotRequest? request,
        string? adresseIp,
        CancellationToken cancellationToken = default)
    {
        return _expeditionService.VerrouillerPreparationLotAsync(request, adresseIp, cancellationToken);
    }
}