using API_ASP.NET_Core.Data;

namespace API_ASP.NET_Core.Repositories;

/// <summary>
/// Enregistrement brut issu de la source SQL camion.
/// </summary>
public sealed record CamionRecord
{
    public string? IdCamion { get; init; }
    public string? CodeCamion { get; init; }
    public string? LibelleCamion { get; init; }
    public string? Immatriculation { get; init; }
    public bool? EstActif { get; init; }
}

/// <summary>
/// Repository SQL dédié aux camions.
/// </summary>
/// <remarks>
/// Cette classe doit rester limitée à l'accès SQL. La normalisation des données
/// est réalisée dans <see cref="Services.CamionsService"/>.
/// </remarks>
public sealed class CamionsRepository
{
    /// <summary>
    /// Nom de vue à confirmer avant activation de la route.
    /// </summary>
    public const string TODO_NOM_VUE_CAMIONS_A_CONFIRMER = "TODO_NOM_VUE_CAMIONS_A_CONFIRMER";

    private readonly SqlConnectionFactory _connectionFactory;

    public CamionsRepository(SqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    /// <summary>
    /// Retourne les camions disponibles depuis la vue SQL métier confirmée.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Levée tant que le nom réel de la vue camion et ses colonnes n'ont pas été confirmés.
    /// </exception>
    public Task<IReadOnlyList<CamionRecord>> GetCamionsDisponiblesAsync()
    {
        _ = _connectionFactory;

        throw new InvalidOperationException(
            "Source SQL camion non configurée : le nom réel de la vue camion ABSSolute n'a pas été confirmé. " +
            "Remplacer TODO_NOM_VUE_CAMIONS_A_CONFIRMER dans CamionsRepository par la vue réelle et mapper les colonnes confirmées avant d'utiliser GET /api/camions/disponibles.");
    }
}
