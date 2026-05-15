using API_ASP.NET_Core.Models;
using API_ASP.NET_Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace API_ASP.NET_Core.Controllers;

[ApiController]
[Route("api/expedition/preparations")]
[Produces("application/json")]
public sealed class ExpeditionPreparationsController : ControllerBase
{
    private readonly ExpeditionService _expeditionService;

    public ExpeditionPreparationsController(ExpeditionService expeditionService)
    {
        _expeditionService = expeditionService;
    }

    /// <summary>
    /// Retourne les lignes préparables par le module Expédition pour la prochaine date préparable.
    /// La date est calculée côté API à partir des données disponibles.
    /// </summary>
    [HttpGet("a-preparer")]
    [ProducesResponseType(typeof(ExpeditionPreparationResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPreparationsAPreparer(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _expeditionService.GetPreparationsAPreparerAsync(cancellationToken);
            return Ok(response);
        }
        catch (InvalidOperationException exception)
        {
            return NotFound(new
            {
                statut = "NOT_FOUND",
                message = exception.Message
            });
        }
    }

    /// <summary>
    /// Verrouille définitivement une préparation Expédition et la rend disponible pour le GET mobile.
    /// </summary>
    /// <remarks>
    /// Le mobile ne lit ensuite que les préparations dont EstVerrouille = 1.
    /// ROLLS_VIDES est refusé côté Expédition car cet article est uniquement récupéré sur le terrain.
    /// </remarks>
    [HttpPost("verrouiller")]
    [ProducesResponseType(typeof(ExpeditionApiResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ExpeditionApiResult), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ExpeditionApiResult), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ExpeditionApiResult), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> VerrouillerPreparation(
        [FromBody] ExpeditionVerrouillageRequest? request,
        CancellationToken cancellationToken = default)
    {
        var adresseIp = HttpContext.Connection.RemoteIpAddress?.ToString();

        var result = await _expeditionService.VerrouillerPreparationAsync(
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
