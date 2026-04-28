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
            environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
            date = DateTime.Now
        });
    }

    [HttpGet("abssolute")]
    public async Task<IActionResult> CheckAbssoluteConnection()
    {
        using var connection = _connectionFactory.CreateAbssoluteConnection();

        var databaseName = await connection.ExecuteScalarAsync<string>("SELECT DB_NAME();");

        return Ok(new
        {
            connection = "AbssoluteConnection",
            status = "ok",
            database = databaseName,
            usage = "Lecture des vues ABSSolute"
        });
    }

    [HttpGet("mobile")]
    public async Task<IActionResult> CheckMobileConnection()
    {
        using var connection = _connectionFactory.CreateMobileConnection();

        var databaseName = await connection.ExecuteScalarAsync<string>("SELECT DB_NAME();");

        return Ok(new
        {
            connection = "MobileConnection",
            status = "ok",
            database = databaseName,
            usage = "Tables dédiées au projet mobile"
        });
    }
}