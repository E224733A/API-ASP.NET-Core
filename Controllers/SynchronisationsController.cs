using API_ASP.NET_Core.Models;
using API_ASP.NET_Core.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace API_ASP.NET_Core.Controllers;

[ApiController]
[Route("api/synchronisations")]
public class SynchronisationsController : ControllerBase
{
    private static readonly HashSet<string> StatutsAutorises = new(StringComparer.OrdinalIgnoreCase)
    {
        "A_FAIRE",
        "FAIT",
        "NON_FAIT",
        "ANOMALIE"
    };

    private readonly SynchronisationsRepository _repository;

    public SynchronisationsController(SynchronisationsRepository repository)
    {
        _repository = repository;
    }

    /// <summary>
    /// Consulte les synchronisations envoyées.
    ///
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
        if (idTourneeMobile <= 0)
        {
            return BadRequest(new
            {
                statut = "VALIDATION_ERROR",
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
    ///
    /// Version officielle attendue :
    /// - schemaVersion = 1.1
    /// - saisie.quantites[]
    /// - quantiteLivree
    /// - quantiteRecuperee
    ///
    /// L'ancien format NbRolls / NbTapis / NbSacs / NbRecuperes
    /// n'est plus accepté dans la logique de validation.
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
        catch (ArgumentException ex)
        {
            return BadRequest(new
            {
                statut = "VALIDATION_ERROR",
                errors = new[]
                {
                    ex.Message
                }
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

        ValidateHeader(request, errors);

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

            if (ligne is null)
            {
                errors.Add($"Ligne {numeroLigne} : la ligne est obligatoire.");
                continue;
            }

            ValidateLigne(ligne, numeroLigne, idsLigneSource, errors);
        }

        return errors;
    }

    private static void ValidateHeader(SynchronisationTourneeRequest request, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(request.SchemaVersion))
        {
            errors.Add("SchemaVersion est obligatoire.");
        }
        else if (request.SchemaVersion.Trim() != "1.1")
        {
            errors.Add($"SchemaVersion {request.SchemaVersion} n'est pas supportée. Version attendue : 1.1.");
        }

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
            errors.Add("DateTournee est invalide. Format attendu : yyyy-MM-dd.");
        }

        if (string.IsNullOrWhiteSpace(request.CodeTournee))
        {
            errors.Add("CodeTournee est obligatoire.");
        }

        if (request.Livreur is null)
        {
            errors.Add("Livreur est obligatoire.");
        }
        else
        {
            if (string.IsNullOrWhiteSpace(request.Livreur.CodeLivreur))
            {
                errors.Add("Livreur.CodeLivreur est obligatoire.");
            }

            if (string.IsNullOrWhiteSpace(request.Livreur.NomLivreur))
            {
                errors.Add("Livreur.NomLivreur est obligatoire.");
            }
        }

        ValidateMobile(request.Mobile, errors);
    }

    private static void ValidateMobile(
        SynchronisationMobileRequest? mobile,
        List<string> errors)
    {
        if (mobile is null)
        {
            errors.Add("Mobile est obligatoire.");
            return;
        }

        if (string.IsNullOrWhiteSpace(mobile.NomAppareil))
        {
            errors.Add("Mobile.NomAppareil est obligatoire.");
        }

        if (string.IsNullOrWhiteSpace(mobile.VersionApplication))
        {
            errors.Add("Mobile.VersionApplication est obligatoire.");
        }

        if (string.IsNullOrWhiteSpace(mobile.DateChargementMobile))
        {
            errors.Add("Mobile.DateChargementMobile est obligatoire.");
        }
        else
        {
            ValidateDateTimeOffsetOrDateTime(
                mobile.DateChargementMobile,
                "Mobile.DateChargementMobile",
                errors);
        }

        if (string.IsNullOrWhiteSpace(mobile.DateEnvoiMobile))
        {
            errors.Add("Mobile.DateEnvoiMobile est obligatoire.");
        }
        else
        {
            ValidateDateTimeOffsetOrDateTime(
                mobile.DateEnvoiMobile,
                "Mobile.DateEnvoiMobile",
                errors);
        }
    }

    private static void ValidateLigne(
        SynchronisationLigneRequest ligne,
        int numeroLigne,
        HashSet<string> idsLigneSource,
        List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(ligne.IdLigneSource))
        {
            errors.Add($"Ligne {numeroLigne} : IdLigneSource est obligatoire.");
        }
        else
        {
            var idLigneSource = ligne.IdLigneSource.Trim();

            if (!idsLigneSource.Add(idLigneSource))
            {
                errors.Add($"Ligne {numeroLigne} : IdLigneSource est dupliqué dans la requête.");
            }
        }

        if (ligne.OrdreArret < 0)
        {
            errors.Add($"Ligne {numeroLigne} : OrdreArret ne peut pas être négatif.");
        }

        ValidateClient(ligne, numeroLigne, errors);
        ValidatePointLivraison(ligne, numeroLigne, errors);
        ValidateSaisie(ligne.Saisie, numeroLigne, errors);
    }

    private static void ValidateClient(
        SynchronisationLigneRequest ligne,
        int numeroLigne,
        List<string> errors)
    {
        if (ligne.Client is null)
        {
            errors.Add($"Ligne {numeroLigne} : Client est obligatoire.");
            return;
        }

        if (string.IsNullOrWhiteSpace(ligne.Client.NumClient))
        {
            errors.Add($"Ligne {numeroLigne} : Client.NumClient est obligatoire.");
        }

        if (string.IsNullOrWhiteSpace(ligne.Client.NomClient))
        {
            errors.Add($"Ligne {numeroLigne} : Client.NomClient est obligatoire.");
        }
    }

    private static void ValidatePointLivraison(
        SynchronisationLigneRequest ligne,
        int numeroLigne,
        List<string> errors)
    {
        if (ligne.PointLivraison is null)
        {
            errors.Add($"Ligne {numeroLigne} : PointLivraison est obligatoire.");
            return;
        }

        if (string.IsNullOrWhiteSpace(ligne.PointLivraison.CodePDL))
        {
            errors.Add($"Ligne {numeroLigne} : PointLivraison.CodePDL est obligatoire.");
        }
    }

    private static void ValidateSaisie(
        SynchronisationSaisieRequest? saisie,
        int numeroLigne,
        List<string> errors)
    {
        if (saisie is null)
        {
            errors.Add($"Ligne {numeroLigne} : Saisie est obligatoire.");
            return;
        }

        var statut = saisie.StatutPassage?.Trim().ToUpperInvariant();

        if (string.IsNullOrWhiteSpace(statut))
        {
            errors.Add($"Ligne {numeroLigne} : StatutPassage est obligatoire.");
        }
        else if (!StatutsAutorises.Contains(statut))
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

        if (!string.IsNullOrWhiteSpace(saisie.HeureValidation))
        {
            ValidateDateTimeOffsetOrDateTimeOrTime(
                saisie.HeureValidation,
                $"Ligne {numeroLigne} : HeureValidation",
                errors);
        }

        if ((statut == "NON_FAIT" || statut == "ANOMALIE")
            && string.IsNullOrWhiteSpace(saisie.CommentaireLivreur))
        {
            errors.Add($"Ligne {numeroLigne} : CommentaireLivreur est obligatoire pour le statut {statut}.");
        }

        ValidateQuantites(saisie, numeroLigne, errors);
    }

    private static void ValidateQuantites(
        SynchronisationSaisieRequest saisie,
        int numeroLigne,
        List<string> errors)
    {
        if (saisie.Quantites is null || saisie.Quantites.Count == 0)
        {
            errors.Add($"Ligne {numeroLigne} : Quantites doit contenir au moins un article.");
            return;
        }

        var codesArticles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < saisie.Quantites.Count; i++)
        {
            var numeroArticle = i + 1;
            var quantite = saisie.Quantites[i];

            if (quantite is null)
            {
                errors.Add($"Ligne {numeroLigne}, article {numeroArticle} : l'article est obligatoire.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(quantite.CodeArticle))
            {
                errors.Add($"Ligne {numeroLigne}, article {numeroArticle} : CodeArticle est obligatoire.");
            }
            else
            {
                var codeArticle = quantite.CodeArticle.Trim();

                if (!codesArticles.Add(codeArticle))
                {
                    errors.Add($"Ligne {numeroLigne}, article {numeroArticle} : CodeArticle {codeArticle} est dupliqué.");
                }
            }

            if (quantite.QuantiteLivree < 0)
            {
                errors.Add($"Ligne {numeroLigne}, article {numeroArticle} : QuantiteLivree ne peut pas être négative.");
            }

            if (quantite.QuantiteRecuperee < 0)
            {
                errors.Add($"Ligne {numeroLigne}, article {numeroArticle} : QuantiteRecuperee ne peut pas être négative.");
            }
        }
    }

    private static void ValidateDateTimeOffsetOrDateTime(
        string value,
        string fieldName,
        List<string> errors)
    {
        if (!DateTimeOffset.TryParse(value, out _)
            && !DateTime.TryParse(value, out _))
        {
            errors.Add($"{fieldName} est invalide.");
        }
    }

    private static void ValidateDateTimeOffsetOrDateTimeOrTime(
        string value,
        string fieldName,
        List<string> errors)
    {
        if (!DateTimeOffset.TryParse(value, out _)
            && !DateTime.TryParse(value, out _)
            && !TimeSpan.TryParse(value, out _))
        {
            errors.Add($"{fieldName} est invalide.");
        }
    }
}