using Dapper;
using API_ASP.NET_Core.Data;
using System.Data;

namespace API_ASP.NET_Core.Repositories;

public record LivreurRecord
{
    public string CodeLivreur { get; init; } = string.Empty;
    public string NomLivreur { get; init; } = string.Empty;
}

public record TourneeDisponibleRecord
{
    public string CodeTournee { get; init; } = string.Empty;
    public string? LibelleTournee { get; init; }
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

    /// <summary>
    /// Renvoie uniquement les tournées disponibles pour un jour.
    /// Cette méthode est volontairement légère : elle ne renvoie pas les clients.
    /// </summary>
    public async Task<IEnumerable<TourneeDisponibleRecord>> GetTourneesDisponiblesAsync(
        DateOnly dateTournee)
    {
        using var connection = _connectionFactory.CreateAbssoluteConnection();

        var jourTournee = GetJourTournee(dateTournee);

        const string sql = """
            SELECT
                LTRIM(RTRIM(CAST(t.TOURNEE AS NVARCHAR(50)))) AS CodeTournee,
                MAX(t.TOURNEE_DESC) AS LibelleTournee
            FROM v_tournee AS t
            WHERE TRY_CONVERT(INT, LTRIM(RTRIM(CAST(t.JOUR_TOURNEE AS NVARCHAR(50))))) = @JourTournee
              AND t.TOURNEE IS NOT NULL
              AND LTRIM(RTRIM(CAST(t.TOURNEE AS NVARCHAR(50)))) <> ''
            GROUP BY
                LTRIM(RTRIM(CAST(t.TOURNEE AS NVARCHAR(50))))
            ORDER BY
                TRY_CONVERT(INT, LTRIM(RTRIM(CAST(t.TOURNEE AS NVARCHAR(50))))),
                LTRIM(RTRIM(CAST(t.TOURNEE AS NVARCHAR(50))));
            """;

        return await connection.QueryAsync<TourneeDisponibleRecord>(
            sql,
            new
            {
                JourTournee = jourTournee
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
                                LTRIM(RTRIM(CAST(t.JOUR_TOURNEE AS NVARCHAR(50)))),
                                LTRIM(RTRIM(CAST(t.NUM_CLI AS NVARCHAR(50)))),
                                LTRIM(RTRIM(CAST(t.PDL AS NVARCHAR(50)))),
                                LTRIM(RTRIM(CAST(t.ARRET AS NVARCHAR(50))))
                            ORDER BY
                                TRY_CONVERT(INT, t.ARRET),
                                LTRIM(RTRIM(CAST(t.ARRET AS NVARCHAR(50)))),
                                LTRIM(RTRIM(CAST(t.NUM_CLI AS NVARCHAR(50)))),
                                LTRIM(RTRIM(CAST(t.PDL AS NVARCHAR(50))))
                        ) AS RowNum
                    FROM v_tournee AS t
                    WHERE TRY_CONVERT(INT, LTRIM(RTRIM(CAST(t.JOUR_TOURNEE AS NVARCHAR(50))))) = @JourTournee
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
                                LTRIM(RTRIM(CAST(p.NUMEROCLIENT AS NVARCHAR(50)))),
                                LTRIM(RTRIM(CAST(p.NUMEROPOINTLIVRAISON AS NVARCHAR(50)))),
                                LTRIM(RTRIM(CAST(p.DAYNUMBER AS NVARCHAR(50))))
                            ORDER BY
                                LTRIM(RTRIM(CAST(p.NUMEROCLIENT AS NVARCHAR(50)))),
                                LTRIM(RTRIM(CAST(p.NUMEROPOINTLIVRAISON AS NVARCHAR(50)))),
                                TRY_CONVERT(INT, LTRIM(RTRIM(CAST(p.DAYNUMBER AS NVARCHAR(50)))))
                        ) AS RowNum
                    FROM v_pdl_jour AS p
                )
                SELECT
                    LTRIM(RTRIM(CAST(t.NUM_CLI AS NVARCHAR(50)))) AS NumClient,
                    t.NOM_CLI AS NomClient,

                    t.PDL_DESC AS NomAffiche,

