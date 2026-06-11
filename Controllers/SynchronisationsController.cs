using API_ASP.NET_Core.Models;
using API_ASP.NET_Core.Services;
using API_ASP.NET_Core.Mappers;
using Microsoft.AspNetCore.Mvc;

namespace API_ASP.NET_Core.Controllers;

/// <summary>
/// Contrôleur HTTP dédié au POST final de synchronisation mobile.
/// </summary>
/// <remarks>
/// Cette route reçoit le bilan d'une tournée terminée par un livreur. Le contrôleur ne
/// décide pas des règles de date ou de doublon : il transmet le payload au service de
/// synchronisation, qui applique les contrôles métier avant persistance.
/// </remarks>
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
        // Règle de contrat : un POST de synchronisation doit toujours contenir un corps JSON exploitable.
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
            // Diagnostic production : journaliser la tournée et la date reçues facilite l'analyse d'un incident.
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
