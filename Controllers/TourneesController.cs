using Microsoft.AspNetCore.Mvc;
using API_ASP.NET_Core.Constants;
using API_ASP.NET_Core.Models;
using API_ASP.NET_Core.Services;

namespace API_ASP.NET_Core.Controllers;

/// <summary>
/// Contrôleur utilisé par l'application mobile pour consulter les tournées disponibles
/// et charger le détail d'une tournée sélectionnée.
/// </summary>
[ApiController]
[Route("api/tournees")]
[Produces("application/json")]
public class TourneesController : ControllerBase
{
    private readonly TourneesService _tourneesService;

    public TourneesController(TourneesService service)
    {
        _tourneesService = service;
    }

    [HttpGet("disponibles")]
    [ProducesResponseType(typeof(TourneesDisponiblesResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiValidationErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiNotFoundResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiTechnicalErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<TourneesDisponiblesResponseDto>> GetTourneesDisponibles(
        [FromQuery] string dateTournee,
        [FromQuery] string codeLivreur)
    {
        if (!DateOnly.TryParse(dateTournee, out var date))
        {
            return BadRequest(new ApiValidationErrorResponse
            {
                Statut = ApiErrorCodes.ValidationError,
                Errors = new[]
                {
                    "Paramètre dateTournee invalide. Format attendu : yyyy-MM-dd."
                }
            });
        }

        if (string.IsNullOrWhiteSpace(codeLivreur))
        {
            return BadRequest(new ApiValidationErrorResponse
            {
                Statut = ApiErrorCodes.ValidationError,
                Errors = new[]
                {
                    "Paramètre codeLivreur obligatoire."
                }
            });
        }

        var response = await _tourneesService.GetTourneesDisponiblesAsync(date, codeLivreur);

        if (response is null)
        {
            return NotFound(new ApiNotFoundResponse
            {
                Statut = ApiErrorCodes.NotFound,
                Message = $"Livreur introuvable : {codeLivreur}"
            });
        }

        return Ok(response);
    }

    [HttpGet("jour")]
    [ProducesResponseType(typeof(TourneeMobileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiValidationErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiNotFoundResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiTechnicalErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<TourneeMobileDto>> GetTourneeDuJour(
        [FromQuery] string dateTournee,
        [FromQuery] string codeLivreur,
        [FromQuery] string? codeTournee = null,
        [FromQuery] string? nomLivreur = null)
    {
        if (!DateOnly.TryParse(dateTournee, out var date))
        {
            return BadRequest(new ApiValidationErrorResponse
            {
                Statut = ApiErrorCodes.ValidationError,
                Errors = new[]
                {
                    "Paramètre dateTournee invalide. Format attendu : yyyy-MM-dd."
                }
            });
        }

        if (string.IsNullOrWhiteSpace(codeLivreur))
        {
            return BadRequest(new ApiValidationErrorResponse
            {
                Statut = ApiErrorCodes.ValidationError,
                Errors = new[]
                {
                    "Paramètre codeLivreur obligatoire."
                }
            });
        }

        if (string.IsNullOrWhiteSpace(codeTournee))
        {
            return BadRequest(new ApiValidationErrorResponse
            {
                Statut = ApiErrorCodes.ValidationError,
                Errors = new[]
                {
                    "Paramètre codeTournee obligatoire pour charger une tournée complète. Utilisez /api/tournees/disponibles pour obtenir la liste des tournées."
                }
            });
        }

        var tournee = await _tourneesService.GetTourneeAsync(date, codeLivreur, codeTournee, nomLivreur);

        if (tournee is null)
        {
            return NotFound(new ApiNotFoundResponse
            {
                Statut = ApiErrorCodes.NotFound,
                Message = "Livreur ou tournée introuvable."
            });
        }

        return Ok(tournee);
    }
}
