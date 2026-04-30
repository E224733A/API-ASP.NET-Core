using API_ASP.NET_Core.Models;
using API_ASP.NET_Core.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace API_ASP.NET_Core.Controllers;

[ApiController]
[Route("api/synchronisations")]
public class SynchronisationsController : ControllerBase
{
    private readonly SynchronisationsRepository _repository;

    public SynchronisationsController(SynchronisationsRepository repository)
    {
        _repository = repository;
    }

    /// <summary>
    /// Consulte les synchronisations envoyées.
    /// Filtres possibles :
    /// - dateTournee
    /// - codeTournee
    /// - codeLivreur
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
                    statut = "VALIDATION_ERROR",
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
            statut = "SUCCESS",
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
        var synchronisation = await _repository.GetSynchronisationByIdAsync(idTourneeMobile);

        if (synchronisation is null)
        {
            return NotFound(new
            {
                statut = "NOT_FOUND",
                message = $"Aucune synchronisation trouvée avec l'ID {idTourneeMobile}."
            });
        }

        return Ok(new
        {
            statut = "SUCCESS",
            synchronisation
        });
    }

    /// <summary>
    /// Enregistre une synchronisation de tournée envoyée par le mobile.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> PostSynchronisation([FromBody] SynchronisationTourneeRequest request)
    {
        var errors = ValidateSynchronisationRequest(request);

        if (errors.Count > 0)
        {
            return BadRequest(new
            {
                statut = "VALIDATION_ERROR",
                errors
            });
        }

        try
        {
            if (await _repository.SynchronisationExistsAsync(request.IdSynchronisation))
            {
                return Conflict(new
                {
                    statut = "CONFLICT",
                    message = $"Une synchronisation avec l'ID {request.IdSynchronisation} existe déjà."
                });
            }

            await _repository.SaveSynchronisationAsync(request);

            return Ok(new
            {
                statut = "SUCCESS",
                message = "Synchronisation enregistrée avec succès."
            });
        }
        catch (SqlException ex) when (ex.Number is 2601 or 2627)
        {
            return Conflict(new
            {
                statut = "CONFLICT",
                message = "Cette tournée a déjà été synchronisée."
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                statut = "ERROR",
                message = $"Erreur lors de la synchronisation : {ex.Message}"
            });
        }
    }

    private static List<string> ValidateSynchronisationRequest(SynchronisationTourneeRequest? request)
    {
        var errors = new List<string>();

        if (request is null)
        {
            errors.Add("Le corps de la requête est obligatoire.");
            return errors;
        }

        if (string.IsNullOrWhiteSpace(request.SchemaVersion))
            errors.Add("SchemaVersion est obligatoire.");
        else if (request.SchemaVersion != "1.0")
            errors.Add($"SchemaVersion {request.SchemaVersion} n'est pas supportée.");

        if (string.IsNullOrWhiteSpace(request.IdSynchronisation))
        {
            errors.Add("IdSynchronisation est obligatoire.");
        }
        else if (!Guid.TryParse(request.IdSynchronisation, out _))
        {
            errors.Add("IdSynchronisation doit être un UUID valide.");
        }

        if (string.IsNullOrWhiteSpace(request.DateTournee))
        {
            errors.Add("DateTournee est obligatoire.");
        }
        else if (!DateTime.TryParse(request.DateTournee, out _))
        {
            errors.Add("DateTournee est invalide.");
        }

        if (string.IsNullOrWhiteSpace(request.CodeTournee))
            errors.Add("CodeTournee est obligatoire.");

        if (request.Livreur is null)
        {
            errors.Add("Livreur est obligatoire.");
        }
        else if (string.IsNullOrWhiteSpace(request.Livreur.CodeLivreur))
        {
            errors.Add("Livreur.CodeLivreur est obligatoire.");
        }

        if (request.Lignes is null || request.Lignes.Count == 0)
        {
            errors.Add("La liste des lignes ne doit pas être vide.");
            return errors;
        }

        var idsLigneSource = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < request.Lignes.Count; i++)
        {
            var numeroLigne = i + 1;
            var ligne = request.Lignes[i];
            var saisie = ligne.Saisie;

            if (string.IsNullOrWhiteSpace(ligne.IdLigneSource))
            {
                errors.Add($"Ligne {numeroLigne} : IdLigneSource est obligatoire.");
            }
            else if (!idsLigneSource.Add(ligne.IdLigneSource))
            {
                errors.Add($"Ligne {numeroLigne} : IdLigneSource est dupliqué dans la requête.");
            }

            if (saisie is null)
            {
                errors.Add($"Ligne {numeroLigne} : Saisie est obligatoire.");
                continue;
            }

            if ((saisie.NbExpes) < 0)
                errors.Add($"Ligne {numeroLigne} : NbExpes ne peut pas être négatif.");

            if ((saisie.NbRolls) < 0)
                errors.Add($"Ligne {numeroLigne} : NbRolls ne peut pas être négatif.");

            if ((saisie.NbVetements) < 0)
                errors.Add($"Ligne {numeroLigne} : NbVetements ne peut pas être négatif.");

            if ((saisie.NbTapis) < 0)
                errors.Add($"Ligne {numeroLigne} : NbTapis ne peut pas être négatif.");

            if ((saisie.NbSacs) < 0)
                errors.Add($"Ligne {numeroLigne} : NbSacs ne peut pas être négatif.");

            if ((saisie.NbRecuperes) < 0)
                errors.Add($"Ligne {numeroLigne} : NbRecuperes ne peut pas être négatif.");

            var statut = saisie.StatutPassage?.Trim().ToUpperInvariant();

            if (string.IsNullOrWhiteSpace(statut))
            {
                errors.Add($"Ligne {numeroLigne} : StatutPassage est obligatoire.");
            }
            else if (statut is not "A_FAIRE" and not "FAIT" and not "NON_FAIT" and not "ANOMALIE")
            {
                errors.Add($"Ligne {numeroLigne} : StatutPassage doit être A_FAIRE, FAIT, NON_FAIT ou ANOMALIE.");
            }
            else if (statut == "A_FAIRE")
            {
                errors.Add($"Ligne {numeroLigne} : A_FAIRE est interdit dans l'envoi final.");
            }

            if (!saisie.EstValidee)
            {
                errors.Add($"Ligne {numeroLigne} : EstValidee doit être true pour l'envoi final.");
            }

            if (saisie.EstValidee && string.IsNullOrWhiteSpace(saisie.HeureValidation))
            {
                errors.Add($"Ligne {numeroLigne} : HeureValidation est obligatoire.");
            }

            if (!string.IsNullOrWhiteSpace(saisie.HeureValidation) &&
                !DateTime.TryParse(saisie.HeureValidation, out _) &&
                !TimeSpan.TryParse(saisie.HeureValidation, out _))
            {
                errors.Add($"Ligne {numeroLigne} : HeureValidation est invalide.");
            }

            if ((statut == "NON_FAIT" || statut == "ANOMALIE") &&
                string.IsNullOrWhiteSpace(saisie.CommentaireLivreur))
            {
                errors.Add($"Ligne {numeroLigne} : CommentaireLivreur est obligatoire pour le statut {statut}.");
            }
        }

        return errors;
    }
}