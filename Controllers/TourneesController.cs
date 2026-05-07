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

    /// <summary>
    /// Renvoie la liste des tournées disponibles pour une date et un livreur.
    /// </summary>
    /// <remarks>
    /// Cette route est utilisée par l'écran mobile "Choix de tournée".
    ///
    /// Elle permet à l'application mobile de récupérer les tournées disponibles
    /// avant de charger le détail complet d'une tournée.
    ///
    /// Paramètres :
    /// - dateTournee : date de la tournée au format yyyy-MM-dd ;
    /// - codeLivreur : code du livreur connecté.
    ///
    /// Réponse :
    /// - 200 : liste des tournées disponibles. La liste peut être vide si aucune tournée n'est trouvée ;
    /// - 400 : date invalide ou code livreur manquant ;
    /// - 404 : livreur introuvable ;
    /// - 500 : erreur technique côté serveur.
    ///
    /// Exemple :
    /// GET /api/tournees/disponibles?dateTournee=2026-05-06&amp;codeLivreur=2
    /// </remarks>
    [HttpGet("disponibles")]
    [ProducesResponseType(typeof(IReadOnlyList<TourneeDisponibleDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiValidationErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiNotFoundResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiTechnicalErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IReadOnlyList<TourneeDisponibleDto>>> GetTourneesDisponibles(
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

        var tournees = await _tourneesService.GetTourneesDisponiblesAsync(date, codeLivreur);

        if (tournees is null)
        {
            return NotFound(new ApiNotFoundResponse
            {
                Statut = ApiErrorCodes.NotFound,
                Message = $"Livreur introuvable : {codeLivreur}"
            });
        }

        return Ok(tournees);
    }

    /// <summary>
    /// Renvoie le détail complet d'une tournée sélectionnée.
    /// </summary>
    /// <remarks>
    /// Cette route est utilisée après sélection d'une tournée dans l'application mobile.
    ///
    /// Elle renvoie les informations nécessaires au chargement local de la tournée :
    /// en-tête de tournée, livreur, informations de chargement et lignes clients.
    ///
    /// Paramètres :
    /// - dateTournee : date de la tournée au format yyyy-MM-dd ;
    /// - codeLivreur : code du livreur connecté ;
    /// - codeTournee : code de la tournée sélectionnée ;
    /// - nomLivreur : paramètre optionnel.
    ///
    /// L'API renvoie les données brutes nécessaires à l'application mobile.
    /// Les règles d'affichage propres à l'interface mobile ne sont pas appliquées ici.
    ///
    /// Réponse :
    /// - 200 : détail complet de la tournée ;
    /// - 400 : date invalide, code livreur manquant ou code tournée manquant ;
    /// - 404 : livreur ou tournée introuvable ;
    /// - 500 : erreur technique côté serveur.
    ///
    /// Exemple :
    /// GET /api/tournees/jour?dateTournee=2026-05-06&amp;codeLivreur=2&amp;codeTournee=3001
    /// </remarks>
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