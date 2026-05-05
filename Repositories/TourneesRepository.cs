using Dapper;
using API_ASP.NET_Core.Data;
using System.Data;

namespace API_ASP.NET_Core.Repositories;

public record LivreurRecord
{
    public string CodeLivreur { get; init; } = string.Empty;
    public string NomLivreur { get; init; } = string.Empty;
}

public record TourneeLigneRecord
{
    public string NumClient { get; init; } = string.Empty;
    public string NomClient { get; init; } = string.Empty;
    public string? NomAffiche { get; init; }

    public string? CodePDL { get; init; }
    public string? DescriptionPDL { get; init; }

    public string? AdresseLigne1 { get; init; }
    public string? AdresseLigne2 { get; init; }
    public string? AdresseLigne3 { get; init; }
    public string? Ville { get; init; }
    public string? CodePostal { get; init; }

    public int? OrdreArret { get; init; }
    public int? Horaire { get; init; }

    public int? JourTournee { get; init; }

    public string CodeTournee { get; init; } = string.Empty;
    public string? LibelleTournee { get; init; }

    public int? JourTourneeRetour { get; init; }
    public string? CodeTourneeRetour { get; init; }
    public string? LibelleTourneeRetour { get; init; }

    public string? SchemaLivraison { get; init; }

    public string? Instructions { get; init; }
    public string? CommentaireFiche { get; init; }
    public string? ZoneDechargement { get; init; }
    public string? Zone { get; init; }
    public string? Precision { get; init; }
    public string? Cle { get; init; }

    public bool EstFerme { get; init; }
    public DateTime? DateFermeture { get; init; }
    public string? MotifFermeture { get; init; }
}

public class TourneesRepository
{
    private readonly SqlConnectionFactory _connectionFactory;

