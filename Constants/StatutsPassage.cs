namespace API_ASP.NET_Core.Constants;

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