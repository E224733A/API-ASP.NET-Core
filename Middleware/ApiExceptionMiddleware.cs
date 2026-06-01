using System.Text.Json;

namespace API_ASP.NET_Core.Middleware;

/// <summary>
/// Middleware global pour capturer les exceptions non gérées et retourner une réponse JSON cohérente.
/// Agit comme filet de sécurité après les autres middlewares et services.
/// </summary>
public class ApiExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ApiExceptionMiddleware> _logger;

    public ApiExceptionMiddleware(RequestDelegate next, ILogger<ApiExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

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
/// Extension pour ajouter facilement le middleware d'exception globale au pipeline.
/// </summary>
public static class ApiExceptionMiddlewareExtensions
{
    public static IApplicationBuilder UseApiExceptionHandling(this IApplicationBuilder app)
    {
        return app.UseMiddleware<ApiExceptionMiddleware>();
    }
}
