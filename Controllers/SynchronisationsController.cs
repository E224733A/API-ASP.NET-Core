using API_ASP.NET_Core.Models;
using API_ASP.NET_Core.Services;
using API_ASP.NET_Core.Mappers;
using Microsoft.AspNetCore.Mvc;

namespace API_ASP.NET_Core.Controllers;

/// <summary>
/// Contrôleur HTTP dédié aux synchronisations de tournées mobiles.
/// Il se contente d'orchestrer la réception de la requête et de déléguer
/// la logique métier au <see cref="SynchronisationService"/>. Toute logique
/// de validation ou d'accès aux données est déportée dans les services
/// et validateurs appropriés.
/// </summary>
[ApiController]
[Route("api/synchronisations")]
public sealed class SynchronisationsController : ControllerBase
{
    private readonly SynchronisationService _synchronisationService;
    private readonly SynchronisationMapper _mapper;
    private readonly ILogger<SynchronisationsController> _logger;

    /// <summary>
    /// Initialise une nouvelle instance de <see cref="SynchronisationsController"/>.
    /// </summary>
    /// <param name="synchronisationService">Service métier pour enregistrer les synchronisations.</param>
    /// <param name="mapper">Mapper pour les conversions élémentaires.</param>
    /// <param name="logger">Logger pour tracer les événements.</param>
    public SynchronisationsController(
        SynchronisationService synchronisationService,
        SynchronisationMapper mapper,
        ILogger<SynchronisationsController> logger)
    {
        _synchronisationService = synchronisationService;
        _mapper = mapper;
        _logger = logger;
    }

    /// <summary>
    /// Enregistre une synchronisation mobile.
    /// </summary>
    /// <param name="request">Payload JSON envoyé par le mobile.</param>
    /// <param name="cancellationToken">Jeton d'annulation asynchrone.</param>
    /// <returns>Une réponse HTTP correspondant au résultat de la synchronisation.</returns>
    [HttpPost]
    public async Task<IActionResult> PostSynchronisation(
        [FromBody] SynchronisationTourneeRequest? request,
        CancellationToken cancellationToken)
    {
        // Validation HTTP minimale : le corps de requête ne doit pas être nul.
        if (request is null)
        {
            return BadRequest(new
            {
                statut = "VALIDATION_ERROR",
                message = "Le corps JSON de la synchronisation est obligatoire.",
                errors = new[]
                {
                    "Le corps de la requête est vide ou invalide."
                },
                details = new[]
                {
                    new
                    {
                        champ = "body",
                        message = "Le corps de la requête est vide ou invalide."
                    }
                }
            });
        }

        var adresseIp = HttpContext.Connection.RemoteIpAddress?.ToString();

        try
        {
            var result = await _synchronisationService.EnregistrerSynchronisationAsync(
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
        catch (Exception exception)
        {
            // Toute exception non gérée est considérée comme une erreur interne.
            _logger.LogError(
                exception,
                "Erreur technique lors de la synchronisation de la tournée {CodeTournee} du {DateTournee}.",
                request.CodeTournee,
                request.DateTournee);

            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new
                {
                    statut = "ERROR",
                    code = "SERVER_ERROR",
                    message = "Une erreur technique est survenue pendant le traitement de la synchronisation."
                });
        }
    }
}