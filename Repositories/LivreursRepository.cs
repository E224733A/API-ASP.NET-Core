// Repositories/LivreursRepository.cs
using Dapper;
using Microsoft.Data.SqlClient;
using API_ASP.NET_Core.Data;
using API_ASP.NET_Core.Models;

namespace API_ASP.NET_Core.Repositories;

public class LivreursRepository
{
    private readonly SqlConnectionFactory _connectionFactory;

    public LivreursRepository(SqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IEnumerable<LivreurDto>> GetAllAsync()
    {
        using SqlConnection connection = _connectionFactory.CreateAbssoluteConnection();

        const string sql = """
            SELECT DISTINCT
                DRIVERNUMERO AS CodeLivreur,
                DRIVERNAME   AS NomLivreur
            FROM v_chauffeurs
            ORDER BY DRIVERNUMERO;
            """;

        return await connection.QueryAsync<LivreurDto>(sql);
    }
}