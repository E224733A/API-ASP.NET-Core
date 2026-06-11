namespace API_ASP.NET_Core.Constants;

/// <summary>
/// Versions de schéma utilisées dans les réponses de chargement mobile.
/// </summary>
/// <remarks>
/// Cette constante concerne le contrat de lecture retourné au mobile lors du chargement
/// des tournées. Elle ne doit pas être confondue avec la version exigée par le POST
/// de synchronisation finale, qui possède ses propres règles de validation.
/// </remarks>
public static class SchemaVersions
{
    public const string SynchronisationActuelle = "1.2";
}
