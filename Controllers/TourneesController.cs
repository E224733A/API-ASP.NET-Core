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
	/// Renvoie la tournée du jour pour un livreur et éventuellement un code tournée.
	/// Exemple : GET /api/tournees/jour?dateTournee=2026-04-28&codeLivreur=2&codeTournee=2001
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

		var tournee = await _tourneesService.GetTourneeAsync(date, codeLivreur, codeTournee, nomLivreur);
		if (tournee == null)
		{
			return NotFound();
		}

		return Ok(tournee);
	}
}