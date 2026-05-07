using API_ASP.NET_Core.Constants;
using API_ASP.NET_Core.Models;
using API_ASP.NET_Core.Repositories;
using API_ASP.NET_Core.Validators;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace API_ASP.NET_Core.Controllers;

/// <summary>
/// Contrôleur utilisé pour consulter et enregistrer les synchronisations envoyées
/// par l'application mobile des livreurs.
/// </summary>
/// <remarks>
/// Ce contrôleur concerne principalement la fin de journée, lorsque l'application mobile
/// renvoie à l'API les données saisies hors connexion par le livreur.
/// </remarks>
[ApiController]
[Route("api/synchronisations")]
[Produces("application/json")]
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
    /// Consulte les synchronisations déjà envoyées.
    /// </summary>
    /// <remarks>
    /// Cette route permet de consulter les synchronisations reçues par l'API.
    ///
    /// Elle est surtout utile pour les tests, le contrôle technique et le suivi des envois.
    ///
    /// Paramètres optionnels :
    /// - dateTournee : filtre sur la date de tournée au format yyyy-MM-dd ;
    /// - codeTournee : filtre sur le code tournée ;
    /// - codeLivreur : filtre sur le code livreur.
    ///
    /// Réponses :
    /// - 200 : liste des synchronisations trouvées ;
    /// - 400 : dateTournee invalide ;
    /// - 500 : erreur technique côté serveur.
    ///
    /// Exemple :
    /// GET /api/synchronisations?dateTournee=2026-04-28&amp;codeTournee=2001&amp;codeLivreur=2
    /// </remarks>
    [HttpGet]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
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
    /// <remarks>
    /// Cette route permet de retrouver le détail complet d'une synchronisation déjà enregistrée.
    ///
    /// Elle est utile pour vérifier ce que l'application mobile a réellement transmis :
    /// en-tête de tournée, livreur, informations mobile, lignes validées, quantités et commentaires.
    ///
    /// Paramètre :
    /// - idTourneeMobile : identifiant technique de la tournée mobile enregistrée.
    ///
    /// Réponses :
    /// - 200 : synchronisation trouvée ;
    /// - 400 : idTourneeMobile invalide ;
    /// - 404 : synchronisation introuvable ;
    /// - 500 : erreur technique côté serveur.
    ///
    /// Exemple :
    /// GET /api/synchronisations/1
    /// </remarks>
    [HttpGet("{idTourneeMobile:long}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
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
    /// Enregistre une synchronisation de tournée envoyée par l'application mobile.
    /// </summary>
    /// <remarks>
    /// Cette route est appelée en fin de journée lorsque le livreur renvoie les données saisies
    /// depuis l'application mobile.
    ///
    /// Elle reçoit le résultat de la tournée après le travail hors connexion :
    /// informations de tournée, livreur, appareil mobile, lignes validées, statuts de passage,
    /// commentaires et quantités livrées ou récupérées.
    ///
    /// Règles métier principales :
    /// - le corps JSON est obligatoire ;
    /// - schemaVersion est obligatoire et doit correspondre à la version supportée par l'API ;
    /// - idSynchronisation est obligatoire, doit être un UUID valide et ne doit pas déjà exister ;
    /// - dateTournee est obligatoire au format yyyy-MM-dd ;
    /// - codeTournee est obligatoire ;
    /// - livreur.codeLivreur est obligatoire ;
    /// - livreur.nomLivreur est obligatoire ;
    /// - mobile.nomAppareil est obligatoire ;
    /// - mobile.versionApplication est obligatoire ;
    /// - mobile.dateChargementMobile est obligatoire ;
    /// - mobile.dateEnvoiMobile est obligatoire ;
    /// - la liste lignes ne doit pas être vide ;
    /// - chaque idLigneSource est obligatoire ;
    /// - chaque idLigneSource doit être unique dans la requête ;
    /// - ordreArret ne peut pas être négatif ;
    /// - client.numClient est obligatoire ;
    /// - client.nomClient est obligatoire ;
    /// - pointLivraison.codePDL est obligatoire ;
    /// - saisie.statutPassage est obligatoire ;
    /// - le statut A_FAIRE est interdit dans l'envoi final ;
    /// - saisie.estValidee doit être true pour chaque ligne envoyée ;
    /// - saisie.heureValidation est obligatoire pour une ligne validée ;
    /// - un statut NON_FAIT nécessite un commentaire livreur ;
    /// - un statut ANOMALIE nécessite un commentaire livreur ;
    /// - chaque quantité doit avoir un codeArticle ;
    /// - les codes articles ne doivent pas être dupliqués dans une même ligne ;
    /// - quantiteLivree ne peut pas être négative ;
    /// - quantiteRecuperee ne peut pas être négative.
    ///
    /// Réponses :
    /// - 200 : synchronisation enregistrée ;
    /// - 400 : données invalides ou règles métier non respectées ;
    /// - 409 : synchronisation déjà reçue ou tournée déjà envoyée ;
    /// - 500 : erreur technique côté serveur.
    ///
    /// Exemple de corps JSON valide :
    ///
    /// {
    ///   "schemaVersion": "1.1",
    ///   "idSynchronisation": "11111111-1111-1111-1111-111111111111",
    ///   "dateTournee": "2026-04-28",
    ///   "codeTournee": "2001",
    ///   "libelleTournee": "MDR VENDEE",
    ///   "livreur": {
    ///     "codeLivreur": "2",
    ///     "nomLivreur": "DAVID LEBAS"
    ///   },
    ///   "mobile": {
    ///     "nomAppareil": "Samsung A15",
    ///     "versionApplication": "1.0.0",
    ///     "dateChargementMobile": "2026-04-28T07:30:00+02:00",
    ///     "dateEnvoiMobile": "2026-04-28T16:45:00+02:00"
    ///   },
    ///   "commentaireGlobal": null,
    ///   "lignes": [
    ///     {
    ///       "idLigneSource": "2026-04-28|2001|2|1058|1|1",
    ///       "ordreArret": 1,
    ///       "client": {
    ///         "numClient": "1058",
    ///         "nomClient": "EHPAD L EQUAIZIERE",
    ///         "nomAffiche": "EHPAD EQUAIZIERE GARNACHE"
    ///       },
    ///       "pointLivraison": {
    ///         "codePDL": "1",
    ///         "descriptionPDL": "EHPAD EQUAIZIERE GARNACHE"
    ///       },
    ///       "saisie": {
    ///         "precisionLivreur": "2 rolls repris au local arrière",
    ///         "statutPassage": "FAIT",
    ///         "commentaireLivreur": null,
    ///         "heureValidation": "2026-04-28T09:12:00+02:00",
    ///         "estValidee": true,
    ///         "quantites": [
    ///           {
    ///             "codeArticle": "ROLLS",
    ///             "libelle": "Rolls",
    ///             "quantiteLivree": 3,
    ///             "quantiteRecuperee": 2
    ///           },
    ///           {
    ///             "codeArticle": "TAPIS",
    ///             "libelle": "Tapis",
    ///             "quantiteLivree": 1,
    ///             "quantiteRecuperee": 0
    ///           },
    ///           {
    ///             "codeArticle": "SACS",
    ///             "libelle": "Sacs",
    ///             "quantiteLivree": 0,
    ///             "quantiteRecuperee": 0
    ///           }
    ///         ]
    ///       }
    ///     }
    ///   ]
    /// }
    /// </remarks>
    [HttpPost]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
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