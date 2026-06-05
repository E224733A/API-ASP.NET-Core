using API_ASP.NET_Core.Data;
using Dapper;
using Microsoft.Data.SqlClient;

namespace API_ASP.NET_Core.Application.Mobile;

/// <summary>
/// Provider prévu pour la version finale basée sur une source métier ABSSolute par CodePDL.
/// La vue finale n'étant pas encore disponible, ce provider est tolérant : en cas de source absente,
/// il retourne null et ne bloque jamais le chargement de tournée mobile.
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

    public async Task<string?> GetLienAdresseLivraisonAsync(
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

            const string sql = """
                SELECT TOP (1)
                    NULLIF(LTRIM(RTRIM(CAST(LienAdresseLivraison AS NVARCHAR(2048)))), N'') AS LienAdresseLivraison
                FROM [lavinprosli].[dbo].[v_Mobile_LienAdresseLivraison]
                WHERE LTRIM(RTRIM(CAST(CodePDL AS NVARCHAR(100)))) = @CodePDL
                  AND ISNULL(EstActif, 1) = 1
                ORDER BY DateModification DESC;
                """;

            var url = await connection.QuerySingleOrDefaultAsync<string?>(
                new CommandDefinition(
                    sql,
                    new
                    {
                        CodePDL = codePdl.Trim()
                    },
                    cancellationToken: cancellationToken));

            return LienAdresseLivraisonUrlValidator.NormalizeUrl(url);
        }
        catch (Exception exception) when (exception is SqlException or InvalidOperationException)
        {
            _logger.LogWarning(
                exception,
                "Source finale des liens adresse livraison indisponible pour le CodePDL {CodePDL}.",
                codePdl);

            return null;
        }
    }
}
