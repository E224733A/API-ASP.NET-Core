namespace API_ASP.NET_Core.Constants;

public static class ArticlesSaisissables
{
    public const string Rolls = "ROLLS";
    public const string RollsVides = "ROLLS_VIDES";
    public const string Tapis = "TAPIS";
    public const string Sacs = "SACS";
    public const string Vetements = "VETEMENTS";
    public const string Expes = "EXPES";

    public const string LibelleRolls = "Chariots";
    public const string LibelleRollsVides = "Chariots vides";
    public const string LibelleTapis = "Tapis";
    public const string LibelleSacs = "Sacs";
    public const string LibelleVetements = "Vêtements";
    public const string LibelleExpes = "Expéditions";

    /*
     * Articles affichés dans l'application mobile pour la V1.2.
     *
     * Règle métier : ROLLS et ROLLS_VIDES utilisent le vocabulaire métier
     * "chariots". ROLLS correspond aux chariots classiques, ROLLS_VIDES
     * correspond aux chariots vides demandés par les clients.
     *
     * ROLLS_VIDES peut désormais être préparé côté Expédition comme une
     * quantité livrée prévue, tout en restant récupérable côté mobile.
     * Cela permet de suivre deux mouvements différents sur le même article :
     * - quantiteLivreePrevue / quantiteLivree pour les chariots vides demandés ;
     * - quantiteRecuperee pour les chariots vides récupérés sur le terrain.
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
