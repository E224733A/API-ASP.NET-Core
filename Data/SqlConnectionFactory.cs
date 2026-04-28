using Microsoft.Data.SqlClient;

namespace API_ASP.NET_Core.Data;

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