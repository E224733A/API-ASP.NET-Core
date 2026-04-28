using Dapper;
using Microsoft.AspNetCore.Mvc;
using API_ASP.NET_Core.Data;

namespace API_ASP.NET_Core.Controllers;

[ApiController]
[Route("api/debug/sql")]
public class DebugSqlController : ControllerBase
{
    private readonly SqlConnectionFactory _connectionFactory;

    public DebugSqlController(SqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    [HttpGet("tables-mobile")]
    public async Task<IActionResult> GetTablesMobile()
    {
        using var connection = _connectionFactory.CreateMobileConnection();

        var tables = await connection.QueryAsync<string>("""
            SELECT name
            FROM sys.tables
            WHERE name LIKE 'Mobile_%'
            ORDER BY name;
            """);

        return Ok(tables);
    }

    [HttpGet("tables-mobile/details")]
    public async Task<IActionResult> GetTablesMobileDetails()
    {
        using var connection = _connectionFactory.CreateMobileConnection();

        var tables = await connection.QueryAsync("""
            SELECT 
                t.name AS TableName,
                SUM(p.rows) AS NombreLignes
            FROM sys.tables t
            INNER JOIN sys.partitions p ON t.object_id = p.object_id
            WHERE t.name LIKE 'Mobile_%'
              AND p.index_id IN (0, 1)
            GROUP BY t.name
            ORDER BY t.name;
            """);

        return Ok(tables);
    }

    [HttpGet("contraintes-mobile")]
    public async Task<IActionResult> GetContraintesMobile()
    {
        using var connection = _connectionFactory.CreateMobileConnection();

        var contraintes = await connection.QueryAsync("""
            SELECT 
                t.name AS TableName,
                c.name AS ConstraintName,
                c.type_desc AS ConstraintType
            FROM sys.objects c
            INNER JOIN sys.tables t ON c.parent_object_id = t.object_id
            WHERE t.name LIKE 'Mobile_%'
              AND c.type IN ('C', 'F', 'PK', 'UQ')
            ORDER BY t.name, c.type_desc, c.name;
            """);

        return Ok(contraintes);
    }

    [HttpGet("index-mobile")]
    public async Task<IActionResult> GetIndexMobile()
    {
        using var connection = _connectionFactory.CreateMobileConnection();

        var index = await connection.QueryAsync("""
            SELECT 
                t.name AS TableName,
                i.name AS IndexName,
                i.is_unique AS EstUnique,
                i.filter_definition AS Filtre
            FROM sys.indexes i
            INNER JOIN sys.tables t ON i.object_id = t.object_id
            WHERE t.name LIKE 'Mobile_%'
              AND i.name IS NOT NULL
            ORDER BY t.name, i.name;
            """);

        return Ok(index);
    }

    [HttpGet("vues-abssolute")]
    public async Task<IActionResult> GetVuesAbssolute()
    {
        using var connection = _connectionFactory.CreateAbssoluteConnection();

        var vues = await connection.QueryAsync("""
            SELECT 
                TABLE_SCHEMA AS SchemaName,
                TABLE_NAME AS ViewName
            FROM INFORMATION_SCHEMA.VIEWS
            ORDER BY TABLE_NAME;
            """);

        return Ok(vues);
    }

    [HttpGet("chauffeurs")]
    public async Task<IActionResult> GetChauffeurs()
    {
        using var connection = _connectionFactory.CreateAbssoluteConnection();

        var chauffeurs = await connection.QueryAsync("""
            SELECT TOP 10 *
            FROM v_chauffeurs;
            """);

        return Ok(chauffeurs);
    }

    [HttpGet("tournees")]
    public async Task<IActionResult> GetTournees()
    {
        using var connection = _connectionFactory.CreateAbssoluteConnection();

        var tournees = await connection.QueryAsync("""
            SELECT TOP 10 *
            FROM v_tournee;
            """);

        return Ok(tournees);
    }
}