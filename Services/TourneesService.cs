using API_ASP.NET_Core.Application.Mobile;
using API_ASP.NET_Core.Constants;
using API_ASP.NET_Core.Mappers;
using API_ASP.NET_Core.Models;
using API_ASP.NET_Core.Repositories;

namespace API_ASP.NET_Core.Services;

/// <summary>
/// Service dédié aux opérations de consultation des tournées pour les livreurs.
/// </summary>
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

    public async Task<TourneesDisponiblesResponseDto?> GetTourneesDisponiblesAsync(string? codeLivreur)
    {
        if (string.IsNullOrWhiteSpace(codeLivreur))
        {
            return null;
        }

        var date = _dateMetierService.GetDateTourneeAutorisee();
        return await GetTourneesDisponiblesAsync(date, codeLivreur.Trim());
    }

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

        await _repository.SaveChargementTourneeAsync(
            dateTournee,
            SchemaVersions.SynchronisationActuelle,
            livreur,
            tournee.CodeTournee,
            tournee.LibelleTournee,
            tournee.Chargement?.NombrePointsEnvoyes ?? lignes.Count);

        return tournee;
    }

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

    private static string BuildAdresseLivraisonKey(string? numClient, string? codePdl)
    {
        return $"{NormalizeKeyPart(numClient)}|{NormalizeKeyPart(codePdl)}";
    }

    private static string NormalizeKeyPart(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim();
    }

    private static int TryParseInt(string? value)
    {
        return int.TryParse(value, out var number)
            ? number
            : int.MaxValue;
    }
}
