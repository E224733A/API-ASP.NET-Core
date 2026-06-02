using API_ASP.NET_Core.Constants;
using API_ASP.NET_Core.Mappers;
using API_ASP.NET_Core.Models;
using API_ASP.NET_Core.Repositories;

namespace API_ASP.NET_Core.Services;

/// <summary>
/// Service dédié aux opérations de consultation des tournées pour les livreurs.
/// Cette classe encapsule l'accès au dépôt SQL et la transformation des données
/// vers les DTO consommés par l'application mobile. Elle expose également des
/// méthodes simplifiées qui s'appuient sur la date métier calculée côté API
/// afin d'alléger le contrôleur et éviter que celui-ci ne manipule la logique
/// de calcul de date ou de normalisation des codes.
/// </summary>
public sealed class TourneesService
{
    private readonly TourneesRepository _repository;
    private readonly TourneeMobileMapper _mapper;
    private readonly DateMetierService _dateMetierService;

    /// <summary>
    /// Initialise une nouvelle instance de <see cref="TourneesService"/>.
    /// </summary>
    /// <param name="repository">Dépôt SQL pour les tournées.</param>
    /// <param name="mapper">Mapper chargé de convertir les enregistrements SQL en DTO.</param>
    /// <param name="dateMetierService">Service de calcul de date métier.</param>
    public TourneesService(
        TourneesRepository repository,
        TourneeMobileMapper mapper,
        DateMetierService dateMetierService)
    {
        _repository = repository;
        _mapper = mapper;
        _dateMetierService = dateMetierService;
    }

    /// <summary>
    /// Renvoie la liste des tournées disponibles pour un jour précis et un livreur.
    /// Cette surcharge utilise explicitement la date métier passée en paramètre.
    /// </summary>
    /// <param name="dateTournee">Date métier à utiliser pour la recherche.</param>
    /// <param name="codeLivreur">Code métier du livreur.</param>
    /// <returns>Une réponse enveloppée contenant la liste des tournées disponibles ou null en cas d'erreur ou si le livreur n'existe pas.</returns>
    public async Task<TourneesDisponiblesResponseDto?> GetTourneesDisponiblesAsync(
        DateOnly dateTournee,
        string codeLivreur)
    {
        // Appel identique à l'implémentation existante : on vérifie la présence d'un code livreur,
        // on cherche le livreur, puis on récupère les tournées disponibles et on assemble la réponse.
        if (string.IsNullOrWhiteSpace(codeLivreur))
        {
            return null;
        }

        var livreur = await _repository.GetLivreurAsync(codeLivreur);
        if (livreur is null)
        {
            return null;
        }

        var records = await _repository.GetTourneesDisponiblesAsync(dateTournee);

        var tournees = records
            .Where(tournee => !string.IsNullOrWhiteSpace(tournee.CodeTournee))
            .Select(tournee => new TourneeDisponibleDto
            {
                CodeTournee = tournee.CodeTournee,
                LibelleTournee = tournee.LibelleTournee ?? string.Empty,
                NombrePoints = tournee.NombrePoints
            })
            .OrderBy(tournee => TryParseInt(tournee.CodeTournee))
            .ThenBy(tournee => tournee.LibelleTournee)
            .ToList();

        return new TourneesDisponiblesResponseDto
        {
            SchemaVersion = SchemaVersions.SynchronisationActuelle,
            DateTournee = dateTournee.ToString("yyyy-MM-dd"),
            DateModifiable = false,
            Livreur = new LivreurDto
            {
                CodeLivreur = livreur.CodeLivreur,
                NomLivreur = string.IsNullOrWhiteSpace(livreur.NomLivreur)
                    ? "Inconnu"
                    : livreur.NomLivreur
            },
            Tournees = tournees
        };
    }

    /// <summary>
    /// Renvoie la liste des tournées disponibles pour le livreur en utilisant la date métier autorisée calculée côté API.
    /// Cette méthode simplifie l'appel depuis le contrôleur en supprimant la gestion de la date et la normalisation du code.
    /// </summary>
    /// <param name="codeLivreur">Code métier du livreur.</param>
    /// <returns>Une réponse enveloppée contenant la liste des tournées disponibles ou null en cas d'erreur ou si le livreur n'existe pas.</returns>
    public async Task<TourneesDisponiblesResponseDto?> GetTourneesDisponiblesAsync(string? codeLivreur)
    {
        if (string.IsNullOrWhiteSpace(codeLivreur))
        {
            return null;
        }

        var date = _dateMetierService.GetDateTourneeAutorisee();
        return await GetTourneesDisponiblesAsync(date, codeLivreur.Trim());
    }

