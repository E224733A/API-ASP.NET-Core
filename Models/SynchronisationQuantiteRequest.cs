namespace API_ASP.NET_Core.Models;

/// <summary>
/// Quantité saisie par le livreur pour un article donné.
///
/// Nouveau format officiel du contrat JSON :
///
/// "quantites": [
///   {
///     "codeArticle": "ROLLS",
///     "libelle": "Rolls",
///     "quantiteLivree": 3,
///     "quantiteRecuperee": 2
///   }
/// ]
/// </summary>
public sealed class SynchronisationQuantiteRequest
{
    /// <summary>
    /// Code technique de l'article.
    ///
    /// Exemples :
    /// - ROLLS
    /// - TAPIS
    /// - SACS
    /// - VETEMENTS
    /// - EXPES
    /// </summary>
    public string CodeArticle { get; set; } = string.Empty;

    /// <summary>
    /// Libellé lisible de l'article.
    ///
    /// Exemples :
    /// - Rolls
    /// - Tapis
    /// - Sacs
    /// - Vêtements
    /// </summary>
    public string? Libelle { get; set; }

    /// <summary>
    /// Quantité livrée chez le client pour cet article.
    /// Doit être un entier positif ou nul.
    /// </summary>
    public int QuantiteLivree { get; set; }

    /// <summary>
    /// Quantité récupérée chez le client pour cet article.
    /// Doit être un entier positif ou nul.
    /// </summary>
    public int QuantiteRecuperee { get; set; }
}