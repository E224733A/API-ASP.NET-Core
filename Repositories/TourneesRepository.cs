using Dapper;
using API_ASP.NET_Core.Data;

namespace API_ASP.NET_Core.Repositories;

public record TourneeLigneRecord
(
    string NumClient,
    string NomClient,
    string? NomAffiche,
    string? CodePDL,
    string? DescriptionPDL,
    string? AdresseLigne1,
    string? AdresseLigne2,
    string? AdresseLigne3,
    string? Ville,
    string? CodePostal,
    int? OrdreArret,
    int? Horaire,
    int? JourTournee,
    string CodeTournee,
    string? LibelleTournee,
    int? JourTourneeRetour,
    string? CodeTourneeRetour,
    string? LibelleTourneeRetour,
    string? SchemaLivraison,
    string? Instructions,
    string? CommentaireFiche,
    string? ZoneDechargement,
    string? Zone,
    string? Precision,
    string? Cle,
    bool EstFerme,
    DateTime? DateFermeture,
    string? MotifFermeture
);

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

        var sql = """
            SELECT
                -- informations client
                t.NUM_CLI           AS NumClient,
                t.NOM_CLI           AS NomClient,
                t.PDL_DESC          AS NomAffiche,
                t.PDL               AS CodePDL,
                t.PDL_DESC          AS DescriptionPDL,

                -- informations adresse (vue v_pdl_jour)
                p.STREET            AS AdresseLigne1,
                p.STREET2           AS AdresseLigne2,
                p.STREET3           AS AdresseLigne3,
                p.CITY              AS Ville,
                p.ZIPCODE           AS CodePostal,

                -- informations de tournée
                t.ARRET             AS OrdreArret,
                t.ARRET             AS Horaire,
                t.JOUR_TOURNEE      AS JourTournee,
                t.TOURNEE           AS CodeTournee,
                t.TOURNEE_DESC      AS LibelleTournee,

                -- informations de tournée retour
                t.JOUR_TOURNEE_RETOUR AS JourTourneeRetour,
                t.TOURNEE_RET         AS CodeTourneeRetour,
                t.TOURNEE_RETOUR_DESC AS LibelleTourneeRetour,

                t.SCHEMA_LIV         AS SchemaLivraison,
                t.INSTRUCTIONS       AS Instructions,
                NULL                 AS CommentaireFiche,

                t.ZONE_DECH          AS ZoneDechargement,
                NULL                 AS Zone,
                NULL                 AS Precision,
                NULL                 AS Cle,

                -- informations de fermeture (vue v_fermeture)
                CASE WHEN f.ACTIVE = 1 THEN CAST(1 AS BIT) ELSE CAST(0 AS BIT) END AS EstFerme,
                f.NONBUSINESSDAY     AS DateFermeture,
                NULL                 AS MotifFermeture
            FROM v_tournee AS t

            -- jointure adresse : on fait correspondre le client, la PDL et le jour
            LEFT JOIN v_pdl_jour AS p
                ON t.NUM_CLI          = p.NUMEROCLIENT
               AND t.PDL              = p.NUMEROPOINTLIVRAISON
               AND t.JOUR_TOURNEE     = p.DAYNUMBER

            -- jointure fermeture : correspondance client + date de fermeture
            LEFT JOIN v_fermeture AS f
                ON t.NUM_CLI          = f.CUSTOMERNUBLER
               AND f.NONBUSINESSDAY   = @Date

            -- jointure pour filtrer le livreur via la table de routing
            LEFT JOIN v_route_number AS r
                ON t.TOURNEE          = r.tournee
               AND t.JOUR_TOURNEE     = r.liv_jour_id

            WHERE (@CodeTournee IS NULL OR t.TOURNEE = @CodeTournee)
              AND r.id_liv = @CodeLivreur    -- filtre sur le livreur
            ORDER BY t.ARRET;
            """;

        return await connection.QueryAsync<TourneeLigneRecord>(sql, new
        {
            // on convertit DateOnly en DateTime pour comparer la date de fermeture
            Date = dateTournee.ToDateTime(TimeOnly.MinValue),
            CodeLivreur = codeLivreur,
            CodeTournee = codeTournee
        });
    }
}