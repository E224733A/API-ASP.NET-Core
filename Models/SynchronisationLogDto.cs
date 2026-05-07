namespace API_ASP.NET_Core.Models;

/// <summary>
/// Log technique lié à une synchronisation.
/// </summary>
/// <remarks>
/// Ce modèle permet de tracer les événements importants liés à l'envoi
/// et à l'enregistrement d'une synchronisation mobile.
/// </remarks>
public sealed class SynchronisationLogDto
{
    /// <summary>
    /// Identifiant technique du log.
    /// </summary>
    public long IdLog { get; set; }

    /// <summary>
    /// Identifiant technique de la tournée mobile associée.
    /// </summary>
    public long? IdTourneeMobile { get; set; }

    /// <summary>
    /// Identifiant technique interne du livreur associé.
    /// </summary>
    public int? IdLivreur { get; set; }

    /// <summary>
    /// Identifiant unique de synchronisation associé.
    /// </summary>
    public Guid? IdSynchronisation { get; set; }

    /// <summary>
    /// Date et heure de l'événement.
    /// </summary>
    public DateTime DateEvenement { get; set; }

    /// <summary>
    /// Type d'événement enregistré.
    /// </summary>
    /// <remarks>
    /// Exemple : ENVOI_REUSSI, ENVOI_ERREUR, VALIDATION_ERROR.
    /// </remarks>
    public string TypeEvenement { get; set; } = string.Empty;

    /// <summary>
    /// Niveau du log.
    /// </summary>
    /// <remarks>
    /// Exemple : INFO, WARNING, ERROR.
    /// </remarks>
    public string Niveau { get; set; } = string.Empty;

    /// <summary>
    /// Message lisible du log.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Détail technique complémentaire.
    /// </summary>
    public string? DetailTechnique { get; set; }

    /// <summary>
    /// Adresse IP associée à la requête, lorsqu'elle est disponible.
    /// </summary>
    public string? AdresseIP { get; set; }

    /// <summary>
    /// Nom de l'appareil mobile associé.
    /// </summary>
    public string? NomAppareil { get; set; }

    /// <summary>
    /// Version de l'application mobile associée.
    /// </summary>
    public string? VersionApplication { get; set; }
}