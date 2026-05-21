using API_ASP.NET_Core.Constants;
using API_ASP.NET_Core.Models;
using API_ASP.NET_Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace API_ASP.NET_Core.Controllers;

/// <summary>
/// Contrôleur utilisé par l'application mobile pour consulter les tournées disponibles
/// et charger le détail complet d'une tournée sélectionnée.
/// </summary>
/// <remarks>
/// Ce contrôleur correspond au flux du matin : le livreur s'identifie, consulte les tournées
/// disponibles pour la date métier calculée côté API, puis charge une tournée complète
/// dans l'application mobile.
/// Après ce chargement, le mobile peut fonctionner hors connexion grâce à son stockage local SQLite.
/// </remarks>
[ApiController]
[Route("api/tournees")]
[Produces("application/json")]
public class TourneesController : ControllerBase
{
    private readonly TourneesService _tourneesService;
    private readonly DateMetierService _dateMetierService;

    public TourneesController(
        TourneesService service,
        DateMetierService dateMetierService)
    {
        _tourneesService = service;
        _dateMetierService = dateMetierService;
    }

    /// <summary>
    /// Liste les tournées disponibles pour la date métier serveur et un livreur.
    /// </summary>
    /// <remarks>
    /// Cette route est utilisée par l'écran de choix de tournée.
    ///
    /// Elle retourne une réponse enveloppée contenant :
    /// - schemaVersion ;
    /// - dateTournee ;
    /// - dateModifiable ;
    /// - livreur ;
    /// - tournees[].
    ///
    /// La date n'est pas acceptée depuis le mobile.
    /// Elle est toujours calculée par l'API avec le fuseau métier Europe/Paris.
    ///
    /// Exemple :
    /// GET /api/tournees/disponibles?codeLivreur=2
    /// </remarks>
    /// <param name="codeLivreur">Code métier du livreur. Exemple : 2.</param>
    /// <returns>Liste des tournées disponibles pour le livreur et la date serveur autorisée.</returns>
    [HttpGet("disponibles")]
    [ProducesResponseType(typeof(TourneesDisponiblesResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiValidationErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiNotFoundResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiTechnicalErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<TourneesDisponiblesResponseDto>> GetTourneesDisponibles(
        [FromQuery] string? codeLivreur)
    {
        var parametreDateInterdit = GetParametreDateInterdit();

        if (parametreDateInterdit is not null)
        {
            return BadRequest(BuildDateQueryForbiddenResponse(parametreDateInterdit));
        }

        if (string.IsNullOrWhiteSpace(codeLivreur))
        {
            return BadRequest(new ApiValidationErrorResponse
            {
                Statut = ApiErrorCodes.ValidationError,
                Errors = new[]
                {
                    "Paramètre codeLivreur obligatoire."
                }
            });
        }

        var date = _dateMetierService.GetDateTourneeAutorisee();
        var codeLivreurNormalise = codeLivreur.Trim();

        var response = await _tourneesService.GetTourneesDisponiblesAsync(date, codeLivreurNormalise);

        if (response is null)
        {
            return NotFound(new ApiNotFoundResponse
            {
                Statut = ApiErrorCodes.NotFound,
                Message = $"Livreur introuvable : {codeLivreurNormalise}"
            });
        }

        return Ok(response);
    }

    /// <summary>
    /// Charge le détail complet d'une tournée pour l'application mobile.
    /// </summary>
    /// <remarks>
    /// Cette route est utilisée après sélection de la tournée.
    ///
    /// Elle retourne le contrat JSON de chargement du matin en version 1.2.
    /// La réponse contient l'en-tête de tournée, le livreur, les articles saisissables,
    /// les clients ou points de livraison, les instructions, les commentaires exceptionnels,
    /// les informations de retour et les quantités initiales.
    ///
    /// Champs importants pour le mobile :
    /// - schemaVersion = "1.2" ;
    /// - dateModifiable = false ;
    /// - lignes[].idLigneSource : identifiant stable à renvoyer lors du POST final ;
    /// - lignes[].infosLivreur.commentaireExceptionnel : commentaire ponctuel affiché au livreur ;
    /// - lignes[].infosLivreur.zoneDechargementAffichee : zone prête à afficher si disponible ;
    /// - lignes[].saisie.quantites[].quantiteLivreePrevue : quantité prévue optionnelle, nullable ;
    /// - lignes[].saisie.quantites[].quantiteLivree et quantiteRecuperee : valeurs initiales de saisie.
    ///
    /// La date n'est pas acceptée depuis le mobile.
    /// Elle est toujours calculée par l'API avec le fuseau métier Europe/Paris.
    ///
    /// Exemple :
    /// GET /api/tournees/jour?codeTournee=4006&amp;codeLivreur=2
    /// </remarks>
    /// <param name="codeLivreur">Code métier du livreur. Exemple : 2.</param>
    /// <param name="codeTournee">Code de la tournée à charger. Exemple : 4006.</param>
    /// <param name="nomLivreur">Nom du livreur, optionnel. Le code livreur reste la donnée de référence.</param>
    /// <returns>Détail complet de la tournée à stocker localement dans l'application mobile.</returns>
    [HttpGet("jour")]
    [ProducesResponseType(typeof(TourneeMobileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiValidationErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiNotFoundResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiTechnicalErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<TourneeMobileDto>> GetTourneeDuJour(
        [FromQuery] string? codeLivreur,
        [FromQuery] string? codeTournee = null,
        [FromQuery] string? nomLivreur = null)
    {
        var parametreDateInterdit = GetParametreDateInterdit();

        if (parametreDateInterdit is not null)
        {
            return BadRequest(BuildDateQueryForbiddenResponse(parametreDateInterdit));
        }

        if (string.IsNullOrWhiteSpace(codeLivreur))
        {
            return BadRequest(new ApiValidationErrorResponse
            {
                Statut = ApiErrorCodes.ValidationError,
                Errors = new[]
                {
                    "Paramètre codeLivreur obligatoire."
                }
            });
        }

        if (string.IsNullOrWhiteSpace(codeTournee))
        {
            return BadRequest(new ApiValidationErrorResponse
            {
                Statut = ApiErrorCodes.ValidationError,
                Errors = new[]
                {
                    "Paramètre codeTournee obligatoire pour charger une tournée complète. Utilisez /api/tournees/disponibles pour obtenir la liste des tournées."
                }
            });
        }

        var date = _dateMetierService.GetDateTourneeAutorisee();
        var codeLivreurNormalise = codeLivreur.Trim();
        var codeTourneeNormalise = codeTournee.Trim();

        var tournee = await _tourneesService.GetTourneeAsync(date, codeLivreurNormalise, codeTourneeNormalise, nomLivreur);

        if (tournee is null)
        {
            return NotFound(new ApiNotFoundResponse
            {
                Statut = ApiErrorCodes.NotFound,
                Message = "Livreur ou tournée introuvable."
            });
        }

        return Ok(tournee);
    }

    private string? GetParametreDateInterdit()
    {
        return Request.Query.Keys.FirstOrDefault(key =>
            string.Equals(key, "date", StringComparison.OrdinalIgnoreCase)
            || string.Equals(key, "dateTournee", StringComparison.OrdinalIgnoreCase));
    }

    private object BuildDateQueryForbiddenResponse(string parametreDateInterdit)
    {
        var dateAutorisee = _dateMetierService.GetDateTourneeAutorisee();

        return new
        {
            statut = "VALIDATION_ERROR",
            code = "DATE_QUERY_PARAM_INTERDIT",
            message = "La date de tournée n'est pas acceptée dans l'URL. Elle est calculée côté API avec la date métier Europe/Paris.",
            parametreInterdit = parametreDateInterdit,
            dateTourneeAutorisee = dateAutorisee.ToString("yyyy-MM-dd")
        };
    }
}
