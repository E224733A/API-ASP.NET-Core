namespace API_ASP.NET_Core.Constants;

public static class ArticlesSaisissables
{
    public const string Rolls = "ROLLS";
    public const string Tapis = "TAPIS";
    public const string Sacs = "SACS";
    public const string Vetements = "VETEMENTS";
    public const string Expes = "EXPES";

    public const string LibelleRolls = "Rolls";
    public const string LibelleTapis = "Tapis";
    public const string LibelleSacs = "Sacs";
    public const string LibelleVetements = "Vêtements";
    public const string LibelleExpes = "Expéditions";

    /*
     * Articles affichés dans l'application mobile pour la V1.
     *
     * Le modèle reste extensible :
     * - on peut ajouter VETEMENTS ou EXPES plus tard ;
     * - le contrat JSON ne change pas, car le mobile utilise quantites[].
     */
    public static readonly IReadOnlyList<(string CodeArticle, string Libelle)> ActifsV1 =
        new List<(string CodeArticle, string Libelle)>
        {
            (Rolls, LibelleRolls),
            (Tapis, LibelleTapis),
            (Sacs, LibelleSacs)
        };
}