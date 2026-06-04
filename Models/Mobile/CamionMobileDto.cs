namespace API_ASP.NET_Core.Models;

/// <summary>
/// Camion disponible pour l'application mobile.
/// </summary>
/// <remarks>
/// Ce DTO représente le contrat JSON renvoyé par GET /api/camions/disponibles.
/// Les chaînes sont normalisées par le service avant exposition au mobile.
/// </remarks>
public sealed class CamionMobileDto
{
    /// <summary>
    /// Identifiant source du camion.
    /// </summary>
    /// <remarks>
    /// Ce champ est obligatoire côté mobile et doit provenir de la source SQL camion.
    /// </remarks>
    public string IdCamion { get; init; } = default!;

    /// <summary>
    /// Code métier du camion, lorsqu'il est disponible.
    /// </summary>
    public string? CodeCamion { get; init; }

    /// <summary>
    /// Libellé lisible du camion, lorsqu'il est disponible.
    /// </summary>
    public string? LibelleCamion { get; init; }

    /// <summary>
    /// Immatriculation du camion, lorsqu'elle est disponible.
    /// </summary>
    public string? Immatriculation { get; init; }

    /// <summary>
    /// Indique si le camion est actif dans la source métier.
    /// </summary>
    /// <remarks>
    /// Lorsque la source SQL ne fournit pas cette information, la valeur par défaut est true.
    /// </remarks>
    public bool EstActif { get; init; } = true;
}
