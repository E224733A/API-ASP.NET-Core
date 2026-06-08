using API_ASP.NET_Core.Data;
using Dapper;
using Microsoft.Data.SqlClient;

namespace API_ASP.NET_Core.Application.Mobile;

/// <summary>
/// Provider final basé sur la source métier ERP/ABSSolute.
/// La vue finale n'étant pas encore disponible, ce provider est tolérant :
/// en cas de source absente ou de colonne différente, il retourne null et ne bloque jamais le chargement mobile.
/// </summary>
public sealed class RepositoryLienAdresseLivraisonProvider : ILienAdresseLivraisonProvider
{
    private readonly SqlConnectionFactory _connectionFactory;
    private readonly ILogger<RepositoryLienAdresseLivraisonProvider> _logger;

    public RepositoryLienAdresseLivraisonProvider(
        SqlConnectionFactory connectionFactory,
        ILogger<RepositoryLienAdresseLivraisonProvider> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task<AdresseLivraisonInfo?> GetAdresseLivraisonAsync(
        string? codePdl,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(codePdl))
        {
            return null;
        }

        try
        {
            using var connection = _connectionFactory.CreateAbssoluteConnection();

            /*
             * Source attendue après livraison par l'ERP :
             * - CodePDL
             * - LatitudeLivraison  : latitude GPS WGS84, exemple 4.9224
             * - LongitudeLivraison : longitude GPS WGS84, exemple -52.3135
             *
             * Si la vue finale porte un autre nom, modifier uniquement le FROM.
             * Si les colonnes portent un autre nom, garder les alias SQL LatitudeLivraison / LongitudeLivraison.
             */
            const string sql = """
                SELECT TOP (1)
                    TRY_CONVERT(float, LatitudeLivraison) AS LatitudeLivraison,
                    TRY_CONVERT(float, LongitudeLivraison) AS LongitudeLivraison
                FROM [lavinprosli].[dbo].[v_Mobile_CoordonneesLivraison]
                WHERE LTRIM(RTRIM(CAST(CodePDL AS NVARCHAR(100)))) = @CodePDL;
                """;

            var record = await connection.QuerySingleOrDefaultAsync<AdresseLivraisonRepositoryRecord>(
                new CommandDefinition(
                    sql,
                    new
                    {
                        CodePDL = codePdl.Trim()
                    },
                    cancellationToken: cancellationToken));

            return LienAdresseLivraisonUrlValidator.CreateInfo(
                record?.LatitudeLivraison,
                record?.LongitudeLivraison);
        }
        catch (Exception exception) when (exception is SqlException or InvalidOperationException)
        {
            _logger.LogWarning(
                exception,
                "Source finale des coordonnées GPS de livraison indisponible pour le CodePDL {CodePDL}.",
                codePdl);

            return null;
        }
    }

    private sealed class AdresseLivraisonRepositoryRecord
    {
        public double? LatitudeLivraison { get; init; }
        public double? LongitudeLivraison { get; init; }
    }
}
