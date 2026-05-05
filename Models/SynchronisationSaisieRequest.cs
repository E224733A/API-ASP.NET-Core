using System.Collections.Generic;

namespace API_ASP.NET_Core.Models;

/// <summary>
/// Données saisies par le livreur pour une ligne de tournée.
///
/// Nouveau format officiel :
/// - les quantités sont envoyées dans le tableau Quantites ;
/// - chaque article possède une quantité livrée et une quantité récupérée ;
/// - l'ancien format NbRolls / NbTapis / NbSacs / NbRecuperes n'est plus utilisé.
/// </summary>
public sealed class SynchronisationSaisieRequest
{
    /// <summary>
    /// Précision libre ajoutée par le livreur.
    ///
    /// Exemple :
    /// "2 rolls repris au local arrière".
    /// </summary>
    public string? PrecisionLivreur { get; set; }

    /// <summary>
    /// Statut du passage.
    ///
    /// Valeurs autorisées pour l'envoi final :
    /// - FAIT
    /// - NON_FAIT
    /// - ANOMALIE
    ///
    /// A_FAIRE peut exister au chargement du matin,
    /// mais doit être refusé dans POST /api/synchronisations.
    /// </summary>
    public string StatutPassage { get; set; } = string.Empty;

    /// <summary>
    /// Commentaire saisi par le livreur.
    ///
    /// Obligatoire si StatutPassage vaut :
    /// - NON_FAIT
    /// - ANOMALIE
    /// </summary>
    public string? CommentaireLivreur { get; set; }

    /// <summary>
    /// Heure de validation de la ligne.
    ///
    /// Format recommandé :
    /// 2026-04-28T09:12:00+02:00
    ///
    /// Une heure simple peut aussi être acceptée temporairement :
    /// 09:12:00
    /// </summary>
    public string? HeureValidation { get; set; }

    /// <summary>
    /// Indique si la ligne a été validée par le livreur.
    ///
    /// Pour l'envoi final, cette valeur doit être true.
    /// </summary>
    public bool EstValidee { get; set; }

    /// <summary>
    /// Quantités saisies par article.
    ///
    /// Chaque élément correspond à une ligne de quantité dans :
    /// Mobile_TourneeLigneQuantite.
    ///
    /// Exemples de CodeArticle :
    /// - ROLLS
    /// - TAPIS
    /// - SACS
    /// - VETEMENTS
    /// - EXPES
    ///
    /// Chaque article contient :
    /// - QuantiteLivree
    /// - QuantiteRecuperee
    /// </summary>
    public List<SynchronisationQuantiteRequest> Quantites { get; set; } = new();
}