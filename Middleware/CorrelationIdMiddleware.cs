namespace API_ASP.NET_Core.Middleware;

/// <summary>
/// Middleware pour gérer un identifiant de corrélation par requête.
/// Permet de tracer les requêtes à travers les logs et les erreurs.
/// </summary>
public class CorrelationIdMiddleware
{
    private const string CorrelationIdHeaderName = "X-Correlation-Id";
    private const string CorrelationIdItemsKey = "CorrelationId";

    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Récupérer le CorrelationId du header, ou générer un nouveau GUID
        var correlationId = GetOrGenerateCorrelationId(context);

        // Stocker dans HttpContext.Items pour l'utiliser dans les middlewares suivants
        context.Items[CorrelationIdItemsKey] = correlationId;

        // Ajouter le header à la réponse
        context.Response.Headers[CorrelationIdHeaderName] = correlationId;

        _logger.LogInformation("Requête reçue : {CorrelationId} {Method} {Path}",
            correlationId, context.Request.Method, context.Request.Path);

        await _next(context);

        _logger.LogInformation("Réponse envoyée : {CorrelationId} {StatusCode}",
            correlationId, context.Response.StatusCode);
    }

    private string GetOrGenerateCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue("X-Correlation-Id", out var headerValue))
        {
            var correlationId = headerValue.ToString();
            if (!string.IsNullOrWhiteSpace(correlationId))
            {
                return correlationId;
            }
        }

        return Guid.NewGuid().ToString("D");
    }
}

/// <summary>
/// Extension pour ajouter facilement le middleware de CorrelationId au pipeline.
/// </summary>
public static class CorrelationIdMiddlewareExtensions
{
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder app)
    {
        return app.UseMiddleware<CorrelationIdMiddleware>();
    }

    /// <summary>
    /// Récupère le CorrelationId depuis HttpContext.Items.
    /// À utiliser dans les contrôleurs ou services.
    /// </summary>
    public static string? GetCorrelationId(this HttpContext context)
    {
        if (context.Items.TryGetValue("CorrelationId", out var value))
        {
            return value?.ToString();
        }
        return null;
    }
}