    public TourneesRepository(SqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    /// <summary>
    /// Vérifie l'existence du livreur dans les données chauffeurs ABSSolute.
    ///
    /// Le code livreur utilisé par l'application correspond à :
    /// v_chauffeurs.DRIVERNUMERO.
    /// </summary>
    public async Task<LivreurRecord?> GetLivreurAsync(string codeLivreur)
    {
        using var connection = _connectionFactory.CreateAbssoluteConnection();

        const string sql = """
            SELECT TOP 1
                CAST(DRIVERNUMERO AS NVARCHAR(50)) AS CodeLivreur,
                DRIVERNAME AS NomLivreur
            FROM v_chauffeurs
            WHERE LTRIM(RTRIM(CAST(DRIVERNUMERO AS NVARCHAR(50)))) = @CodeLivreur;
            """;

        return await connection.QuerySingleOrDefaultAsync<LivreurRecord>(
            sql,
            new
            {
                CodeLivreur = codeLivreur.Trim()
            });
    }

    private async Task<string> GetFermetureCustomerColumnAsync(IDbConnection connection)
    {
        const string sql = """
            SELECT COUNT(1)
            FROM sys.columns
            WHERE object_id = OBJECT_ID('v_fermeture')
              AND name = 'CUSTOMERNUMBER';
            """;

        var hasCustomerNumber = await connection.ExecuteScalarAsync<int>(sql);

        return hasCustomerNumber > 0
            ? "CUSTOMERNUMBER"
            : "CUSTOMERNUBLER";
    }

    /// <summary>
    /// Récupère les lignes réelles d'une tournée.
    ///
    /// Nouvelle règle :
    /// - codeLivreur sert à identifier le livreur dans v_chauffeurs ;
    /// - les lignes sont récupérées avec codeTournee + jourTournee ;
    /// - v_route_number n'est plus utilisé comme filtre livreur, car id_liv
    ///   ne correspond pas de manière fiable au code chauffeur.
    /// </summary>
    public async Task<IEnumerable<TourneeLigneRecord>> GetTourneeLinesAsync(
        DateOnly dateTournee,
        string codeLivreur,
        string? codeTournee = null)
    {
        using var connection = _connectionFactory.CreateAbssoluteConnection();

        var jourTournee = GetJourTournee(dateTournee);
        var fermetureCustomerColumn = await GetFermetureCustomerColumnAsync(connection);

        var sql = $"""
                WITH TourneeDedoublonnee AS (
                    SELECT
                        t.*,
                        ROW_NUMBER() OVER (
                            PARTITION BY
                                LTRIM(RTRIM(CAST(t.TOURNEE AS NVARCHAR(50)))),
                                t.JOUR_TOURNEE,
                                LTRIM(RTRIM(CAST(t.NUM_CLI AS NVARCHAR(50)))),
                                LTRIM(RTRIM(CAST(t.PDL AS NVARCHAR(50)))),
                                t.ARRET
                            ORDER BY
                                t.ARRET,
                                t.NUM_CLI,
                                t.PDL
                        ) AS RowNum
                    FROM v_tournee AS t
                    WHERE t.JOUR_TOURNEE = @JourTournee
                      AND (
                            @CodeTournee IS NULL
                            OR LTRIM(RTRIM(CAST(t.TOURNEE AS NVARCHAR(50)))) = @CodeTournee
                          )
                ),
                PdlDedoublonnee AS (
                    SELECT
                        p.*,
                        ROW_NUMBER() OVER (
                            PARTITION BY
                                p.NUMEROCLIENT,
                                p.NUMEROPOINTLIVRAISON,
                                p.DAYNUMBER
                            ORDER BY
                                p.NUMEROCLIENT,
                                p.NUMEROPOINTLIVRAISON,
                                p.DAYNUMBER
                        ) AS RowNum
                    FROM v_pdl_jour AS p
                )
                SELECT
                    CAST(t.NUM_CLI AS NVARCHAR(50)) AS NumClient,
                    t.NOM_CLI AS NomClient,

                    t.PDL_DESC AS NomAffiche,

                    CAST(t.PDL AS NVARCHAR(50)) AS CodePDL,
                    t.PDL_DESC AS DescriptionPDL,

                    p.STREET AS AdresseLigne1,
                    p.STREET2 AS AdresseLigne2,
                    p.STREET3 AS AdresseLigne3,
                    p.CITY AS Ville,
                    p.ZIPCODE AS CodePostal,

                    t.ARRET AS OrdreArret,
                    t.ARRET AS Horaire,

                    t.JOUR_TOURNEE AS JourTournee,

                    CAST(t.TOURNEE AS NVARCHAR(50)) AS CodeTournee,
                    t.TOURNEE_DESC AS LibelleTournee,

                    t.JOUR_TOURNEE_RETOUR AS JourTourneeRetour,
                    CAST(t.TOURNEE_RET AS NVARCHAR(50)) AS CodeTourneeRetour,
                    t.TOURNEE_RETOUR_DESC AS LibelleTourneeRetour,

                    t.SCHEMA_LIV AS SchemaLivraison,

                    t.INSTRUCTIONS AS Instructions,
                    CAST(NULL AS NVARCHAR(100)) AS CommentaireFiche,

                    t.ZONE_DECH AS ZoneDechargement,
                    CAST(NULL AS NVARCHAR(100)) AS Zone,

                    CAST(NULL AS NVARCHAR(100)) AS Precision,
                    CAST(NULL AS NVARCHAR(100)) AS Cle,

                    CASE
                        WHEN f.ACTIVE = 1 THEN CAST(1 AS BIT)
                        ELSE CAST(0 AS BIT)
                    END AS EstFerme,

                    f.NONBUSINESSDAY AS DateFermeture,
                    CAST(NULL AS NVARCHAR(255)) AS MotifFermeture
                FROM TourneeDedoublonnee AS t
                LEFT JOIN PdlDedoublonnee AS p
                    ON t.NUM_CLI = p.NUMEROCLIENT
                   AND t.PDL = p.NUMEROPOINTLIVRAISON
                   AND t.JOUR_TOURNEE = p.DAYNUMBER
                   AND p.RowNum = 1
                LEFT JOIN v_fermeture AS f
                    ON t.NUM_CLI = f.{fermetureCustomerColumn}
                   AND f.NONBUSINESSDAY = @DateFermeture
                WHERE t.RowNum = 1
                ORDER BY
                    t.ARRET,
                    t.NUM_CLI,
                    t.PDL;
                """;

        return await connection.QueryAsync<TourneeLigneRecord>(
            sql,
            new
            {
                DateFermeture = dateTournee.ToDateTime(TimeOnly.MinValue),
                JourTournee = jourTournee,
                CodeTournee = string.IsNullOrWhiteSpace(codeTournee)
                    ? null
                    : codeTournee.Trim()
            });
    }

    private static int GetJourTournee(DateOnly dateTournee)
    {
        return dateTournee.DayOfWeek switch
        {
            DayOfWeek.Monday => 1,
            DayOfWeek.Tuesday => 2,
            DayOfWeek.Wednesday => 3,
            DayOfWeek.Thursday => 4,
            DayOfWeek.Friday => 5,
            DayOfWeek.Saturday => 6,
            DayOfWeek.Sunday => 7,
            _ => throw new ArgumentOutOfRangeException(nameof(dateTournee))
        };
    }
}