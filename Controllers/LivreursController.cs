// Controllers/LivreursController.cs
using Microsoft.AspNetCore.Mvc;
using API_ASP.NET_Core.Repositories;
using API_ASP.NET_Core.Models;

namespace API_ASP.NET_Core.Controllers;

[ApiController]
[Route("api/livreurs")]
public class LivreursController : ControllerBase
{
    private readonly LivreursRepository _livreursRepository;

    public LivreursController(LivreursRepository livreursRepository)
    {
        _livreursRepository = livreursRepository;
    }

    /// <summary>
    /// Retourne la liste des livreurs/chauffeurs pour l’application mobile.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<LivreurDto>>> GetAll()
    {
        var livreurs = await _livreursRepository.GetAllAsync();
        return Ok(livreurs);
    }
}