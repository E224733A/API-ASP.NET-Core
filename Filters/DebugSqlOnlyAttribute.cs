using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace API_ASP.NET_Core.Filters;

/// <summary>
/// Filtre d'action réservé aux routes de diagnostic SQL.
/// </summary>
/// <remarks>
/// Les routes annotées avec ce filtre exposent des informations techniques sur les schémas
/// ou les vues SQL. Quand DebugSql est désactivé, l'API retourne 404 afin que ces routes
/// ne soient pas visibles dans un usage normal.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class DebugSqlOnlyAttribute : Attribute, IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var configuration = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>();
        var debugSqlEnabled = configuration.GetValue<bool>("DebugSql:Enabled", defaultValue: true);

        if (!debugSqlEnabled)
        {
            context.Result = new NotFoundResult();
            return;
        }

        await next();
    }
}
