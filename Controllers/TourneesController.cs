using Microsoft.AspNetCore.Mvc;
using API_ASP.NET_Core.Models;
using API_ASP.NET_Core.Services;

namespace API_ASP.NET_Core.Controllers;

[ApiController]
[Route("api/tournees")]
public class TourneesController : ControllerBase
{
    private readonly TourneesService _tourneesService;

    public TourneesController(TourneesService service)
    {
        _tourneesService = service;
    }

    /// <summary>
    /// Renvoie uniquement la liste des tournées disponibles pour une date.
    ///
    /// Cette route est utilisée par l'écran mobile "Choix de tournée".
    /// Elle ne renvoie pas les lignes clients.
    ///
    /// Exemple :
    /// GET /api/tournees/disponibles?dateTournee=2026-05-06&codeLivreur=2
    /// </summary>
    [HttpGet("disponibles")]
    public async Task<ActionResult<IReadOnlyList<TourneeDisponibleDto>>> GetTourneesDisponibles(
        [FromQuery] string dateTournee,
        [FromQuery] string codeLivreur)
    {
        if (!DateOnly.TryParse(dateTournee, out var date))
        {
            return BadRequest("Paramètre dateTournee invalide. Format attendu : YYYY-MM-DD");
        }

        if (string.IsNullOrWhiteSpace(codeLivreur))
        {
            return BadRequest("Paramètre codeLivreur obligatoire.");
        }

        var tournees = await _tourneesService.GetTourneesDisponiblesAsync(date, codeLivreur);

        if (tournees is null)
        {
            return NotFound($"Livreur introuvable : {codeLivreur}");
        }

        return Ok(tournees);
    }

    /// <summary>
    /// Renvoie la tournée complète du jour pour un livreur et un code tournée.
    ///
    /// Cette route est utilisée après sélection d'une tournée.
    ///
    /// Exemple :
    /// GET /api/tournees/jour?dateTournee=2026-05-06&codeLivreur=2&codeTournee=3001
    /// </summary>
    [HttpGet("jour")]
    public async Task<ActionResult<TourneeMobileDto>> GetTourneeDuJour(
        [FromQuery] string dateTournee,
        [FromQuery] string codeLivreur,
        [FromQuery] string? codeTournee = null,
        [FromQuery] string? nomLivreur = null)
    {
        if (!DateOnly.TryParse(dateTournee, out var date))
        {
            return BadRequest("Paramètre dateTournee invalide. Format attendu : YYYY-MM-DD");
        }

        if (string.IsNullOrWhiteSpace(codeLivreur))
        {
            return BadRequest("Paramètre codeLivreur obligatoire.");
        }

        if (string.IsNullOrWhiteSpace(codeTournee))
        {
            return BadRequest("Paramètre codeTournee obligatoire pour charger une tournée complète. Utilisez /api/tournees/disponibles pour obtenir la liste des tournées.");
        }

        var tournee = await _tourneesService.GetTourneeAsync(date, codeLivreur, codeTournee, nomLivreur);
        if (tournee == null)
        {
            return NotFound();
        }

        return Ok(tournee);
    }
}
