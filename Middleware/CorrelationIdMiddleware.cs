namespace API_ASP.NET_Core.Middleware;

/// <summary>
/// Middleware chargé de propager un identifiant de corrélation par requête.
/// </summary>
/// <remarks>
/// L'identifiant permet de relier une requête mobile ou ServeWeb aux logs API.
/// Si le client fournit X-Correlation-Id, il est conservé ; sinon l'API génère un GUID.
/// </remarks>
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

    /// <summary>
    /// Ajoute le correlationId au contexte HTTP, à la réponse et aux logs.
    /// </summary>
    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = GetOrGenerateCorrelationId(context);

        context.Items[CorrelationIdItemsKey] = correlationId;

        context.Response.Headers[CorrelationIdHeaderName] = correlationId;

        _logger.LogInformation("Requête reçue : {CorrelationId} {Method} {Path}",
            correlationId, context.Request.Method, context.Request.Path);

        await _next(context);

        _logger.LogInformation("Réponse envoyée : {CorrelationId} {StatusCode}",
            correlationId, context.Response.StatusCode);
    }

    /// <summary>
    /// Récupère l'identifiant fourni par le client ou génère un GUID.
    /// </summary>
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
/// Extension pour ajouter le middleware de correlationId au pipeline ASP.NET Core.
/// </summary>
public static class CorrelationIdMiddlewareExtensions
{
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder app)
    {
        return app.UseMiddleware<CorrelationIdMiddleware>();
    }

    /// <summary>
    /// Récupère le correlationId depuis HttpContext.Items.
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
