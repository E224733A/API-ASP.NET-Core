using API_ASP.NET_Core.Models;
using API_ASP.NET_Core.Repositories;

namespace API_ASP.NET_Core.Services;

/// <summary>
/// Service applicatif dédié à la consultation des camions disponibles.
/// </summary>
/// <remarks>
/// Le service orchestre l'appel au repository et applique la normalisation attendue
/// par le contrat mobile. Le contrôleur reste ainsi limité au protocole HTTP.
/// </remarks>
public sealed class CamionsService
{
    private readonly CamionsRepository _camionsRepository;

    public CamionsService(CamionsRepository camionsRepository)
    {
        _camionsRepository = camionsRepository;
    }

    /// <summary>
    /// Retourne la liste des camions disponibles normalisée pour le mobile.
    /// </summary>
    public async Task<CamionsDisponiblesResponseDto> GetCamionsDisponiblesAsync()
    {
        var records = await _camionsRepository.GetCamionsDisponiblesAsync();

        var camions = records
            .Select(MapToDto)
            .Where(camion => !string.IsNullOrWhiteSpace(camion.IdCamion))
            .OrderBy(camion => camion.CodeCamion ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(camion => camion.Immatriculation ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(camion => camion.IdCamion, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new CamionsDisponiblesResponseDto
        {
            Camions = camions
        };
    }

    private static CamionMobileDto MapToDto(CamionRecord record)
    {
        return new CamionMobileDto
        {
            IdCamion = TrimToEmpty(record.IdCamion),
            CodeCamion = TrimToNull(record.CodeCamion),
            LibelleCamion = TrimToNull(record.LibelleCamion),
            Immatriculation = TrimToNull(record.Immatriculation),
            EstActif = record.EstActif ?? true
        };
    }

    private static string TrimToEmpty(string? value)
    {
        return value?.Trim() ?? string.Empty;
    }

    private static string? TrimToNull(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed)
            ? null
            : trimmed;
    }
}
