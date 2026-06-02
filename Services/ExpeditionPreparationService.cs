using System.Threading;
using System.Threading.Tasks;
using API_ASP.NET_Core.Models;

namespace API_ASP.NET_Core.Services;

/// <summary>
/// Service d’orchestration pour les préparations Expédition.
/// Dans cette version, ce service délègue au service existant <see cref="ExpeditionService"/>
/// afin de conserver intacte la logique métier et les règles actuelles.
/// L’objectif est de clarifier l’architecture en séparant les responsabilités HTTP (contrôleur)
/// des appels métier.
/// </summary>
public sealed class ExpeditionPreparationService
{
    private readonly ExpeditionService _expeditionService;

    /// <summary>
    /// Initialise une nouvelle instance du service de préparation Expédition.
    /// </summary>
    /// <param name="expeditionService">Service existant regroupant la logique métier actuelle.</param>
    public ExpeditionPreparationService(ExpeditionService expeditionService)
    {
        _expeditionService = expeditionService;
    }

    /// <summary>
    /// Récupère la liste des préparations à préparer pour la date calculée côté API.
    /// Ce service délègue l’appel à <see cref="ExpeditionService.GetPreparationsAPreparerAsync(CancellationToken)"/>.
    /// </summary>
    /// <param name="cancellationToken">Jeton d’annulation.</param>
    /// <returns>DTO de réponse pour le GET Expédition.</returns>
    public Task<ExpeditionPreparationResponseDto> GetPreparationsAPreparerAsync(CancellationToken cancellationToken = default)
    {
        return _expeditionService.GetPreparationsAPreparerAsync(cancellationToken);
    }
}