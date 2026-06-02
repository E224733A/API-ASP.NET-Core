// Controllers/LivreursController.cs
using Microsoft.AspNetCore.Mvc;
using API_ASP.NET_Core.Models;
using API_ASP.NET_Core.Services;

namespace API_ASP.NET_Core.Controllers;

/// <summary>
/// Contrôleur permettant de consulter les livreurs disponibles.
/// </summary>
/// <remarks>
/// Ce contrôleur peut être utilisé par l'application mobile pour récupérer
/// la liste des livreurs/chauffeurs connus de l'API.
/// </remarks>
[ApiController]
[Route("api/livreurs")]
[Produces("application/json")]
public class LivreursController : ControllerBase
{
    private readonly LivreursService _livreursService;

    public LivreursController(LivreursService livreursService)
    {
        _livreursService = livreursService;
    }

    /// <summary>
    /// Retourne la liste des livreurs/chauffeurs.
    /// </summary>
    /// <remarks>
    /// Cette route permet de consulter les livreurs disponibles dans les données métier.
    ///
    /// Réponses :
    /// - 200 : liste des livreurs ;
    /// - 500 : erreur technique côté serveur.
    ///
    /// Exemple :
    /// GET /api/livreurs
    /// </remarks>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<LivreurDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiTechnicalErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IEnumerable<LivreurDto>>> GetAll()
    {
        var livreurs = await _livreursService.GetAllAsync();
        return Ok(livreurs);
    }
}