                    LTRIM(RTRIM(CAST(t.PDL AS NVARCHAR(50)))) AS CodePDL,
                    t.PDL_DESC AS DescriptionPDL,

                    p.STREET AS AdresseLigne1,
                    p.STREET2 AS AdresseLigne2,
                    p.STREET3 AS AdresseLigne3,
                    p.CITY AS Ville,
                    p.ZIPCODE AS CodePostal,

                    TRY_CONVERT(INT, t.ARRET) AS OrdreArret,
                    TRY_CONVERT(INT, t.ARRET) AS Horaire,

                    TRY_CONVERT(INT, LTRIM(RTRIM(CAST(t.JOUR_TOURNEE AS NVARCHAR(50))))) AS JourTournee,

                    LTRIM(RTRIM(CAST(t.TOURNEE AS NVARCHAR(50)))) AS CodeTournee,
                    t.TOURNEE_DESC AS LibelleTournee,

                    TRY_CONVERT(INT, LTRIM(RTRIM(CAST(t.JOUR_TOURNEE_RETOUR AS NVARCHAR(50))))) AS JourTourneeRetour,
                    LTRIM(RTRIM(CAST(t.TOURNEE_RET AS NVARCHAR(50)))) AS CodeTourneeRetour,
                    t.TOURNEE_RETOUR_DESC AS LibelleTourneeRetour,

                    t.SCHEMA_LIV AS SchemaLivraison,

                    t.INSTRUCTIONS AS Instructions,
                    CAST(NULL AS NVARCHAR(100)) AS CommentaireFiche,

                    t.ZONE_DECH AS ZoneDechargement,
                    CAST(NULL AS NVARCHAR(100)) AS Zone,

                    CAST(NULL AS NVARCHAR(100)) AS Precision,
                    CAST(NULL AS NVARCHAR(100)) AS Cle,

                    CASE
                        WHEN UPPER(LTRIM(RTRIM(CAST(f.ACTIVE AS NVARCHAR(20))))) IN ('1', 'Y', 'YES', 'O', 'OUI', 'TRUE', 'VRAI')
                            THEN CAST(1 AS BIT)
                        ELSE CAST(0 AS BIT)
                    END AS EstFerme,

                    TRY_CONVERT(DATETIME, f.NONBUSINESSDAY) AS DateFermeture,
                    CAST(NULL AS NVARCHAR(255)) AS MotifFermeture
                FROM TourneeDedoublonnee AS t
                LEFT JOIN PdlDedoublonnee AS p
                    ON LTRIM(RTRIM(CAST(t.NUM_CLI AS NVARCHAR(50)))) = LTRIM(RTRIM(CAST(p.NUMEROCLIENT AS NVARCHAR(50))))
                   AND LTRIM(RTRIM(CAST(t.PDL AS NVARCHAR(50)))) = LTRIM(RTRIM(CAST(p.NUMEROPOINTLIVRAISON AS NVARCHAR(50))))
                   AND TRY_CONVERT(INT, LTRIM(RTRIM(CAST(t.JOUR_TOURNEE AS NVARCHAR(50)))))
                        = TRY_CONVERT(INT, LTRIM(RTRIM(CAST(p.DAYNUMBER AS NVARCHAR(50)))))
                   AND p.RowNum = 1
                LEFT JOIN v_fermeture AS f
                    ON LTRIM(RTRIM(CAST(t.NUM_CLI AS NVARCHAR(50)))) = LTRIM(RTRIM(CAST(f.{fermetureCustomerColumn} AS NVARCHAR(50))))
                   AND TRY_CONVERT(DATE, f.NONBUSINESSDAY) = @DateFermeture
                WHERE t.RowNum = 1
                ORDER BY
                    TRY_CONVERT(INT, t.ARRET),
                    LTRIM(RTRIM(CAST(t.ARRET AS NVARCHAR(50)))),
                    LTRIM(RTRIM(CAST(t.NUM_CLI AS NVARCHAR(50)))),
                    LTRIM(RTRIM(CAST(t.PDL AS NVARCHAR(50))));
                """;

        return await connection.QueryAsync<TourneeLigneRecord>(
            sql,
            new
            {
                DateFermeture = dateTournee.ToDateTime(TimeOnly.MinValue).Date,
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
