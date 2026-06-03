namespace API_ASP.NET_Core.Models;

/// <summary>
/// Quantité enregistrée pour un article dans une ligne de synchronisation.
/// </summary>
public sealed class SynchronisationQuantiteDetailDto
{
    public long IdQuantite { get; set; }

    public long IdTourneeLigne { get; set; }

    public string CodeArticle { get; set; } = string.Empty;

    public string? LibelleArticle { get; set; }

    /// <summary>
    /// Quantité livrée prévue par l'expédition.
    /// null = non renseigné ; 0 = zéro volontaire.
    /// </summary>
    public int? QuantiteLivreePrevue { get; set; }

    public int QuantiteLivree { get; set; }

    public int QuantiteRecuperee { get; set; }

    public DateTimeOffset DateCreation { get; set; }

    public DateTimeOffset? DateModification { get; set; }
}
