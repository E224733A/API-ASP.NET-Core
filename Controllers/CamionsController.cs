using API_ASP.NET_Core.Models;
using API_ASP.NET_Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace API_ASP.NET_Core.Controllers;

/// <summary>
/// Contrôleur utilisé par l'application mobile pour consulter les camions disponibles.
/// </summary>
[ApiController]
[Route("api/camions")]
[Produces("application/json")]
public sealed class CamionsController : ControllerBase
{
    private readonly CamionsService _camionsService;

    public CamionsController(CamionsService camionsService)
    {
        _camionsService = camionsService;
    }

    /// <summary>
    /// Retourne la liste des camions disponibles pour le mobile.
    /// </summary>
    /// <remarks>
    /// Cette route ne modifie aucune donnée. Elle renvoie un contrat JSON mobile
    /// contenant schemaVersion et camions[].
    ///
    /// Exemple :
    /// GET /api/camions/disponibles
    /// </remarks>
    [HttpGet("disponibles")]
    [ProducesResponseType(typeof(CamionsDisponiblesResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiTechnicalErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<CamionsDisponiblesResponseDto>> GetCamionsDisponibles()
    {
        var response = await _camionsService.GetCamionsDisponiblesAsync();
        return Ok(response);
    }
}
