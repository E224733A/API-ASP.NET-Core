namespace API_ASP.NET_Core.Models;

public sealed class SynchronisationDetailDto
{
    public SynchronisationResumeDto Entete { get; set; } = new();

    public List<SynchronisationLigneDetailDto> Lignes { get; set; } = new();

    public List<SynchronisationQuantiteDetailDto> Quantites { get; set; } = new();

    public List<SynchronisationLogDto> Logs { get; set; } = new();
}