    /// <summary>
    /// Charge le détail complet d'une tournée pour un livreur à une date précise.
    /// Cette surcharge utilise explicitement la date métier passée en paramètre.
    /// </summary>
    /// <param name="dateTournee">Date métier à utiliser pour la recherche.</param>
    /// <param name="codeLivreur">Code métier du livreur.</param>
    /// <param name="codeTournee">Code de la tournée à charger.</param>
    /// <param name="nomLivreur">Nom du livreur, optionnel.</param>
    /// <returns>Le détail complet de la tournée ou null si le livreur ou la tournée est introuvable.</returns>
    public async Task<TourneeMobileDto?> GetTourneeAsync(
        DateOnly dateTournee,
        string codeLivreur,
        string? codeTournee = null,
        string? nomLivreur = null)
    {
        // Appel identique à l'implémentation existante : on vérifie les paramètres, on recherche le livreur,
        // on récupère les lignes de tournée et on assemble la réponse.
        if (string.IsNullOrWhiteSpace(codeLivreur))
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(codeTournee))
        {
            return null;
        }

        var livreur = await _repository.GetLivreurAsync(codeLivreur);

        if (livreur is null)
        {
            return null;
        }

        var lignes = (await _repository.GetTourneeLinesAsync(
            dateTournee,
            livreur.CodeLivreur,
            codeTournee)).ToList();

        if (lignes.Count == 0)
        {
            return null;
        }

        var articlesSaisissables = await _repository.GetArticlesSaisissablesAsync();
        var commentairesExceptionnels = await _repository.GetCommentairesExceptionnelsAsync(dateTournee, codeTournee);
        var preRemplissages = await _repository.GetPreRemplissagesAsync(dateTournee, codeTournee);

        var tournee = _mapper.Map(
            dateTournee,
            livreur,
            lignes,
            articlesSaisissables,
            commentairesExceptionnels,
            preRemplissages);

        await _repository.SaveChargementTourneeAsync(
            dateTournee,
            SchemaVersions.SynchronisationActuelle,
            livreur,
            tournee.CodeTournee,
            tournee.LibelleTournee,
            tournee.Chargement?.NombrePointsEnvoyes ?? lignes.Count);

        return tournee;
    }

    /// <summary>
    /// Charge le détail complet d'une tournée en utilisant la date métier autorisée calculée côté API.
    /// Cette méthode simplifie l'appel depuis le contrôleur en supprimant la gestion de la date et la normalisation des codes.
    /// </summary>
    /// <param name="codeLivreur">Code métier du livreur.</param>
    /// <param name="codeTournee">Code de la tournée à charger.</param>
    /// <param name="nomLivreur">Nom du livreur, optionnel.</param>
    /// <returns>Le détail complet de la tournée ou null si le livreur ou la tournée est introuvable.</returns>
    public async Task<TourneeMobileDto?> GetTourneeAsync(
        string? codeLivreur,
        string? codeTournee = null,
        string? nomLivreur = null)
    {
        if (string.IsNullOrWhiteSpace(codeLivreur) || string.IsNullOrWhiteSpace(codeTournee))
        {
            return null;
        }

        var date = _dateMetierService.GetDateTourneeAutorisee();
        return await GetTourneeAsync(date, codeLivreur.Trim(), codeTournee.Trim(), nomLivreur);
    }

    /// <summary>
    /// Convertit une chaîne en entier pour le tri des codes tournée.
    /// Les valeurs non numériques sont ramenées à int.MaxValue pour être triées en fin de liste.
    /// </summary>
    /// <param name="value">Valeur de code tournée potentiellement numérique.</param>
    /// <returns>Un entier utilisable pour l'ordonnancement.</returns>
    private static int TryParseInt(string? value)
    {
        return int.TryParse(value, out var number)
            ? number
            : int.MaxValue;
    }
}