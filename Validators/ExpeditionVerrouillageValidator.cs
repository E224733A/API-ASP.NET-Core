using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using API_ASP.NET_Core.Models;

namespace API_ASP.NET_Core.Validators;

/// <summary>
/// Validator pour la requête de verrouillage Expédition.
/// Cette classe est introduite pour respecter l’architecture MVC en séparant la validation
/// du payload. Pour l’instant, elle délègue implicitement la validation au service
/// existant afin de ne pas changer le comportement. Elle peut être enrichie ultérieurement.
/// </summary>
public sealed class ExpeditionVerrouillageValidator
{
    /// <summary>
    /// Valide la requête de verrouillage. Actuellement, cette méthode ne fait que
    /// retourner une liste vide, car la validation complète est encore réalisée dans
    /// <see cref="Services.ExpeditionService"/> pour conserver le comportement existant.
    /// </summary>
    /// <param name="request">Requête de verrouillage.</param>
    /// <param name="cancellationToken">Jeton d’annulation.</param>
    /// <returns>Liste des erreurs de validation. Vide si aucune erreur.</returns>
    public Task<IReadOnlyList<string>> ValidateAsync(ExpeditionVerrouillageLotRequest? request, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<string> noErrors = Array.Empty<string>();
        return Task.FromResult(noErrors);
    }
}