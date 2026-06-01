using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace API_ASP.NET_Core.Filters;

/// <summary>
/// Filtre d'action pour protéger les routes de débogage SQL.
/// 
/// Comportement :
/// - Si la configuration "DebugSql:Enabled" = true : autorise l'accès
/// - Si la configuration "DebugSql:Enabled" = false : retourne 404 NotFound
/// - En production, ce filtre masque l'existence même du contrôleur
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class DebugSqlOnlyAttribute : Attribute, IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        // Récupérer la configuration
        var configuration = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>();
        var debugSqlEnabled = configuration.GetValue<bool>("DebugSql:Enabled", defaultValue: true);

        // Si DebugSql est désactivé, retourner 404
        if (!debugSqlEnabled)
        {
            context.Result = new NotFoundResult();
            return;
        }

        // Sinon, continuer normalement
        await next();
    }
}
