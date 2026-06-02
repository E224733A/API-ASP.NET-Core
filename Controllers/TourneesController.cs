using API_ASP.NET_Core.Constants;
using API_ASP.NET_Core.Models;
using API_ASP.NET_Core.Services;
using API_ASP.NET_Core.Validators;
using Microsoft.AspNetCore.Mvc;

namespace API_ASP.NET_Core.Controllers;

/// <summary>
/// Contrôleur utilisé par l'application mobile pour consulter les tournées disponibles
/// et charger le détail complet d'une tournée sélectionnée.
/// 
/// La logique de calcul de date métier et de normalisation des codes a été déplacée
/// dans <see cref="TourneesService"/> afin de respecter l'architecture MVC et
/// d'alléger ce contrôleur. Les validations restent toutefois réalisées ici pour
/// garantir que les paramètres attendus sont présents et que les paramètres de date
/// sont interdits.
/// </summary>
[ApiController]
[Route("api/tournees")]
[Produces("application/json")]
public class TourneesController : ControllerBase
{
    private readonly TourneesService _tourneesService;
    private readonly TourneeRequestValidator _tourneeValidator;

    public TourneesController(
        TourneesService service,
        TourneeRequestValidator tourneeValidator)
    {
        _tourneesService = service;
        _tourneeValidator = tourneeValidator;
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
        // Valider : aucun paramètre de date n'est accepté
        var parametreDateInterdit = _tourneeValidator.ValidateNoDatesInQuery(Request.Query);
        if (parametreDateInterdit is not null)
        {
            return BadRequest(_tourneeValidator.BuildDateQueryForbiddenResponse(parametreDateInterdit));
        }

        // Valider : codeLivreur obligatoire
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

        // Normalisation du code pour l'affichage du message d'erreur éventuel
        var codeLivreurNormalise = codeLivreur.Trim();

        var response = await _tourneesService.GetTourneesDisponiblesAsync(codeLivreur);

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
        // Valider : aucun paramètre de date n'est accepté
        var parametreDateInterdit = _tourneeValidator.ValidateNoDatesInQuery(Request.Query);
        if (parametreDateInterdit is not null)
        {
            return BadRequest(_tourneeValidator.BuildDateQueryForbiddenResponse(parametreDateInterdit));
        }

        // Valider : codeLivreur obligatoire
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

        // Valider : codeTournee obligatoire
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

        var tournee = await _tourneesService.GetTourneeAsync(codeLivreur, codeTournee, nomLivreur);

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
}