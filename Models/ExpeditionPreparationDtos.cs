using System.Text.Json.Serialization;

namespace API_ASP.NET_Core.Models;

public sealed class ExpeditionPreparationResponseDto
{
    public string SchemaVersion { get; set; } = "1.0";
    public string DateTournee { get; set; } = string.Empty;
    public string? CodeTournee { get; set; }
    public int NombreLignes { get; set; }
    public List<ExpeditionPreparationLigneDto> Lignes { get; set; } = new();
}

public sealed class ExpeditionPreparationLigneDto
{
    public string IdLigneSource { get; set; } = string.Empty;
    public int? OrdreArret { get; set; }
    public string? Horaire { get; set; }
    public string NumClient { get; set; } = string.Empty;
    public string NomClient { get; set; } = string.Empty;
    public string? NomAffiche { get; set; }
    public string? CodePDL { get; set; }
    public string? DescriptionPDL { get; set; }
    public string CodeTournee { get; set; } = string.Empty;
    public string? LibelleTournee { get; set; }
    public string? CommentaireExceptionnel { get; set; }
    public List<ExpeditionPreparationQuantiteDto> Quantites { get; set; } = new();
}

public sealed class ExpeditionPreparationQuantiteDto
{
    public string CodeArticle { get; set; } = string.Empty;
    public string Libelle { get; set; } = string.Empty;
    public int? QuantiteLivreePrevue { get; set; }
}

public sealed class ExpeditionVerrouillageRequest
{
    public string SchemaVersion { get; set; } = "1.0";
    public string IdLotVerrouillage { get; set; } = string.Empty;
    public string DateTournee { get; set; } = string.Empty;
    public string CodeTournee { get; set; } = string.Empty;
    public string? LibelleTournee { get; set; }
    public ExpeditionUtilisateurRequest? Utilisateur { get; set; }
    public List<ExpeditionVerrouillageLigneRequest> Lignes { get; set; } = new();
}

public sealed class ExpeditionUtilisateurRequest
{
    public string? Identifiant { get; set; }
    public string? NomAffiche { get; set; }
}

public sealed class ExpeditionVerrouillageLigneRequest
{
    public string IdLigneSource { get; set; } = string.Empty;
    public int? OrdreArret { get; set; }
    public string? Horaire { get; set; }
    public string NumClient { get; set; } = string.Empty;
    public string? NomClient { get; set; }
    public string? NomAffiche { get; set; }
    public string? CodePDL { get; set; }
    public string? DescriptionPDL { get; set; }
    public string? CommentaireExceptionnel { get; set; }
    public List<ExpeditionVerrouillageQuantiteRequest> Quantites { get; set; } = new();
}

public sealed class ExpeditionVerrouillageQuantiteRequest
{
    public string CodeArticle { get; set; } = string.Empty;
    public string? Libelle { get; set; }
    public int? QuantiteLivreePrevue { get; set; }
}

public sealed class ExpeditionApiResult
{
    public string Statut { get; set; } = string.Empty;
    public string? Code { get; set; }
    public string? Message { get; set; }
    public List<string>? Errors { get; set; }
    public long? IdPreRemplissageTournee { get; set; }
    public string? IdLotVerrouillage { get; set; }
}

public sealed class ExpeditionLotVerrouillageDto
{
    public Guid IdLotVerrouillage { get; set; }
    public string EmpreintePayload { get; set; } = string.Empty;
    public DateTime DateTournee { get; set; }
    public string CodeTournee { get; set; } = string.Empty;
    public long? IdPreRemplissageTournee { get; set; }
}

public sealed class ExpeditionPreparationEtatDto
{
    public long IdPreRemplissageTournee { get; set; }
    public DateTime DateTournee { get; set; }
    public string CodeTournee { get; set; } = string.Empty;
    public bool EstVerrouille { get; set; }
    public Guid? IdLotVerrouillage { get; set; }
}
