namespace API_ASP.NET_Core.Constants;

/// <summary>
/// Statuts métier possibles pour un arrêt de tournée côté mobile.
/// </summary>
/// <remarks>
/// Le statut A_FAIRE correspond à l'état initial affiché au chargement de la tournée.
/// Il n'est pas accepté dans un envoi final : le livreur doit avoir terminé chaque arrêt
/// avec une décision explicite, FAIT, NON_FAIT ou ANOMALIE.
/// </remarks>
public static class StatutsPassage
{
    public const string AFaire = "A_FAIRE";
    public const string Fait = "FAIT";
    public const string NonFait = "NON_FAIT";
    public const string Anomalie = "ANOMALIE";

    public static readonly IReadOnlySet<string> Tous = new HashSet<string>(
        new[]
        {
            AFaire,
            Fait,
            NonFait,
            Anomalie
        },
        StringComparer.OrdinalIgnoreCase);

    public static readonly IReadOnlySet<string> AutorisesEnvoiFinal = new HashSet<string>(
        new[]
        {
            Fait,
            NonFait,
            Anomalie
        },
        StringComparer.OrdinalIgnoreCase);
}
