using API_ASP.NET_Core.Application.Mobile;
using API_ASP.NET_Core.Constants;
using API_ASP.NET_Core.Mappers;
using API_ASP.NET_Core.Models;
using API_ASP.NET_Core.Repositories;

namespace API_ASP.NET_Core.Services;

/// <summary>
/// Service dédié aux opérations de consultation des tournées pour les livreurs.
/// </summary>
/// <remarks>
/// Ce service prépare les réponses de chargement mobile : liste des tournées disponibles,
/// détail complet d'une tournée, articles saisissables, commentaires exceptionnels,
/// préremplissages Expédition et liens d'adresse de livraison. La date métier reste fournie
/// par <see cref="DateMetierService"/> afin que le mobile ne pilote pas la date chargée.
/// </remarks>
public sealed class TourneesService
{
    private readonly TourneesRepository _repository;
    private readonly TourneeMobileMapper _mapper;
    private readonly DateMetierService _dateMetierService;
    private readonly ILienAdresseLivraisonProvider _lienAdresseLivraisonProvider;

    public TourneesService(
        TourneesRepository repository,
        TourneeMobileMapper mapper,
        DateMetierService dateMetierService,
        ILienAdresseLivraisonProvider lienAdresseLivraisonProvider)
    {
        _repository = repository;
        _mapper = mapper;
        _dateMetierService = dateMetierService;
        _lienAdresseLivraisonProvider = lienAdresseLivraisonProvider;
    }

    /// <summary>
    /// Retourne les tournées disponibles pour un livreur et une date métier déjà déterminée.
    /// </summary>
    /// <remarks>
    /// Cette surcharge est utile pour tester ou réutiliser explicitement une date calculée.
    /// Elle vérifie d'abord que le livreur existe avant de construire la liste des tournées.
    /// </remarks>
    public async Task<TourneesDisponiblesResponseDto?> GetTourneesDisponiblesAsync(
        DateOnly dateTournee,
        string codeLivreur)
    {
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
    /// Retourne les tournées disponibles pour le livreur sur la date métier mobile autorisée.
    /// </summary>
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
    /// Charge le détail complet d'une tournée mobile pour une date métier déjà déterminée.
    /// </summary>
    /// <remarks>
    /// La réponse combine les lignes de tournée, les articles saisissables, les commentaires
    /// exceptionnels, les préremplissages Expédition et les liens d'adresse de livraison.
    /// Le chargement est ensuite tracé en base pour diagnostic.
    /// </remarks>
    public async Task<TourneeMobileDto?> GetTourneeAsync(
        DateOnly dateTournee,
        string codeLivreur,
        string? codeTournee = null,
        string? nomLivreur = null)
    {
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
        var liensAdresseLivraisonParClientEtPdl = await GetLiensAdresseLivraisonParClientEtPdlAsync(lignes);

        var tournee = _mapper.Map(
            dateTournee,
            livreur,
            lignes,
            articlesSaisissables,
            commentairesExceptionnels,
            preRemplissages,
            liensAdresseLivraisonParClientEtPdl);

        // Diagnostic métier : conserver une trace du chargement envoyé au mobile.
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
    /// Charge le détail d'une tournée pour la date métier mobile autorisée.
    /// </summary>
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
    /// Récupère les liens d'adresse de livraison pour les couples client / point de livraison de la tournée.
    /// </summary>
    /// <remarks>
    /// Les liens sont chargés à part afin de ne pas bloquer le modèle principal de tournée
    /// sur une source optionnelle. L'absence de lien reste représentée par null.
    /// </remarks>
    private async Task<IReadOnlyDictionary<string, string?>> GetLiensAdresseLivraisonParClientEtPdlAsync(
        IReadOnlyList<TourneeLigneRecord> lignes)
    {
        var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var points = lignes
            .Where(ligne =>
                !string.IsNullOrWhiteSpace(ligne.NumClient)
                && !string.IsNullOrWhiteSpace(ligne.CodePDL))
            .Select(ligne => new
            {
                NumClient = ligne.NumClient.Trim(),
                CodePDL = ligne.CodePDL!.Trim()
            })
            .Distinct()
            .ToList();

        foreach (var point in points)
        {
            var key = BuildAdresseLivraisonKey(point.NumClient, point.CodePDL);
            var lien = await _lienAdresseLivraisonProvider.GetLienAdresseLivraisonAsync(
                point.NumClient,
                point.CodePDL);

            result[key] = string.IsNullOrWhiteSpace(lien) ? null : lien.Trim();
        }

        return result;
    }

    /// <summary>
    /// Construit la clé utilisée pour rattacher un lien d'adresse à un client et un PDL.
    /// </summary>
    private static string BuildAdresseLivraisonKey(string? numClient, string? codePdl)
    {
        return $"{NormalizeKeyPart(numClient)}|{NormalizeKeyPart(codePdl)}";
    }

    /// <summary>
    /// Normalise une partie de clé sans produire de valeur nulle.
    /// </summary>
    private static string NormalizeKeyPart(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim();
    }

    /// <summary>
    /// Convertit un code tournée numérique en valeur de tri, avec repli en fin de liste.
    /// </summary>
    private static int TryParseInt(string? value)
    {
        return int.TryParse(value, out var number)
            ? number
            : int.MaxValue;
    }
}
