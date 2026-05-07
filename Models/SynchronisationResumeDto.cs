namespace API_ASP.NET_Core.Models;

/// <summary>
/// Résumé d'une synchronisation de tournée enregistrée.
/// </summary>
/// <remarks>
/// Ce modèle est utilisé pour afficher une vue synthétique des synchronisations reçues.
/// </remarks>
public sealed class SynchronisationResumeDto
{
    /// <summary>
    /// Identifiant technique de la tournée mobile enregistrée.
    /// </summary>
    public long IdTourneeMobile { get; set; }

    /// <summary>
    /// Identifiant unique de synchronisation envoyé par l'application mobile.
    /// </summary>
    public Guid IdSynchronisation { get; set; }

    /// <summary>
    /// Date de la tournée synchronisée.
    /// </summary>
    public DateTime DateTournee { get; set; }

    /// <summary>
    /// Code de la tournée.
    /// </summary>
    public string CodeTournee { get; set; } = string.Empty;

    /// <summary>
    /// Libellé de la tournée.
    /// </summary>
    public string? LibelleTournee { get; set; }

    /// <summary>
    /// Identifiant technique interne du livreur.
    /// </summary>
    public int IdLivreur { get; set; }

    /// <summary>
    /// Code métier du livreur.
    /// </summary>
    public string CodeLivreur { get; set; } = string.Empty;

    /// <summary>
    /// Nom du livreur.
    /// </summary>
    public string NomLivreur { get; set; } = string.Empty;

    /// <summary>
    /// Statut de synchronisation de la tournée.
    /// </summary>
    /// <remarks>
    /// Exemple : ENVOYEE, ERREUR, NON_ENVOYEE.
    /// </remarks>
    public string StatutSynchronisation { get; set; } = string.Empty;

    /// <summary>
    /// Date et heure de chargement de la tournée sur le mobile.
    /// </summary>
    public DateTime? DateChargementMobile { get; set; }

    /// <summary>
    /// Date et heure de réception de la synchronisation par l'API.
    /// </summary>
    public DateTime DateReceptionApi { get; set; }

    /// <summary>
    /// Date et heure d'envoi déclarée par l'application mobile.
    /// </summary>
    public DateTime? DateEnvoi { get; set; }

    /// <summary>
    /// Indique si la tournée est verrouillée après synchronisation.
    /// </summary>
    public bool EstVerrouillee { get; set; }

    /// <summary>
    /// Nombre de points prévus dans la tournée.
    /// </summary>
    public int? NombrePointsPrevus { get; set; }

    /// <summary>
    /// Nombre de points effectivement saisis ou validés.
    /// </summary>
    public int? NombrePointsSaisis { get; set; }

    /// <summary>
    /// Commentaire global transmis par le mobile.
    /// </summary>
    public string? CommentaireGlobal { get; set; }

    /// <summary>
    /// Nom de l'appareil mobile ayant envoyé la synchronisation.
    /// </summary>
    public string? NomAppareil { get; set; }

    /// <summary>
    /// Version de l'application mobile ayant envoyé la synchronisation.
    /// </summary>
    public string? VersionApplication { get; set; }

    /// <summary>
    /// Nombre de lignes avec le statut FAIT.
    /// </summary>
    public int NombreFaits { get; set; }

    /// <summary>
    /// Nombre de lignes avec le statut NON_FAIT.
    /// </summary>
    public int NombreNonFaits { get; set; }

    /// <summary>
    /// Nombre de lignes avec le statut ANOMALIE.
    /// </summary>
    public int NombreAnomalies { get; set; }
}