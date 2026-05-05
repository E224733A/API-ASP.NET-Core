namespace API_ASP.NET_Core.Models;

public sealed class SynchronisationLogDto
{
    public long IdLog { get; set; }

    public long? IdTourneeMobile { get; set; }

    public int? IdLivreur { get; set; }

    public Guid? IdSynchronisation { get; set; }

    public DateTime DateEvenement { get; set; }

    public string TypeEvenement { get; set; } = string.Empty;

    public string Niveau { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public string? DetailTechnique { get; set; }

    public string? AdresseIP { get; set; }

    public string? NomAppareil { get; set; }

    public string? VersionApplication { get; set; }
}