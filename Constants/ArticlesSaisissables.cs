namespace API_ASP.NET_Core.Constants;

public static class ArticlesSaisissables
{
    public const string Rolls = "ROLLS";
    public const string RollsVides = "ROLLS_VIDES";
    public const string Tapis = "TAPIS";
    public const string Sacs = "SACS";
    public const string Vetements = "VETEMENTS";
    public const string Expes = "EXPES";

    public const string LibelleRolls = "Rolls";
    public const string LibelleRollsVides = "Rolls vides";
    public const string LibelleTapis = "Tapis";
    public const string LibelleSacs = "Sacs";
    public const string LibelleVetements = "Vêtements";
    public const string LibelleExpes = "Expéditions";

    /*
     * Articles affichés dans l'application mobile pour la V1.2.
     *
     * Le mobile applique déjà les règles de saisie métier en dur.
     * L'API doit tout de même renvoyer ROLLS_VIDES dans la liste afin
     * que la ligne apparaisse au chargement du matin, même si le référentiel
     * SQL n'a pas encore été alimenté ou si la base vient d'être recréée.
     *
     * Le modèle reste extensible :
     * - on peut ajouter VETEMENTS ou EXPES plus tard ;
     * - le contrat JSON ne change pas, car le mobile utilise quantites[].
     */
    public static readonly IReadOnlyList<(string CodeArticle, string Libelle)> ActifsV1 =
        new List<(string CodeArticle, string Libelle)>
        {
            (Rolls, LibelleRolls),
            (RollsVides, LibelleRollsVides),
            (Tapis, LibelleTapis),
            (Sacs, LibelleSacs)
        };
}
