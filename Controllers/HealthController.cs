using Dapper;
using Microsoft.AspNetCore.Mvc;
using API_ASP.NET_Core.Data;

namespace API_ASP.NET_Core.Controllers;

[ApiController]
[Route("api/health")]
public class HealthController : ControllerBase
{
    private readonly SqlConnectionFactory _connectionFactory;

    public HealthController(SqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new
        {
            service = "API-ASP.NET-Core",
            status = "ok",
            date = DateTime.Now
        });
    }

    [HttpGet("sql")]
    public async Task<IActionResult> CheckSql()
    {
        using var connection = _connectionFactory.CreateConnection();

        var result = await connection.ExecuteScalarAsync<int>("SELECT 1");

        return Ok(new
        {
            sql = result == 1 ? "ok" : "erreur",
            date = DateTime.Now
        });
    }
}