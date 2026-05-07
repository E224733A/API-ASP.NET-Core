namespace API_ASP.NET_Core.Models;

/// <summary>
/// Requête envoyée par l'application mobile pour synchroniser une tournée en fin de journée.
/// </summary>
/// <remarks>
/// Ce modèle représente le corps JSON attendu par POST /api/synchronisations.
///
/// Il contient :
/// - les informations générales de la tournée ;
/// - l'identifiant unique de synchronisation ;
/// - le livreur ;
/// - les informations de l'appareil mobile ;
/// - les lignes de tournée validées par le livreur.
/// </remarks>
public class SynchronisationTourneeRequest
{
    /// <summary>
    /// Version du schéma JSON utilisée par l'application mobile.
    /// </summary>
    /// <remarks>
    /// Cette version permet à l'API de vérifier que le mobile utilise un contrat compatible.
    ///
    /// Exemple : 1.1
    /// </remarks>
    public string SchemaVersion { get; set; } = string.Empty;

    /// <summary>
    /// Identifiant unique de l'envoi de synchronisation.
    /// </summary>
    /// <remarks>
    /// Doit être un UUID valide.
    /// Il permet d'éviter d'enregistrer deux fois le même envoi mobile.
    ///
    /// Exemple : 11111111-1111-1111-1111-111111111111
    /// </remarks>
    public string IdSynchronisation { get; set; } = string.Empty;

    /// <summary>
    /// Date de la tournée synchronisée.
    /// </summary>
    /// <remarks>
    /// Format attendu : yyyy-MM-dd.
    ///
    /// Exemple : 2026-04-28
    /// </remarks>
    public string DateTournee { get; set; } = string.Empty;

    /// <summary>
    /// Code de la tournée synchronisée.
    /// </summary>
    /// <remarks>
    /// Ce code doit correspondre à la tournée chargée le matin par l'application mobile.
    ///
    /// Exemple : 2001
    /// </remarks>
    public string CodeTournee { get; set; } = string.Empty;

    /// <summary>
    /// Libellé lisible de la tournée.
    /// </summary>
    /// <remarks>
    /// Exemple : MDR VENDEE
    /// </remarks>
    public string LibelleTournee { get; set; } = string.Empty;

    /// <summary>
    /// Informations du livreur ayant réalisé la tournée.
    /// </summary>
    public SynchronisationLivreurRequest? Livreur { get; set; }

    /// <summary>
    /// Informations de l'appareil mobile utilisé pour l'envoi.
    /// </summary>
    public SynchronisationMobileRequest? Mobile { get; set; }

    /// <summary>
    /// Commentaire général saisi sur la tournée.
    /// </summary>
    /// <remarks>
    /// Ce champ est optionnel.
    /// Il peut être utilisé pour ajouter une remarque globale sur la tournée.
    /// </remarks>
    public string? CommentaireGlobal { get; set; }

    /// <summary>
    /// Liste des lignes de tournée envoyées par l'application mobile.
    /// </summary>
    /// <remarks>
    /// La liste ne doit pas être vide lors de l'envoi final.
    /// Chaque ligne correspond à un client ou point de livraison de la tournée.
    /// </remarks>
    public List<SynchronisationLigneRequest> Lignes { get; set; } = new();
}