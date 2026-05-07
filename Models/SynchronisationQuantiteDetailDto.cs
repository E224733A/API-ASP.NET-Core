namespace API_ASP.NET_Core.Models;

/// <summary>
/// Quantité enregistrée pour un article dans une ligne de synchronisation.
/// </summary>
public sealed class SynchronisationQuantiteDetailDto
{
    /// <summary>
    /// Identifiant technique de la quantité enregistrée.
    /// </summary>
    public long IdQuantite { get; set; }

    /// <summary>
    /// Identifiant technique de la ligne de tournée associée.
    /// </summary>
    public long IdTourneeLigne { get; set; }

    /// <summary>
    /// Code technique de l'article.
    /// </summary>
    /// <remarks>
    /// Exemple : ROLLS
    /// </remarks>
    public string CodeArticle { get; set; } = string.Empty;

    /// <summary>
    /// Libellé lisible de l'article.
    /// </summary>
    /// <remarks>
    /// Exemple : Rolls
    /// </remarks>
    public string? LibelleArticle { get; set; }

    /// <summary>
    /// Quantité livrée enregistrée.
    /// </summary>
    public int QuantiteLivree { get; set; }

    /// <summary>
    /// Quantité récupérée enregistrée.
    /// </summary>
    public int QuantiteRecuperee { get; set; }

    /// <summary>
    /// Date de création de l'enregistrement.
    /// </summary>
    public DateTime DateCreation { get; set; }

    /// <summary>
    /// Date de dernière modification de l'enregistrement.
    /// </summary>
    public DateTime? DateModification { get; set; }
}