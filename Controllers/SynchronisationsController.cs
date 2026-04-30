using Microsoft.AspNetCore.Mvc;
using API_ASP.NET_Core.Models;
using API_ASP.NET_Core.Repositories;

namespace API_ASP.NET_Core.Controllers;

/// <summary>
/// Contrôleur HTTP pour recevoir les synchronisations de tournée.
/// </summary>
[Route("api/synchronisations")]
[ApiController]
public class SynchronisationsController : ControllerBase
{
    private readonly SynchronisationsRepository _synchronisationsRepository;

    public SynchronisationsController(SynchronisationsRepository synchronisationsRepository)
    {
        _synchronisationsRepository = synchronisationsRepository;
    }

    /// <summary>
    /// Envoie une synchronisation de tournée.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<SynchronisationResponse>> CreateSynchronisation(
        [FromBody] SynchronisationTourneeRequest request)
    {
        // Valider l’objet
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
            // Vérifier l'unicité dans Mobile_Tournee (via repository)
            var exists = await _synchronisationsRepository.SynchronisationExistsAsync(request.IdSynchronisation);
            if (exists)
            {
                return Conflict(new SynchronisationResponse
                {
                    Statut = "CONFLICT",
                    Message = $"Une synchronisation avec l'ID {request.IdSynchronisation} existe déjà."
                });
            }

            // Sauvegarder la synchronisation
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

        // IdSynchronisation requis et doit être un GUID
        if (string.IsNullOrWhiteSpace(request.IdSynchronisation))
            errors.Add("IdSynchronisation est requis.");
        else if (!Guid.TryParse(request.IdSynchronisation, out _))
            errors.Add("IdSynchronisation doit être un GUID valide.");

        // DateTournee requise et au format YYYY-MM-DD
        if (string.IsNullOrWhiteSpace(request.DateTournee))
            errors.Add("DateTournee est requise.");
        else if (!DateTime.TryParse(request.DateTournee, out _))
            errors.Add("DateTournee est invalide. Format attendu : YYYY-MM-DD.");

        if (string.IsNullOrWhiteSpace(request.CodeTournee))
            errors.Add("CodeTournee est requis.");

        if (request.Livreur == null)
        {
            errors.Add("Livreur est requis.");
        }
        else
        {
            if (string.IsNullOrWhiteSpace(request.Livreur.CodeLivreur))
                errors.Add("Livreur.CodeLivreur est requis.");

            if (string.IsNullOrWhiteSpace(request.Livreur.NomLivreur))
                errors.Add("Livreur.NomLivreur est requis.");
        }

        if (request.Mobile == null)
        {
            errors.Add("Mobile est requis.");
        }
        else
        {
            if (string.IsNullOrWhiteSpace(request.Mobile.NomAppareil))
                errors.Add("Mobile.NomAppareil est requis.");

            if (string.IsNullOrWhiteSpace(request.Mobile.VersionApplication))
                errors.Add("Mobile.VersionApplication est requis.");

            if (!string.IsNullOrWhiteSpace(request.Mobile.DateChargementMobile) &&
                !DateTime.TryParse(request.Mobile.DateChargementMobile, out _))
                errors.Add("Mobile.DateChargementMobile est invalide.");

            if (!string.IsNullOrWhiteSpace(request.Mobile.DateEnvoiMobile) &&
                !DateTime.TryParse(request.Mobile.DateEnvoiMobile, out _))
                errors.Add("Mobile.DateEnvoiMobile est invalide.");
        }

        // Vérifier les lignes
        if (request.Lignes == null || !request.Lignes.Any())
        {
            errors.Add("Au moins une ligne de tournée est requise.");
            return errors;
        }

        foreach (var (ligne, index) in request.Lignes.Select((l, i) => (l, i)))
        {
            var iLigne = index + 1;
            if (ligne == null)
            {
                errors.Add($"Ligne {iLigne} : la ligne est requise.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(ligne.IdLigneSource))
                errors.Add($"Ligne {iLigne} : IdLigneSource est requis.");

            if (ligne.OrdreArret < 0)
                errors.Add($"Ligne {iLigne} : OrdreArret ne peut pas être négatif.");

            if (ligne.Client == null)
            {
                errors.Add($"Ligne {iLigne} : Client est requis.");
            }
            else
            {
                if (string.IsNullOrWhiteSpace(ligne.Client.NumClient))
                    errors.Add($"Ligne {iLigne} : Client.NumClient est requis.");

                if (string.IsNullOrWhiteSpace(ligne.Client.NomClient))
                    errors.Add($"Ligne {iLigne} : Client.NomClient est requis.");
            }

            if (ligne.PointLivraison == null)
            {
                errors.Add($"Ligne {iLigne} : PointLivraison est requis.");
            }
            else
            {
                if (string.IsNullOrWhiteSpace(ligne.PointLivraison.CodePDL))
                    errors.Add($"Ligne {iLigne} : PointLivraison.CodePDL est requis.");

                if (string.IsNullOrWhiteSpace(ligne.PointLivraison.DescriptionPDL))
                    errors.Add($"Ligne {iLigne} : PointLivraison.DescriptionPDL est requis.");
            }

            var saisie = ligne.Saisie;
            if (saisie == null)
            {
                errors.Add($"Ligne {iLigne} : Saisie est requise.");
                continue;
            }

            // Quantités non négatives
            if (saisie.NbExpes < 0) errors.Add($"Ligne {iLigne} : NbExpes ne peut pas être négatif.");
            if (saisie.NbRolls < 0) errors.Add($"Ligne {iLigne} : NbRolls ne peut pas être négatif.");
            if (saisie.NbVetements < 0) errors.Add($"Ligne {iLigne} : NbVetements ne peut pas être négatif.");
            if (saisie.NbTapis < 0) errors.Add($"Ligne {iLigne} : NbTapis ne peut pas être négatif.");
            if (saisie.NbSacs < 0) errors.Add($"Ligne {iLigne} : NbSacs ne peut pas être négatif.");
            if (saisie.NbRecuperes < 0) errors.Add($"Ligne {iLigne} : NbRecuperes ne peut pas être négatif.");

            // estValidee doit être true
            if (!saisie.EstValidee)
                errors.Add($"Ligne {iLigne} : EstValidee doit être true.");

            // Statut de passage et règles associées
            if (string.IsNullOrWhiteSpace(saisie.StatutPassage))
            {
                errors.Add($"Ligne {iLigne} : StatutPassage est requis.");
            }
            else if (saisie.StatutPassage != "FAIT" &&
                     saisie.StatutPassage != "NON_FAIT" &&
                     saisie.StatutPassage != "ANOMALIE")
            {
                errors.Add($"Ligne {iLigne} : StatutPassage '{saisie.StatutPassage}' est invalide. Valeurs autorisées : FAIT, NON_FAIT, ANOMALIE.");
            }

            // heureValidation obligatoire et valide
            if (string.IsNullOrWhiteSpace(saisie.HeureValidation))
            {
                errors.Add($"Ligne {iLigne} : HeureValidation est obligatoire.");
            }
            else if (!DateTime.TryParse(saisie.HeureValidation, out _))
            {
                errors.Add($"Ligne {iLigne} : HeureValidation est invalide.");
            }

            // Commentaire obligatoire pour NON_FAIT ou ANOMALIE
            if ((saisie.StatutPassage == "NON_FAIT" || saisie.StatutPassage == "ANOMALIE") &&
                string.IsNullOrWhiteSpace(saisie.CommentaireLivreur))
            {
                errors.Add($"Ligne {iLigne} : CommentaireLivreur est obligatoire pour le statut {saisie.StatutPassage}.");
            }
        }

        return errors;
    }
}