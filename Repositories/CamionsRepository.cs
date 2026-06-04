using API_ASP.NET_Core.Data;
using Dapper;

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
/// Cette classe reste limitée à l'accès SQL. La normalisation des données
/// est réalisée dans <see cref="Services.CamionsService"/>.
/// </remarks>
public sealed class CamionsRepository
{
    private readonly SqlConnectionFactory _connectionFactory;

    public CamionsRepository(SqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    /// <summary>
    /// Retourne les camions disponibles depuis la vue SQL métier confirmée.
    /// </summary>
    /// <remarks>
    /// Source confirmée : [lavinprosli].[dbo].[v_Truck].
    /// Colonnes confirmées : CODE, DESCRIPTION, DEFAULTDRIVERNUMBER, DEFAULTDRIVERNAME.
    ///
    /// Mapping retenu :
    /// - CODE est utilisé comme identifiant source du camion ;
    /// - CODE est également exposé comme code camion, car aucune autre colonne de code camion
    ///   distincte n'est disponible dans la vue confirmée ;
    /// - DESCRIPTION est exposé comme libellé camion ;
    /// - CODE est exposé comme immatriculation, car les valeurs observées correspondent
    ///   au format d'une immatriculation ;
    /// - EstActif vaut true par défaut, car la vue confirmée ne fournit pas de colonne d'activité.
    ///
    /// DEFAULTDRIVERNUMBER et DEFAULTDRIVERNAME ne sont pas utilisés pour identifier le camion :
    /// ils décrivent le chauffeur par défaut et non le camion lui-même.
    /// </remarks>
    public async Task<IReadOnlyList<CamionRecord>> GetCamionsDisponiblesAsync()
    {
        using var connection = _connectionFactory.CreateAbssoluteConnection();

        const string sql = """
            SELECT DISTINCT
                LTRIM(RTRIM(CAST(CODE AS NVARCHAR(50)))) AS IdCamion,
                LTRIM(RTRIM(CAST(CODE AS NVARCHAR(50)))) AS CodeCamion,
                NULLIF(LTRIM(RTRIM(CAST(DESCRIPTION AS NVARCHAR(150)))), N'') AS LibelleCamion,
                LTRIM(RTRIM(CAST(CODE AS NVARCHAR(50)))) AS Immatriculation,
                CAST(1 AS bit) AS EstActif
            FROM [lavinprosli].[dbo].[v_Truck]
            WHERE CODE IS NOT NULL
              AND LTRIM(RTRIM(CAST(CODE AS NVARCHAR(50)))) <> N'';
            """;

        var camions = await connection.QueryAsync<CamionRecord>(sql);
        return camions.ToList();
    }
}
