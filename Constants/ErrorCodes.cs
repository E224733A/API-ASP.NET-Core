namespace API_ASP.NET_Core.Constants;

/// <summary>
/// Codes de retour contractuels exposés par l'API aux clients MobileSLI et ServeWeb.
/// </summary>
/// <remarks>
/// Ces valeurs font partie du contrat JSON : les modifier peut casser l'interprétation
/// des erreurs côté application mobile, module Expédition ou scripts de validation.
/// Ajouter un nouveau code est possible, mais renommer ou recycler un code existant
/// doit être traité comme une évolution de contrat.
/// </remarks>
public static class ApiErrorCodes
{
    public const string ValidationError = "VALIDATION_ERROR";
    public const string Conflict = "CONFLICT";
    public const string Error = "ERROR";
    public const string NotFound = "NOT_FOUND";
    public const string Success = "SUCCESS";

    public const string SynchronisationAlreadyExists = "SYNCHRONISATION_ALREADY_EXISTS";
    public const string TourneeAlreadySent = "TOURNEE_ALREADY_SENT";
    public const string TechnicalError = "TECHNICAL_ERROR";
}
