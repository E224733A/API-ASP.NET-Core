using API_ASP.NET_Core.Models;
using API_ASP.NET_Core.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace API_ASP.NET_Core.Controllers;

/// <summary>
/// Contrôleur du module Expédition.
/// Il expose uniquement deux routes métier :
/// - GET global pour charger toutes les données préparables ;
/// - POST global pour verrouiller et sauvegarder en SQL Server.
/// </summary>
[ApiController]
[Route("api/expedition")]
[Produces("application/json")]
public sealed class ExpeditionController : ControllerBase
{
    private readonly ExpeditionService _expeditionService;
    private readonly ILogger<ExpeditionController> _logger;

    public ExpeditionController(
        ExpeditionService expeditionService,
        ILogger<ExpeditionController> logger)
    {
        _expeditionService = expeditionService;
        _logger = logger;
    }

    /// <summary>
    /// Charge en une seule fois toutes les données nécessaires à la préparation Expédition.
    /// </summary>
    /// <remarks>
    /// La date préparable est calculée côté API selon le fuseau métier Europe/Paris.
    /// En première version, elle correspond au lendemain calendaire.
    /// L'application web Expédition doit ensuite travailler sur son brouillon SQLite local
    /// sans rappeler l'API à chaque clic ou modification.
    /// </remarks>
    [HttpGet("preparations/a-preparer")]
    [ProducesResponseType(typeof(ExpeditionPreparationsApreparerResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ExpeditionErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ExpeditionPreparationsApreparerResponse>> GetPreparationsApreparer(
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _expeditionService.GetPreparationsApreparerAsync(cancellationToken);
            return Ok(response);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Erreur technique pendant le chargement des données Expédition à préparer.");

            return StatusCode(StatusCodes.Status500InternalServerError, new ExpeditionErrorResponse
            {
                Statut = "ERROR",
                Code = "TECHNICAL_ERROR",
                Message = "Une erreur technique est survenue pendant le chargement des données Expédition.",
                Errors = Array.Empty<string>()
            });
        }
    }

    /// <summary>
    /// Verrouille un lot complet de préparations Expédition.
    /// </summary>
    /// <remarks>
    /// Cette route est appelée automatiquement par l'application web Expédition autour de 00:05.
    /// Le serveur web déclenche, mais l'API vérifie : date métier, idLotVerrouillage, idLigneSource,
    /// quantités, commentaires et absence de données de récupération.
    /// Le POST est idempotent : un même idLotVerrouillage traité avec succès ne crée pas de doublon.
    /// </remarks>
    [HttpPost("preparations/verrouiller")]
    [ProducesResponseType(typeof(ExpeditionVerrouillageSuccessResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ExpeditionErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ExpeditionErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ExpeditionErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> VerrouillerPreparations(
        [FromBody] ExpeditionVerrouillageRequest? request,
        CancellationToken cancellationToken)
    {
        var adresseIp = HttpContext.Connection.RemoteIpAddress?.ToString();

        try
        {
            var result = await _expeditionService.VerrouillerPreparationsAsync(
                request,
                adresseIp,
                cancellationToken);

            return StatusCode(result.StatusCode, result.Body);
        }
        catch (SqlException exception) when (exception.Number is 2601 or 2627)
        {
            _logger.LogWarning(exception,
                "Conflit SQL pendant le verrouillage Expédition du lot {IdLotVerrouillage}.",
                request?.IdLotVerrouillage);

            return Conflict(new ExpeditionErrorResponse
            {
                Statut = "CONFLICT",
                Code = "SQL_UNIQUE_CONSTRAINT",
                Message = "Le lot ou une tournée est déjà verrouillé en base.",
                Errors = new[]
                {
                    "La contrainte SQL a empêché la création d'un doublon."
                }
            });
        }
        catch (Exception exception)
        {
            _logger.LogError(exception,
                "Erreur technique pendant le verrouillage Expédition du lot {IdLotVerrouillage}.",
                request?.IdLotVerrouillage);

            return StatusCode(StatusCodes.Status500InternalServerError, new ExpeditionErrorResponse
            {
                Statut = "ERROR",
                Code = "TECHNICAL_ERROR",
                Message = "Une erreur technique est survenue pendant le verrouillage Expédition.",
                Errors = Array.Empty<string>()
            });
        }
    }
}
