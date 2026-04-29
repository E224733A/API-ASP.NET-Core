using Microsoft.AspNetCore.Mvc;
using API_ASP.NET_Core.Models;
using API_ASP.NET_Core.Repositories;

namespace API_ASP.NET_Core.Controllers;

[Route("api/synchronisations")]
[ApiController]
public class SynchronisationsController : ControllerBase
{
    private readonly SynchronisationsRepository _synchronisationsRepository;

    public SynchronisationsController(SynchronisationsRepository synchronisationsRepository)
    {
        _synchronisationsRepository = synchronisationsRepository;
    }

    [HttpPost]
    public async Task<ActionResult<SynchronisationResponse>> CreateSynchronisation(
        [FromBody] SynchronisationTourneeRequest request)
    {
        // Validation du modèle
        var validationErrors = ValidateSynchronisation(request);
        if (validationErrors.Any())
        {
            return BadRequest(new 
            { 
                statut = "VALIDATION_ERROR",
                errors = validationErrors 
            });
        }

        try
        {
            // Vérifier l'unicité de IdSynchronisation
            var exists = await _synchronisationsRepository.SynchronisationExistsAsync(request.IdSynchronisation);
            if (exists)
            {
                return Conflict(new SynchronisationResponse
                {
                    Statut = "CONFLICT",
                    Message = $"Une synchronisation avec l'ID {request.IdSynchronisation} existe déjà."
                });
            }

            await _synchronisationsRepository.SaveSynchronisationAsync(request);

            return Ok(new SynchronisationResponse
            {
                Statut = "SUCCESS",
                Message = "Synchronisation enregistrée avec succès."
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new SynchronisationResponse
            {
                Statut = "ERROR",
                Message = $"Erreur lors de la synchronisation : {ex.Message}"
            });
        }
    }

    private static List<string> ValidateSynchronisation(SynchronisationTourneeRequest request)
    {
        var errors = new List<string>();

        // Validations globales
        if (string.IsNullOrWhiteSpace(request.IdSynchronisation))
            errors.Add("IdSynchronisation est requis.");

        if (string.IsNullOrWhiteSpace(request.CodeTournee))
            errors.Add("CodeTournee est requis.");

        if (request.Livreur == null)
            errors.Add("Livreur est requis.");

        if (request.Mobile == null)
            errors.Add("Mobile est requis.");

        if (request.Lignes == null || !request.Lignes.Any())
            errors.Add("Au moins une ligne de tournée est requise.");

        // Validations par ligne
        foreach (var (ligne, index) in request.Lignes.Select((l, i) => (l, i)))
        {
            var saisie = ligne.Saisie;
            if (saisie == null)
            {
                errors.Add($"Ligne {index + 1} : La saisie est requise.");
                continue;
            }

            // Vérifier les quantités non négatives
            if (saisie.NbExpes < 0)
                errors.Add($"Ligne {index + 1} : nbExpes ne peut pas être négatif.");

            if (saisie.NbRolls < 0)
                errors.Add($"Ligne {index + 1} : nbRolls ne peut pas être négatif.");

            if (saisie.NbVetements < 0)
                errors.Add($"Ligne {index + 1} : nbVetements ne peut pas être négatif.");

            if (saisie.NbTapis < 0)
                errors.Add($"Ligne {index + 1} : nbTapis ne peut pas être négatif.");

            if (saisie.NbSacs < 0)
                errors.Add($"Ligne {index + 1} : nbSacs ne peut pas être négatif.");

            if (saisie.NbRecuperes < 0)
                errors.Add($"Ligne {index + 1} : nbRecuperes ne peut pas être négatif.");

            // Vérifier estValidee = true
            if (!saisie.EstValidee)
                errors.Add($"Ligne {index + 1} : estValidee doit être true.");

            // Vérifier les règles par statut
            switch (saisie.StatutPassage)
            {
                case "FAIT":
                    if (string.IsNullOrWhiteSpace(saisie.HeureValidation))
                        errors.Add($"Ligne {index + 1} : heureValidation est obligatoire pour le statut FAIT.");
                    break;

                case "NON_FAIT":
                    if (string.IsNullOrWhiteSpace(saisie.CommentaireLivreur))
                        errors.Add($"Ligne {index + 1} : commentaireLivreur est obligatoire pour le statut NON_FAIT.");
                    if (string.IsNullOrWhiteSpace(saisie.HeureValidation))
                        errors.Add($"Ligne {index + 1} : heureValidation est obligatoire pour le statut NON_FAIT.");
                    break;

                case "ANOMALIE":
                    if (string.IsNullOrWhiteSpace(saisie.CommentaireLivreur))
                        errors.Add($"Ligne {index + 1} : commentaireLivreur est obligatoire pour le statut ANOMALIE.");
                    if (string.IsNullOrWhiteSpace(saisie.HeureValidation))
                        errors.Add($"Ligne {index + 1} : heureValidation est obligatoire pour le statut ANOMALIE.");
                    break;

                default:
                    errors.Add($"Ligne {index + 1} : statutPassage '{saisie.StatutPassage}' invalide. Utilisez FAIT, NON_FAIT ou ANOMALIE.");
                    break;
            }
        }

        return errors;
    }
}
