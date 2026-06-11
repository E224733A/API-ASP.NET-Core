using Microsoft.Data.SqlClient;

namespace API_ASP.NET_Core.Data;

/// <summary>
/// Point unique de création des connexions SQL utilisées par l'API.
/// </summary>
/// <remarks>
/// Le projet distingue volontairement la lecture des vues ABSSolute et l'écriture dans
/// les tables Mobile_*. Le mobile et ServeWeb ne doivent jamais accéder directement
/// à SQL Server : ils passent par l'API, qui choisit ici la connexion adaptée au flux.
/// </remarks>
public class SqlConnectionFactory
{
    private readonly IConfiguration _configuration;

    public SqlConnectionFactory(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public SqlConnection CreateAbssoluteConnection()
    {
        var connectionString = _configuration.GetConnectionString("AbssoluteConnection");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("La chaîne de connexion AbssoluteConnection est manquante.");
        }

        return new SqlConnection(connectionString);
    }

    public SqlConnection CreateMobileConnection()
    {
        var connectionString = _configuration.GetConnectionString("MobileConnection");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("La chaîne de connexion MobileConnection est manquante.");
        }

        return new SqlConnection(connectionString);
    }
}
