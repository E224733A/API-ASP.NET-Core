namespace API_ASP.NET_Core.Constants;

/// <summary>
/// Chemins d'URL centralisés pour les routes de l'API.
/// Ces constantes ne sont pas utilisées directement dans les attributs des contrôleurs
/// afin de ne pas modifier le comportement existant, mais elles offrent un emplacement
/// unique pour référencer les routes si nécessaire à l'avenir.
/// </summary>
public static class ApiRoutes
{
    /// <summary>
    /// Route de base pour l'accès aux informations de santé de l'API.
    /// </summary>
    public const string Health = "api/health";

    /// <summary>
    /// Sous‑route pour vérifier la connexion à la base ABSSolute.
    /// </summary>
    public const string HealthAbssolute = "api/health/abssolute";

    /// <summary>
    /// Sous‑route pour vérifier la connexion à la base mobile.
    /// </summary>
    public const string HealthMobile = "api/health/mobile";

    /// <summary>
    /// Route de base pour consulter les livreurs.
    /// </summary>
    public const string Livreurs = "api/livreurs";

    /// <summary>
    /// Route de base pour les tournées.
    /// </summary>
    public const string Tournees = "api/tournees";

    /// <summary>
    /// Route pour lister les tournées disponibles.
    /// </summary>
    public const string TourneesDisponibles = "api/tournees/disponibles";

    /// <summary>
    /// Route pour charger une tournée du jour.
    /// </summary>
    public const string TourneeJour = "api/tournees/jour";

    /// <summary>
    /// Route de base pour l'enregistrement des synchronisations.
    /// </summary>
    public const string Synchronisations = "api/synchronisations";

    /// <summary>
    /// Route de base pour les préparations Expédition.
    /// </summary>
    public const string ExpeditionPreparations = "api/expedition/preparations";

    /// <summary>
    /// Sous‑route pour récupérer les préparations à préparer côté Expédition.
    /// </summary>
    public const string ExpeditionPreparationsAPreparer = "api/expedition/preparations/a-preparer";

    /// <summary>
    /// Sous‑route pour verrouiller un lot de préparations Expédition.
    /// </summary>
    public const string ExpeditionPreparationsVerrouiller = "api/expedition/preparations/verrouiller";

    /// <summary>
    /// Route de base pour les outils de débogage SQL.
    /// </summary>
    public const string DebugSql = "api/debug/sql";
}
