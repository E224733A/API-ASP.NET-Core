using System.Text.Json;

namespace API_ASP.NET_Core.Middleware;

/// <summary>
/// Middleware global pour capturer les exceptions non gérées et retourner une réponse JSON cohérente.
/// </summary>
/// <remarks>
/// Ce middleware agit comme filet de sécurité après les contrôleurs, services et repositories.
/// Il évite d'exposer une exception brute au mobile ou à ServeWeb et renvoie un correlationId
/// exploitable dans les logs pour retrouver l'incident serveur.
/// </remarks>
public class ApiExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ApiExceptionMiddleware> _logger;

    public ApiExceptionMiddleware(RequestDelegate next, ILogger<ApiExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    /// <summary>
    /// Exécute la suite du pipeline HTTP et transforme les exceptions non gérées en erreur API standard.
    /// </summary>
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception exception)
        {
            var correlationId = context.GetCorrelationId() ?? Guid.NewGuid().ToString("D");
            _logger.LogError(exception, "Exception non gérée | CorrelationId: {CorrelationId}", correlationId);

            context.Response.ContentType = "application/json";
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;

            var response = new
            {
                statut = "ERROR",
                code = "SERVER_ERROR",
                message = "Erreur interne du serveur.",
                correlationId = correlationId
            };

            await context.Response.WriteAsJsonAsync(response);
        }
    }
}

/// <summary>
/// Extension pour ajouter le middleware d'exception globale au pipeline ASP.NET Core.
/// </summary>
public static class ApiExceptionMiddlewareExtensions
{
    public static IApplicationBuilder UseApiExceptionHandling(this IApplicationBuilder app)
    {
        return app.UseMiddleware<ApiExceptionMiddleware>();
    }
}
