namespace API_ASP.NET_Core.Models;

/// <summary>
/// Résumé d'une synchronisation de tournée enregistrée.
/// </summary>
public sealed class SynchronisationResumeDto
{
    public long IdTourneeMobile { get; set; }

    public string SchemaVersion { get; set; } = string.Empty;

    public Guid IdSynchronisation { get; set; }

    public DateTime DateTournee { get; set; }

    public string CodeTournee { get; set; } = string.Empty;

    public string? LibelleTournee { get; set; }

    public int IdLivreur { get; set; }

    public string CodeLivreur { get; set; } = string.Empty;

    public string NomLivreur { get; set; } = string.Empty;

    public string StatutSynchronisation { get; set; } = string.Empty;

    public DateTimeOffset? DateChargementMobile { get; set; }

    public DateTimeOffset DateReceptionApi { get; set; }

    public DateTimeOffset? DateEnvoi { get; set; }

    public bool EstVerrouillee { get; set; }

    public int? NombrePointsPrevus { get; set; }

    public int? NombrePointsSaisis { get; set; }

    public string? CommentaireGlobal { get; set; }

    public string? NomAppareil { get; set; }

    public string? VersionApplication { get; set; }

    public int NombreFaits { get; set; }

    public int NombreNonFaits { get; set; }

    public int NombreAnomalies { get; set; }
}
