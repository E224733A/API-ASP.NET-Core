using System.Collections.Generic;

namespace API_ASP.NET_Core.Models;

/// <summary>
/// Données saisies par le livreur pour une ligne de tournée.
/// </summary>
/// <remarks>
/// Format officiel du contrat JSON v1.2 :
/// - les quantités sont envoyées dans le tableau quantites[] ;
/// - chaque article possède une quantité livrée prévue optionnelle ;
/// - chaque article possède une quantité livrée et une quantité récupérée ;
/// - l'ancien format NbRolls / NbTapis / NbSacs / NbRecuperes n'est plus la source principale.
/// </remarks>
public sealed class SynchronisationSaisieRequest
{
    /// <summary>
    /// Précision libre ajoutée par le livreur.
    /// </summary>
    /// <remarks>
    /// Exemple : 2 rolls repris au local arrière.
    /// </remarks>
    public string? PrecisionLivreur { get; set; }

    /// <summary>
    /// Statut du passage.
    /// </summary>
    /// <remarks>
    /// Valeurs autorisées pour l'envoi final :
    /// - FAIT ;
    /// - NON_FAIT ;
    /// - ANOMALIE.
    ///
    /// A_FAIRE peut exister au chargement du matin,
    /// mais doit être refusé dans POST /api/synchronisations.
    /// </remarks>
    public string StatutPassage { get; set; } = string.Empty;

    /// <summary>
    /// Commentaire saisi par le livreur.
    /// </summary>
    /// <remarks>
    /// Obligatoire si statutPassage vaut NON_FAIT ou ANOMALIE.
    /// </remarks>
    public string? CommentaireLivreur { get; set; }

    /// <summary>
    /// Heure de validation de la ligne.
    /// </summary>
    /// <remarks>
    /// Format recommandé : 2026-05-07T09:12:00+02:00.
    /// Une heure simple peut aussi être acceptée temporairement : 09:12:00.
    /// </remarks>
    public string? HeureValidation { get; set; }

    /// <summary>
    /// Indique si la ligne a été validée par le livreur.
    /// </summary>
    /// <remarks>
    /// Pour l'envoi final, cette valeur doit être true.
    /// </remarks>
    public bool EstValidee { get; set; }

    /// <summary>
    /// Quantités saisies par article.
    /// </summary>
    /// <remarks>
    /// Chaque élément correspond à une ligne de quantité dans Mobile_TourneeLigneQuantite.
    ///
    /// Exemples de codeArticle :
    /// - ROLLS ;
    /// - TAPIS ;
    /// - SACS ;
    /// - VETEMENTS ;
    /// - EXPES.
    ///
    /// Chaque article contient :
    /// - quantiteLivreePrevue ;
    /// - quantiteLivree ;
    /// - quantiteRecuperee.
    /// </remarks>
    public List<SynchronisationQuantiteRequest> Quantites { get; set; } = new();
}
