namespace API_ASP.NET_Core.Models;

/// <summary>
/// Réponse retournée lorsqu'une validation échoue.
/// </summary>
public sealed class ApiValidationErrorResponse
{
    /// <summary>
    /// Statut de l'erreur.
    /// </summary>
    /// <remarks>
    /// Exemple : VALIDATION_ERROR
    /// </remarks>
    public string Statut { get; set; } = string.Empty;

    /// <summary>
    /// Liste des erreurs de validation détectées.
    /// </summary>
    public IEnumerable<string> Errors { get; set; } = Array.Empty<string>();
}

/// <summary>
/// Réponse retournée lorsqu'un conflit métier ou technique est détecté.
/// </summary>
public sealed class ApiConflictResponse
{
    /// <summary>
    /// Statut de l'erreur.
    /// </summary>
    /// <remarks>
    /// Exemple : CONFLICT
    /// </remarks>
    public string Statut { get; set; } = string.Empty;

    /// <summary>
    /// Code précis du conflit.
    /// </summary>
    /// <remarks>
    /// Exemples : SYNCHRONISATION_ALREADY_EXISTS, TOURNEE_ALREADY_SENT
    /// </remarks>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Message lisible décrivant le conflit.
    /// </summary>
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Réponse retournée lorsqu'une ressource demandée est introuvable.
/// </summary>
public sealed class ApiNotFoundResponse
{
    /// <summary>
    /// Statut de l'erreur.
    /// </summary>
    /// <remarks>
    /// Exemple : NOT_FOUND
    /// </remarks>
    public string Statut { get; set; } = string.Empty;

    /// <summary>
    /// Message lisible décrivant la ressource introuvable.
    /// </summary>
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Réponse retournée lorsqu'une erreur technique survient.
/// </summary>
public sealed class ApiTechnicalErrorResponse
{
    /// <summary>
    /// Statut de l'erreur.
    /// </summary>
    /// <remarks>
    /// Exemple : ERROR
    /// </remarks>
    public string Statut { get; set; } = string.Empty;

    /// <summary>
    /// Code technique de l'erreur.
    /// </summary>
    /// <remarks>
    /// Exemple : TECHNICAL_ERROR
    /// </remarks>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Message lisible décrivant l'erreur.
    /// </summary>
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Réponse retournée lors de la consultation de la liste des synchronisations.
/// </summary>
public sealed class SynchronisationsListResponse
{
    /// <summary>
    /// Statut de la réponse.
    /// </summary>
    /// <remarks>
    /// Exemple : SUCCESS
    /// </remarks>
    public string Statut { get; set; } = string.Empty;

    /// <summary>
    /// Nombre de synchronisations retournées.
    /// </summary>
    public int Count { get; set; }

    /// <summary>
    /// Liste des synchronisations trouvées.
    /// </summary>
    public List<SynchronisationResumeDto> Synchronisations { get; set; } = new();
}

/// <summary>
/// Réponse retournée lors de la consultation du détail d'une synchronisation.
/// </summary>
public sealed class SynchronisationDetailResponse
{
    /// <summary>
    /// Statut de la réponse.
    /// </summary>
    /// <remarks>
    /// Exemple : SUCCESS
    /// </remarks>
    public string Statut { get; set; } = string.Empty;

    /// <summary>
    /// Détail complet de la synchronisation.
    /// </summary>
    public SynchronisationDetailDto Synchronisation { get; set; } = new();
}