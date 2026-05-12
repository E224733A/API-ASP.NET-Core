namespace API_ASP.NET_Core.Models;

/// <summary>
/// Quantité envoyée par le livreur pour un article donné.
///
/// Format officiel du contrat JSON v1.2 :
///
/// "quantites": [
///   {
///     "codeArticle": "ROLLS",
///     "libelle": "Rolls",
///     "quantiteLivreePrevue": 2,
///     "quantiteLivree": 3,
///     "quantiteRecuperee": 2
///   }
/// ]
/// </summary>
public sealed class SynchronisationQuantiteRequest
{
    /// <summary>
    /// Code technique de l'article.
    /// Exemples : ROLLS, TAPIS, SACS.
    /// </summary>
    public string CodeArticle { get; set; } = string.Empty;

    /// <summary>
    /// Libellé lisible de l'article.
    /// Exemples : Rolls, Tapis, Sacs.
    /// </summary>
    public string? Libelle { get; set; }

    /// <summary>
    /// Quantité livrée prévue par l'expédition.
    /// null = l'expédition n'a rien renseigné ;
    /// 0 = l'expédition a volontairement prévu zéro ;
    /// valeur positive = quantité prévue.
    /// </summary>
    public int? QuantiteLivreePrevue { get; set; }

    /// <summary>
    /// Quantité livrée réellement constatée ou confirmée par le livreur.
    /// Doit être un entier positif ou nul.
    /// </summary>
    public int QuantiteLivree { get; set; }

    /// <summary>
    /// Quantité récupérée réellement constatée par le livreur.
    /// Doit être un entier positif ou nul.
    /// </summary>
    public int QuantiteRecuperee { get; set; }
}
