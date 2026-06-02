using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using API_ASP.NET_Core.Models;
using API_ASP.NET_Core.Validators;
using Microsoft.AspNetCore.Http;

namespace API_ASP.NET_Core.Services;

/// <summary>
/// Service d’orchestration pour le verrouillage des lots Expédition.
/// Ce service sépare les responsabilités du contrôleur et encapsule l’appel au
/// service existant <see cref="ExpeditionService"/>. Aucune logique métier n’est modifiée ici.
/// </summary>
public sealed class ExpeditionVerrouillageService
{
    private readonly ExpeditionService _expeditionService;
    private readonly ExpeditionVerrouillageValidator _validator;

    /// <summary>
    /// Initialise une nouvelle instance du service de verrouillage Expédition.
    /// </summary>
    /// <param name="expeditionService">Service existant regroupant la logique métier actuelle.</param>
    /// <param name="validator">Validator dédié au lot de verrouillage.</param>
    public ExpeditionVerrouillageService(
        ExpeditionService expeditionService,
        ExpeditionVerrouillageValidator validator)
    {
        _expeditionService = expeditionService;
        _validator = validator;
    }

    /// <summary>
    /// Verrouille un lot global de préparations Expédition.
    /// Ce service exécute d'abord une validation dédiée avant de déléguer
    /// l'orchestration métier au service existant. Si des erreurs sont
    /// détectées, elles sont retournées avec le statut 400 sans modifier
    /// le comportement métier actuel.
    /// </summary>
    /// <param name="request">Requête du client contenant le lot à verrouiller.</param>
    /// <param name="adresseIp">Adresse IP du client appelant.</param>
    /// <param name="cancellationToken">Jeton d’annulation.</param>
    /// <returns>Tuple (statusCode, body) correspondant au résultat de l’opération.</returns>
    public async Task<(int StatusCode, object Body)> VerrouillerPreparationLotAsync(
        ExpeditionVerrouillageLotRequest? request,
        string? adresseIp,
        CancellationToken cancellationToken = default)
    {
        var validationErrors = await _validator.ValidateAsync(request, cancellationToken);
        if (validationErrors.Count > 0)
        {
            return (
                StatusCodes.Status400BadRequest,
                new ExpeditionApiResult
                {
                    Statut = "VALIDATION_ERROR",
                    Code = "EXPEDITION_VALIDATION_ERROR",
                    Message = "Le lot de préparation Expédition contient des données invalides.",
                    Errors = validationErrors.ToList(),
                    IdLotVerrouillage = request?.IdLotVerrouillage,
                    DateTournee = request?.DateTournee
                });
        }

        // Déléguer au service existant pour l'orchestration et la persistance.
        return await _expeditionService.VerrouillerPreparationLotAsync(request, adresseIp, cancellationToken);
    }
}