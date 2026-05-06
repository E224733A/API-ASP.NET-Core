namespace API_ASP.NET_Core.Models;

public sealed class TourneeDisponibleDto
{
    public string DateTournee { get; set; } = string.Empty;

    public int JourTournee { get; set; }

    public string JourLibelle { get; set; } = string.Empty;

    public string CodeTournee { get; set; } = string.Empty;

    public string LibelleTournee { get; set; } = string.Empty;
}
