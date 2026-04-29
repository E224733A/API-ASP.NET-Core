namespace API_ASP.NET_Core.Models;

public class SynchronisationSaisieRequest
{
    public int NbExpes { get; set; }
    public int NbRolls { get; set; }
    public int NbVetements { get; set; }
    public int NbTapis { get; set; }
    public int NbSacs { get; set; }
    public int NbRecuperes { get; set; }
    public string? PrecisionLivreur { get; set; }
    public string StatutPassage { get; set; } = string.Empty;
    public string? CommentaireLivreur { get; set; }
    public string? HeureValidation { get; set; }
    public bool EstValidee { get; set; }
}
