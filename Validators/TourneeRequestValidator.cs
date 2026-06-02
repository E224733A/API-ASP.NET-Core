using API_ASP.NET_Core.Models;
using API_ASP.NET_Core.Services;

namespace API_ASP.NET_Core.Validators;

/// <summary>
/// Validateur pour les requêtes GET Tournées.
/// Responsabilité : valider les paramètres de query et les règles métier liées aux dates.
/// </summary>
public sealed class TourneeRequestValidator
{
    private readonly DateMetierService _dateMetierService;

    public TourneeRequestValidator(DateMetierService dateMetierService)
    {
        _dateMetierService = dateMetierService;
    }

    /// <summary>
    /// Vérifie qu'aucun paramètre de date n'est présent dans la requête.
    /// Retourne le nom du paramètre interdit s'il existe, null sinon.
    /// </summary>
    public string? ValidateNoDatesInQuery(IQueryCollection query)
    {
        return query.Keys.FirstOrDefault(key =>
            string.Equals(key, "date", StringComparison.OrdinalIgnoreCase)
            || string.Equals(key, "dateTournee", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Construit une réponse d'erreur cohérente pour un paramètre de date interdit.
    /// </summary>
    public object BuildDateQueryForbiddenResponse(string parametreDateInterdit)
    {
        var dateAutorisee = _dateMetierService.GetDateTourneeAutorisee();

        return new
        {
            statut = "VALIDATION_ERROR",
            code = "DATE_QUERY_PARAM_INTERDIT",
            message = "La date de tournée n'est pas acceptée dans l'URL. Elle est calculée côté API avec la date métier Europe/Paris.",
            parametreInterdit = parametreDateInterdit,
            dateTourneeAutorisee = dateAutorisee.ToString("yyyy-MM-dd")
        };
    }
}
