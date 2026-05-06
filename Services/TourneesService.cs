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

    public async Task<IReadOnlyList<TourneeDisponibleDto>?> GetTourneesDisponiblesAsync(
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
        var jourTournee = GetJourTournee(dateTournee);
        var jourLibelle = GetJourLibelle(dateTournee);

        return records
            .Where(tournee => !string.IsNullOrWhiteSpace(tournee.CodeTournee))
            .Select(tournee => new TourneeDisponibleDto
            {
                DateTournee = dateTournee.ToString("yyyy-MM-dd"),
                JourTournee = jourTournee,
                JourLibelle = jourLibelle,
                CodeTournee = tournee.CodeTournee,
                LibelleTournee = tournee.LibelleTournee ?? string.Empty
            })
            .OrderBy(tournee => TryParseInt(tournee.CodeTournee))
            .ThenBy(tournee => tournee.LibelleTournee)
            .ToList();
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

        return _mapper.Map(dateTournee, livreur, lignes);
    }

    private static int GetJourTournee(DateOnly dateTournee)
    {
        return dateTournee.DayOfWeek switch
        {
            DayOfWeek.Monday => 1,
            DayOfWeek.Tuesday => 2,
            DayOfWeek.Wednesday => 3,
            DayOfWeek.Thursday => 4,
            DayOfWeek.Friday => 5,
            DayOfWeek.Saturday => 6,
            DayOfWeek.Sunday => 7,
            _ => throw new ArgumentOutOfRangeException(nameof(dateTournee))
        };
    }

    private static string GetJourLibelle(DateOnly dateTournee)
    {
        return dateTournee.DayOfWeek switch
        {
            DayOfWeek.Monday => "Lundi",
            DayOfWeek.Tuesday => "Mardi",
            DayOfWeek.Wednesday => "Mercredi",
            DayOfWeek.Thursday => "Jeudi",
            DayOfWeek.Friday => "Vendredi",
            DayOfWeek.Saturday => "Samedi",
            DayOfWeek.Sunday => "Dimanche",
            _ => string.Empty
        };
    }

    private static int TryParseInt(string? value)
    {
        return int.TryParse(value, out var number)
            ? number
            : int.MaxValue;
    }
}
