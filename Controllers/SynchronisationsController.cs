using API_ASP.NET_Core.Models;
using API_ASP.NET_Core.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace API_ASP.NET_Core.Controllers;

[ApiController]
[Route("api/synchronisations")]
public sealed class SynchronisationsController : ControllerBase
{
    private readonly SynchronisationService _synchronisationService;
    private readonly ILogger<SynchronisationsController> _logger;

    public SynchronisationsController(
        SynchronisationService synchronisationService,
        ILogger<SynchronisationsController> logger)
    {
        _synchronisationService = synchronisationService;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> PostSynchronisation(
        [FromBody] SynchronisationTourneeRequest? request,
        CancellationToken cancellationToken)
    {
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
        catch (SqlException exception) when (exception.Number is 2601 or 2627)
        {
            /*
             * Sécurité finale contre les doubles envois.
             *
             * Même si le service contrôle avant insertion, deux requêtes peuvent
             * arriver presque en même temps. La contrainte SQL unique filtrée
             * reste donc la protection définitive :
             *
             * UX_Mobile_Tournee_EnvoiUnique
             * DateTournee + CodeTournee
             * WHERE StatutSynchronisation = 'ENVOYEE'
             */
            _logger.LogWarning(
                exception,
                "Double envoi détecté par la contrainte SQL pour la tournée {CodeTournee} du {DateTournee}.",
                request.CodeTournee,
                request.DateTournee);

            return Conflict(new
            {
                statut = "CONFLICT",
                code = "TOURNEE_ALREADY_SENT",
                message = "Cette tournée a déjà été envoyée pour cette date.",
                dateTournee = FormatDateTournee(request.DateTournee),
                codeTournee = request.CodeTournee
            });
        }
        catch (Exception exception)
        {
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

    private static string FormatDateTournee(object? dateTournee)
    {
        if (dateTournee is DateTime dateTime)
        {
            return dateTime.ToString("yyyy-MM-dd");
        }

        if (DateTime.TryParse(Convert.ToString(dateTournee), out var parsedDate))
        {
            return parsedDate.ToString("yyyy-MM-dd");
        }

        return Convert.ToString(dateTournee) ?? string.Empty;
    }
}
