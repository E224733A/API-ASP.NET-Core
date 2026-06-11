using API_ASP.NET_Core.Models;
using API_ASP.NET_Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace API_ASP.NET_Core.Controllers;

/// <summary>
/// Point d'entrée HTTP du module Expédition pour charger et verrouiller les préparations.
/// </summary>
/// <remarks>
/// Les routes Expédition sont volontairement globales : la date métier est calculée côté API
/// et aucun paramètre de date, tournée ou livreur ne doit piloter le chargement. Le contrôleur
/// reste limité au contrat HTTP ; les règles de préparation et de verrouillage sont portées
/// par les services applicatifs.
/// </remarks>
[ApiController]
[Route("api/expedition/preparations")]
[Produces("application/json")]
public sealed class ExpeditionPreparationsController : ControllerBase
{
    // Règle d'architecture : le contrôleur ne décide pas du métier Expédition.
    // Il délègue aux services pour éviter de mélanger contrat HTTP, validation et persistance SQL.
    private readonly ExpeditionPreparationService _preparationService;
    private readonly ExpeditionVerrouillageService _verrouillageService;

    public ExpeditionPreparationsController(
        ExpeditionPreparationService preparationService,
        ExpeditionVerrouillageService verrouillageService)
    {
        _preparationService = preparationService;
        _verrouillageService = verrouillageService;
    }

    /// <summary>
    /// Charge toutes les préparations Expédition pour la date préparable calculée côté API.
    /// Aucun paramètre de requête n'est accepté sur cette route.
    /// </summary>
    [HttpGet("a-preparer")]
    [ProducesResponseType(typeof(ExpeditionPreparationResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ExpeditionApiResult), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetPreparationsAPreparer(CancellationToken cancellationToken = default)
    {
        if (Request.Query.Count > 0)
        {
            return BadRequest(new ExpeditionApiResult
            {
                Statut = "VALIDATION_ERROR",
                Code = "EXPEDITION_GET_QUERY_PARAMS_FORBIDDEN",
                Message = "Le GET Expédition est global : aucun paramètre dateTournee, codeTournee ou codeLivreur n'est autorisé.",
                Errors = Request.Query.Keys
                    .Select(key => $"Paramètre interdit : {key}")
                    .ToList()
            });
        }

        var response = await _preparationService.GetPreparationsAPreparerAsync(cancellationToken);
        return Ok(response);
    }

    /// <summary>
    /// Verrouille un lot global de préparations Expédition.
    /// </summary>
    [HttpPost("verrouiller")]
    [ProducesResponseType(typeof(ExpeditionApiResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ExpeditionApiResult), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ExpeditionApiResult), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ExpeditionApiResult), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> VerrouillerPreparation(
        [FromBody] ExpeditionVerrouillageLotRequest? request,
        CancellationToken cancellationToken = default)
    {
        var adresseIp = HttpContext.Connection.RemoteIpAddress?.ToString();

        var result = await _verrouillageService.VerrouillerPreparationLotAsync(
            request,
            adresseIp,
            cancellationToken);

        return result.StatusCode switch
        {
            StatusCodes.Status200OK => Ok(result.Body),
            StatusCodes.Status400BadRequest => BadRequest(result.Body),
            StatusCodes.Status409Conflict => Conflict(result.Body),
            _ => StatusCode(result.StatusCode, result.Body)
        };
    }
}
