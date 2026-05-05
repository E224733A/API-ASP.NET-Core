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
}