namespace API_ASP.NET_Core.Constants;

/// <summary>
/// Statuts métier associés au cycle de vie d'une synchronisation mobile.
/// </summary>
/// <remarks>
/// Ces valeurs sont persistées en base dans les tables Mobile_* et servent aux contrôles
/// de doublon, aux diagnostics et aux retours API. Elles doivent rester stables pour
/// conserver la lisibilité de l'historique des tournées envoyées.
/// </remarks>
public static class StatutsSynchronisation
{
    public const string EnAttente = "EN_ATTENTE";
    public const string Envoyee = "ENVOYEE";
    public const string ErreurEnvoi = "ERREUR_ENVOI";
    public const string Annulee = "ANNULEE";
}
