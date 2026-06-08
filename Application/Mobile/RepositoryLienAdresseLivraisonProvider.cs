using API_ASP.NET_Core.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace API_ASP.NET_Core.Application.Mobile;

/// <summary>
/// Provider final basé sur la vue SQL réutilisable :
/// [lavinprosli].[dbo].[v_Mobile_AdresseLivraison].
/// La vue doit exposer NUM_CLI, CodePDL et AdresseLivraison.
/// AdresseLivraison doit contenir un lien Google Maps déjà construit.
/// </summary>
public sealed class RepositoryLienAdresseLivraisonProvider : ILienAdresseLivraisonProvider
{
    private readonly SqlConnectionFactory _connectionFactory;
    private readonly ILogger<RepositoryLienAdresseLivraisonProvider> _logger;
    private readonly LiensAdresseLivraisonOptions _options;

    public RepositoryLienAdresseLivraisonProvider(
        SqlConnectionFactory connectionFactory,
        ILogger<RepositoryLienAdresseLivraisonProvider> logger,
        IOptions<LiensAdresseLivraisonOptions> options)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
        _options = options.Value;
    }

    public async Task<string?> GetLienAdresseLivraisonAsync(
        string? numCli,
        string? codePdl,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled
            || string.IsNullOrWhiteSpace(numCli)
            || string.IsNullOrWhiteSpace(codePdl))
        {
            return null;
        }

        var trimmedNumCli = numCli.Trim();
        var trimmedCodePdl = codePdl.Trim();

        try
        {
            using var connection = _connectionFactory.CreateAbssoluteConnection();

            const string sql = """
                SELECT TOP (1)
                    NULLIF(LTRIM(RTRIM(CAST(AdresseLivraison AS NVARCHAR(2048)))), N'') AS AdresseLivraison
                FROM [lavinprosli].[dbo].[v_Mobile_AdresseLivraison]
                WHERE LTRIM(RTRIM(CAST(NUM_CLI AS NVARCHAR(50)))) = @NumCli
                  AND LTRIM(RTRIM(CAST(CodePDL AS NVARCHAR(100)))) = @CodePDL;
                """;

            var url = await connection.QuerySingleOrDefaultAsync<string?>(
                new CommandDefinition(
                    sql,
                    new
                    {
                        NumCli = trimmedNumCli,
                        CodePDL = trimmedCodePdl
                    },
                    cancellationToken: cancellationToken));

            return LienAdresseLivraisonUrlValidator.NormalizeUrl(url);
        }
        catch (Exception exception) when (exception is SqlException or InvalidOperationException)
        {
            _logger.LogWarning(
                exception,
                "Source finale des liens d'adresse de livraison indisponible pour le client {NumCli} et le CodePDL {CodePDL}.",
                trimmedNumCli,
                trimmedCodePdl);

            return null;
        }
    }
}
