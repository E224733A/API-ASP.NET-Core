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

    [HttpGet("vues-abssolute")]
    public IActionResult GetVuesAbssoluteConnues()
    {
        var vues = new[]
        {
            "v_tournee",
            "v_fermeture",
            "v_chauffeurs",
            "v_clients",
            "v_pdl_jour",
            "v_liste_article",
            "v_liste_produit_abssolute",
            "v_jour_client",
            "v_route_number"
        };

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

    [HttpGet("clients")]
    public async Task<IActionResult> GetClients()
    {
        using var connection = _connectionFactory.CreateAbssoluteConnection();

        var clients = await connection.QueryAsync("""
            SELECT TOP 10 *
            FROM v_clients;
            """);

        return Ok(clients);
    }

    [HttpGet("pdl-jour")]
    public async Task<IActionResult> GetPdlJour()
    {
        using var connection = _connectionFactory.CreateAbssoluteConnection();

        var pdlJour = await connection.QueryAsync("""
            SELECT TOP 10 *
            FROM v_pdl_jour;
            """);

        return Ok(pdlJour);
    }

    [HttpGet("fermetures")]
    public async Task<IActionResult> GetFermetures()
    {
        using var connection = _connectionFactory.CreateAbssoluteConnection();

        var fermetures = await connection.QueryAsync("""
            SELECT TOP 10 *
            FROM v_fermeture;
            """);

        return Ok(fermetures);
    }

    [HttpGet("jour-client")]
    public async Task<IActionResult> GetJourClient()
    {
        using var connection = _connectionFactory.CreateAbssoluteConnection();

        var joursClient = await connection.QueryAsync("""
            SELECT TOP 10 *
            FROM v_jour_client;
            """);

        return Ok(joursClient);
    }

    [HttpGet("route-number")]
    public async Task<IActionResult> GetRouteNumber()
    {
        using var connection = _connectionFactory.CreateAbssoluteConnection();

        var routes = await connection.QueryAsync("""
            SELECT TOP 10 *
            FROM v_route_number;
            """);

        return Ok(routes);
    }

    [HttpGet("articles")]
    public async Task<IActionResult> GetArticles()
    {
        using var connection = _connectionFactory.CreateAbssoluteConnection();

        var articles = await connection.QueryAsync("""
            SELECT TOP 10 *
            FROM v_liste_article;
            """);

        return Ok(articles);
    }

    [HttpGet("produits-abssolute")]
    public async Task<IActionResult> GetProduitsAbssolute()
    {
        using var connection = _connectionFactory.CreateAbssoluteConnection();

        var produits = await connection.QueryAsync("""
            SELECT TOP 10 *
            FROM v_liste_produit_abssolute;
            """);

        return Ok(produits);
    }
}