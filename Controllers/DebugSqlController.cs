using Dapper;
using Microsoft.AspNetCore.Mvc;
using API_ASP.NET_Core.Data;
using API_ASP.NET_Core.Filters;

namespace API_ASP.NET_Core.Controllers;

/// <summary>
/// Contrôleur de diagnostic SQL réservé aux vérifications techniques.
/// </summary>
/// <remarks>
/// Attention : ces routes exposent des informations de schéma ou des extraits de vues SQL.
/// Elles ne portent aucune fonctionnalité métier mobile ou Expédition. Leur accès dépend
/// du filtre <see cref="DebugSqlOnlyAttribute"/> et de la configuration DebugSql:Enabled.
/// </remarks>
[ApiController]
[Route("api/debug/sql")]
[DebugSqlOnly]
public class DebugSqlController : ControllerBase
{
    private readonly SqlConnectionFactory _connectionFactory;

    public DebugSqlController(SqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    /// <summary>
    /// Liste les tables Mobile_* visibles dans la base mobile.
    /// </summary>
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

    /// <summary>
    /// Retourne la liste déclarative des vues ABSSolute utilisées ou vérifiées par l'API.
    /// </summary>
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

    /// <summary>
    /// Affiche un extrait brut de la vue des chauffeurs pour diagnostic de mapping livreur.
    /// </summary>
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

    /// <summary>
    /// Affiche un extrait brut de la vue des tournées pour diagnostic de chargement mobile et Expédition.
    /// </summary>
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

    /// <summary>
    /// Affiche un extrait brut de la vue clients pour diagnostic des informations client.
    /// </summary>
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

    /// <summary>
    /// Affiche un extrait brut des points de livraison par jour pour diagnostic des adresses et PDL.
    /// </summary>
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

    /// <summary>
    /// Affiche un extrait brut des fermetures client pour diagnostic du statut client fermé.
    /// </summary>
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

    /// <summary>
    /// Affiche un extrait brut de la vue jour client pour diagnostic des règles de tournée.
    /// </summary>
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

    /// <summary>
    /// Affiche un extrait brut de la vue route number pour diagnostic des codes de tournée.
    /// </summary>
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

    /// <summary>
    /// Affiche un extrait brut de la vue articles pour diagnostic des produits saisissables.
    /// </summary>
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

    /// <summary>
    /// Affiche un extrait brut de la vue produits ABSSolute pour comparaison avec les articles.
    /// </summary>
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
