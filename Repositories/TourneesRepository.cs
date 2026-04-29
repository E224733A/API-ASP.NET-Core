using Dapper;
using API_ASP.NET_Core.Data;

namespace API_ASP.NET_Core.Repositories;

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

    public async Task<IEnumerable<TourneeLigneRecord>> GetTourneeLinesAsync(
        DateOnly dateTournee,
        string codeLivreur,
        string? codeTournee = null)
    {
        using var connection = _connectionFactory.CreateAbssoluteConnection();

        // Requête mise à jour : les conversions de type garantissent que les chaînes correspondent bien au record.
        var sql = """
            SELECT
                CAST(t.NUM_CLI AS NVARCHAR(50))      AS NumClient,
                t.NOM_CLI                            AS NomClient,
                t.PDL_DESC                           AS NomAffiche,
                CAST(t.PDL AS NVARCHAR(50))          AS CodePDL,
                t.PDL_DESC                           AS DescriptionPDL,

                p.STREET                             AS AdresseLigne1,
                p.STREET2                            AS AdresseLigne2,
                p.STREET3                            AS AdresseLigne3,
                p.CITY                               AS Ville,
                p.ZIPCODE                            AS CodePostal,

                t.ARRET                              AS OrdreArret,
                t.ARRET                              AS Horaire,
                t.JOUR_TOURNEE                       AS JourTournee,
                CAST(t.TOURNEE AS NVARCHAR(50))      AS CodeTournee,
                t.TOURNEE_DESC                       AS LibelleTournee,

                t.JOUR_TOURNEE_RETOUR                AS JourTourneeRetour,
                CAST(t.TOURNEE_RET AS NVARCHAR(50))  AS CodeTourneeRetour,
                t.TOURNEE_RETOUR_DESC                AS LibelleTourneeRetour,

                t.SCHEMA_LIV                         AS SchemaLivraison,
                t.INSTRUCTIONS                       AS Instructions,
                CAST(NULL AS NVARCHAR(100))          AS CommentaireFiche,

                t.ZONE_DECH                          AS ZoneDechargement,
                CAST(NULL AS NVARCHAR(100))          AS Zone,
                CAST(NULL AS NVARCHAR(100))          AS Precision,
                CAST(NULL AS NVARCHAR(100))          AS Cle,

                CASE WHEN f.ACTIVE = 1 THEN CAST(1 AS BIT) ELSE CAST(0 AS BIT) END AS EstFerme,
                f.NONBUSINESSDAY                     AS DateFermeture,
                CAST(NULL AS NVARCHAR(100))          AS MotifFermeture
            FROM v_tournee AS t
            LEFT JOIN v_pdl_jour AS p
                ON t.NUM_CLI      = p.NUMEROCLIENT
               AND t.PDL          = p.NUMEROPOINTLIVRAISON
               AND t.JOUR_TOURNEE = p.DAYNUMBER
            LEFT JOIN v_fermeture AS f
                ON t.NUM_CLI      = f.CUSTOMERNUBLER
               AND f.NONBUSINESSDAY = @Date
            LEFT JOIN v_route_number AS r
                ON t.TOURNEE      = r.tournee
               AND t.JOUR_TOURNEE = r.liv_jour_id
            WHERE (@CodeTournee IS NULL OR t.TOURNEE = @CodeTournee)
              AND r.id_liv = @CodeLivreur
            ORDER BY t.ARRET;
        """;

        return await connection.QueryAsync<TourneeLigneRecord>(sql, new
        {
            Date = dateTournee.ToDateTime(TimeOnly.MinValue),
            CodeLivreur = codeLivreur,
            CodeTournee = codeTournee
        });
    }
}