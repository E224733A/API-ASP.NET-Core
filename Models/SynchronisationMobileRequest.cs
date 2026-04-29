namespace API_ASP.NET_Core.Models;

public class SynchronisationMobileRequest
{
    public string NomAppareil { get; set; } = string.Empty;
    public string VersionApplication { get; set; } = string.Empty;
    public string DateChargementMobile { get; set; } = string.Empty;
    public string DateEnvoiMobile { get; set; } = string.Empty;
}
