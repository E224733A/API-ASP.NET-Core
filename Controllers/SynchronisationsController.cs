using API_ASP.NET_Core.Models;
using API_ASP.NET_Core.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace API_ASP.NET_Core.Controllers;

/// <summary>
/// Contrôleur utilisé pour enregistrer les synchronisations envoyées par l'application mobile des livreurs.
/// </summary>
/// <remarks>
/// Ce contrôleur concerne principalement l'envoi du soir.
///
/// L'application mobile travaille hors connexion pendant la tournée, puis renvoie toutes les données
/// saisies lorsque le livreur revient au dépôt et retrouve le réseau interne.
///
/// La route principale exposée ici est :
/// POST /api/synchronisations
///
/// Elle reçoit le contrat JSON MobileSLI en schemaVersion 1.2.
/// </remarks>
[ApiController]
[Route("api/synchronisations")]
[Produces("application/json")]
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

    /// <summary>
    /// Enregistre l'envoi final d'une tournée mobile.
    /// </summary>
    /// <remarks>
    /// Cette route est appelée en fin de journée par l'application mobile MobileSLI.
    ///
    /// Elle reçoit la tournée réalisée par le livreur après le travail hors connexion :
    /// informations générales de tournée, livreur, appareil mobile, lignes de tournée,
    /// statuts de passage, commentaires terrain et quantités par article.
    ///
    /// Règles métier principales attendues :
    /// - le corps JSON est obligatoire ;
    /// - schemaVersion doit valoir "1.2" ;
    /// - idSynchronisation doit être renseigné et unique ;
    /// - dateTournee doit être renseignée ;
    /// - codeTournee doit être renseigné ;
    /// - livreur.codeLivreur doit être renseigné ;
    /// - mobile.nomAppareil et mobile.versionApplication doivent être renseignés ;
    /// - lignes[] ne doit pas être vide ;
    /// - idLigneSource doit être renseigné pour chaque ligne ;
    /// - idLigneSource doit être unique dans l'envoi ;
    /// - A_FAIRE est interdit dans l'envoi final ;
    /// - NON_FAIT et ANOMALIE nécessitent un commentaireLivreur ;
    /// - estValidee doit être true pour chaque ligne envoyée ;
    /// - heureValidation doit être renseignée pour chaque ligne validée ;
    /// - saisie.quantites[] doit contenir au moins un article ;
    /// - codeArticle doit être renseigné et unique dans une même ligne ;
    /// - quantiteLivreePrevue peut être null, mais ne peut pas être négative si elle est renseignée ;
    /// - quantiteLivree et quantiteRecuperee doivent être positives ou nulles.
    ///
    /// Exemple de corps JSON valide :
    ///
    /// {
    ///   "schemaVersion": "1.2",
    ///   "idSynchronisation": "11111111-1111-1111-1111-111111111111",
    ///   "dateTournee": "2026-05-07",
    ///   "codeTournee": "4006",
    ///   "libelleTournee": "BOUAYE",
    ///   "livreur": {
    ///     "codeLivreur": "2",
    ///     "nomLivreur": "DAVID LEBAS"
    ///   },
    ///   "mobile": {
    ///     "nomAppareil": "Samsung A15",
    ///     "versionApplication": "1.0.0",
    ///     "dateChargementMobile": "2026-05-07T07:30:00+02:00",
    ///     "dateEnvoiMobile": "2026-05-07T16:45:00+02:00"
    ///   },
    ///   "commentaireGlobal": null,
    ///   "lignes": [
    ///     {
    ///       "idLigneSource": "2026-05-07|4006|4|333|341|1",
    ///       "ordreArret": 1,
    ///       "horaire": 1,
    ///       "client": {
    ///         "numClient": "333",
    ///         "nomClient": "HOTEL LE MARTINET",
    ///         "nomAffiche": "HOTEL LE MARTINET"
    ///       },
    ///       "pointLivraison": {
    ///         "codePDL": "341",
    ///         "descriptionPDL": "HOTEL LE MARTINET",
    ///         "adresseLigne1": "PLACE DU GENERAL CHARRETTE",
    ///         "adresseLigne2": null,
    ///         "adresseLigne3": "SARL HOTEL LE MARTINET",
    ///         "ville": "BOUIN",
    ///         "codePostal": "85230"
    ///       },
    ///       "tournee": {
    ///         "codeTournee": "4006",
    ///         "libelleTournee": "BOUAYE",
    ///         "jourTournee": 4,
    ///         "jourLibelle": "Jeudi",
    ///         "schemaLivraison": "1W1"
    ///       },
    ///       "retour": {
    ///         "jourTourneeRetour": 4,
    ///         "jourRetourLibelle": "Jeudi",
    ///         "codeTourneeRetour": "4006",
    ///         "libelleTourneeRetour": "BOUAYE"
    ///       },
    ///       "infosLivreur": {
    ///         "instructions": "CODE 8578 *",
    ///         "commentaireExceptionnel": null,
    ///         "zoneDechargement": null,
    ///         "zoneDechargementAffichee": "4",
    ///         "zone": null,
    ///         "precision": null,
    ///         "cle": null,
    ///         "estFerme": false,
    ///         "dateFermeture": null,
    ///         "motifFermeture": null
    ///       },
    ///       "saisie": {
    ///         "precisionLivreur": "Test API v1.2",
    ///         "statutPassage": "FAIT",
    ///         "commentaireLivreur": null,
    ///         "heureValidation": "2026-05-07T09:12:00+02:00",
    ///         "estValidee": true,
    ///         "quantites": [
    ///           {
    ///             "codeArticle": "ROLLS",
    ///             "libelle": "Rolls",
    ///             "quantiteLivreePrevue": null,
    ///             "quantiteLivree": 1,
    ///             "quantiteRecuperee": 2
    ///           },
    ///           {
    ///             "codeArticle": "TAPIS",
    ///             "libelle": "Tapis",
    ///             "quantiteLivreePrevue": 0,
    ///             "quantiteLivree": 0,
    ///             "quantiteRecuperee": 0
    ///           },
    ///           {
    ///             "codeArticle": "SACS",
    ///             "libelle": "Sacs",
    ///             "quantiteLivreePrevue": 2,
    ///             "quantiteLivree": 3,
    ///             "quantiteRecuperee": 1
    ///           }
    ///         ]
    ///       }
    ///     }
    ///   ]
    /// }
    ///
    /// Réponse possible en cas de succès :
    ///
    /// {
    ///   "code": "SUCCESS",
    ///   "message": "Synchronisation enregistrée avec succès."
    /// }
    ///
    /// Réponse possible en cas de validation invalide :
    ///
    /// {
    ///   "code": "VALIDATION_ERROR",
    ///   "message": "Certaines données sont invalides.",
    ///   "erreurs": [
    ///     {
    ///       "champ": "lignes[0].saisie.statutPassage",
    ///       "message": "Le statut A_FAIRE est interdit dans l'envoi final."
    ///     }
    ///   ]
    /// }
    ///
    /// Réponse possible en cas de double envoi :
    ///
    /// {
    ///   "code": "TOURNEE_ALREADY_SENT",
    ///   "message": "Cette tournée a déjà été envoyée pour cette date.",
    ///   "dateTournee": "2026-05-07",
    ///   "codeTournee": "4006"
    /// }
    /// </remarks>
    /// <param name="request">Corps JSON de synchronisation finale envoyé par l'application mobile.</param>
    /// <param name="cancellationToken">Jeton d'annulation transmis par ASP.NET Core si la requête est interrompue.</param>
    /// <returns>Résultat de l'enregistrement de la synchronisation.</returns>
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> PostSynchronisation(
        [FromBody] SynchronisationTourneeRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequest(new
            {
                code = "VALIDATION_ERROR",
                message = "Le corps JSON de la synchronisation est obligatoire.",
                erreurs = new[]
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
