using API_ASP.NET_Core.Constants;
using API_ASP.NET_Core.Models;
using API_ASP.NET_Core.Repositories;
using API_ASP.NET_Core.Validators;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace API_ASP.NET_Core.Controllers;

[ApiController]
[Route("api/synchronisations")]
public sealed class SynchronisationsController : ControllerBase
{
    private readonly SynchronisationsRepository _repository;
    private readonly SynchronisationTourneeValidator _validator;
    private readonly ILogger<SynchronisationsController> _logger;
    private readonly IWebHostEnvironment _environment;

    public SynchronisationsController(
        SynchronisationsRepository repository,
        SynchronisationTourneeValidator validator,
        ILogger<SynchronisationsController> logger,
        IWebHostEnvironment environment)
    {
        _repository = repository;
        _validator = validator;
        _logger = logger;
        _environment = environment;
    }

    /// <summary>
    /// Consulte les synchronisations envoyées.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetSynchronisations(
        [FromQuery] string? dateTournee,
        [FromQuery] string? codeTournee,
        [FromQuery] string? codeLivreur)
    {
        DateTime? parsedDateTournee = null;

        if (!string.IsNullOrWhiteSpace(dateTournee))
        {
            if (!DateTime.TryParse(dateTournee, out var date))
            {
                return BadRequest(new
                {
                    statut = ApiErrorCodes.ValidationError,
                    errors = new[]
                    {
                        "Le paramètre dateTournee est invalide. Format attendu : yyyy-MM-dd."
                    }
                });
            }

            parsedDateTournee = date.Date;
        }

        var synchronisations = await _repository.GetSynchronisationsAsync(
            parsedDateTournee,
            codeTournee,
            codeLivreur);

        return Ok(new
        {
            statut = ApiErrorCodes.Success,
            count = synchronisations.Count,
            synchronisations
        });
    }

    /// <summary>
    /// Consulte le détail complet d'une synchronisation envoyée.
    /// </summary>
    [HttpGet("{idTourneeMobile:long}")]
    public async Task<IActionResult> GetSynchronisationById(long idTourneeMobile)
    {
        if (idTourneeMobile <= 0)
        {
            return BadRequest(new
            {
                statut = ApiErrorCodes.ValidationError,
                errors = new[]
                {
                    "IdTourneeMobile doit être supérieur à 0."
                }
            });
        }

        var synchronisation = await _repository.GetSynchronisationByIdAsync(idTourneeMobile);

        if (synchronisation is null)
        {
            return NotFound(new
            {
                statut = ApiErrorCodes.NotFound,
                message = $"Aucune synchronisation trouvée avec l'ID {idTourneeMobile}."
            });
        }

        return Ok(new
        {
            statut = ApiErrorCodes.Success,
            synchronisation
        });
    }

    /// <summary>
    /// Enregistre une synchronisation de tournée envoyée par le mobile.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> PostSynchronisation([FromBody] SynchronisationTourneeRequest request)
    {
        var errors = _validator.Validate(request);

        if (errors.Count > 0)
        {
            return BadRequest(new
            {
                statut = ApiErrorCodes.ValidationError,
                errors
            });
        }

        try
        {
            if (await _repository.SynchronisationExistsAsync(request.IdSynchronisation))
            {
                return Conflict(new
                {
                    statut = ApiErrorCodes.Conflict,
                    code = ApiErrorCodes.SynchronisationAlreadyExists,
                    message = "Cette synchronisation a déjà été reçue."
                });
            }

            await _repository.SaveSynchronisationAsync(request);

            return Ok(new
            {
                statut = ApiErrorCodes.Success,
                message = "Synchronisation enregistrée avec succès."
            });
        }
        catch (SqlException ex) when (ex.Number is 2601 or 2627)
        {
            _logger.LogWarning(
                ex,
                "Conflit SQL pendant la synchronisation. IdSynchronisation={IdSynchronisation}, DateTournee={DateTournee}, CodeTournee={CodeTournee}, CodeLivreur={CodeLivreur}",
                request.IdSynchronisation,
                request.DateTournee,
                request.CodeTournee,
                request.Livreur?.CodeLivreur);

            return Conflict(new
            {
                statut = ApiErrorCodes.Conflict,
                code = ApiErrorCodes.TourneeAlreadySent,
                message = "Cette tournée a déjà été envoyée pour ce livreur et cette date."
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new
            {
                statut = ApiErrorCodes.ValidationError,
                errors = new[]
                {
                    ex.Message
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Erreur technique pendant la synchronisation. IdSynchronisation={IdSynchronisation}, DateTournee={DateTournee}, CodeTournee={CodeTournee}, CodeLivreur={CodeLivreur}",
                request.IdSynchronisation,
                request.DateTournee,
                request.CodeTournee,
                request.Livreur?.CodeLivreur);

            var message = _environment.IsDevelopment()
                ? $"Erreur technique lors de la synchronisation : {ex.Message}"
                : "Erreur technique lors de la synchronisation.";

            return StatusCode(500, new
            {
                statut = ApiErrorCodes.Error,
                code = ApiErrorCodes.TechnicalError,
                message
            });
        }
    }
}