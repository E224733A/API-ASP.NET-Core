using API_ASP.NET_Core.Constants;
using API_ASP.NET_Core.Mappers;
using API_ASP.NET_Core.Models;
using API_ASP.NET_Core.Repositories;

namespace API_ASP.NET_Core.Services;

public sealed class TourneesService
{
    private readonly TourneesRepository _repository;
    private readonly TourneeMobileMapper _mapper;

    public TourneesService(
        TourneesRepository repository,
        TourneeMobileMapper mapper)
    {
        _repository = repository;
        _mapper = mapper;
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

    private static int TryParseInt(string? value)
    {
        return int.TryParse(value, out var number)
            ? number
            : int.MaxValue;
    }
}
