namespace API_ASP.NET_Core.Models;

public sealed class SynchronisationQuantiteDetailDto
{
    public long IdQuantite { get; set; }

    public long IdTourneeLigne { get; set; }

    public string CodeArticle { get; set; } = string.Empty;

    public string? LibelleArticle { get; set; }

    public int QuantiteLivree { get; set; }

    public int QuantiteRecuperee { get; set; }

    public DateTime DateCreation { get; set; }

    public DateTime? DateModification { get; set; }
}