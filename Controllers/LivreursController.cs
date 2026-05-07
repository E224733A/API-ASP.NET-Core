// Controllers/LivreursController.cs
using Microsoft.AspNetCore.Mvc;
using API_ASP.NET_Core.Repositories;
using API_ASP.NET_Core.Models;

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
    private readonly LivreursRepository _livreursRepository;

    public LivreursController(LivreursRepository livreursRepository)
    {
        _livreursRepository = livreursRepository;
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
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IEnumerable<LivreurDto>>> GetAll()
    {
        var livreurs = await _livreursRepository.GetAllAsync();
        return Ok(livreurs);
    }
}