namespace API_ASP.NET_Core.Models;

/// <summary>
/// Modèle interne issu du DTO public ExpeditionVerrouillageLotRequest.
/// Ce type n'est pas exposé en JSON et sert uniquement à séparer le contrat public
/// de l'orchestration métier et de la persistance SQL du module Expédition.
/// </summary>
public sealed class ExpeditionVerrouillageLotCommand
{
    public string SchemaVersion { get; init; } = string.Empty;
    public string IdLotVerrouillage { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty;
    public DateOnly DateTournee { get; init; }
    public string DateTourneeTexte { get; init; } = string.Empty;
    public string DateVerrouillageDemandee { get; init; } = string.Empty;
    public string FuseauHoraireMetier { get; init; } = string.Empty;
    public List<ExpeditionVerrouillageTourneeCommand> Tournees { get; init; } = new();
}

public sealed class ExpeditionVerrouillageTourneeCommand
{
    public string CodeTournee { get; init; } = string.Empty;
    public string? LibelleTournee { get; init; }
    public string StatutPreparationWeb { get; init; } = string.Empty;
    public string? DateModification { get; init; }
    public List<ExpeditionVerrouillageLigneCommand> Lignes { get; init; } = new();
}

public sealed class ExpeditionVerrouillageLigneCommand
{
    public string IdLigneSource { get; init; } = string.Empty;
    public int? OrdreArret { get; init; }
    public ExpeditionVerrouillageClientCommand Client { get; init; } = new();
    public ExpeditionVerrouillagePointLivraisonCommand PointLivraison { get; init; } = new();
    public string? CommentaireExceptionnel { get; init; }
    public ExpeditionVerrouillageDerniereModificationCommand? DerniereModification { get; init; }
    public List<ExpeditionVerrouillageQuantiteCommand> QuantitesPrevues { get; init; } = new();
}

public sealed class ExpeditionVerrouillageClientCommand
{
    public string NumClient { get; init; } = string.Empty;
    public string? NomClient { get; init; }
    public string? NomAffiche { get; init; }
}

public sealed class ExpeditionVerrouillagePointLivraisonCommand
{
    public string? CodePDL { get; init; }
    public string? DescriptionPDL { get; init; }
    public string? AdresseLigne1 { get; init; }
    public string? AdresseLigne2 { get; init; }
    public string? AdresseLigne3 { get; init; }
    public string? Ville { get; init; }
    public string? CodePostal { get; init; }
}

public sealed class ExpeditionVerrouillageDerniereModificationCommand
{
    public string? Date { get; init; }
    public string? Utilisateur { get; init; }
}

public sealed class ExpeditionVerrouillageQuantiteCommand
{
    public string CodeArticle { get; init; } = string.Empty;
    public int? QuantiteLivreePrevue { get; init; }
}
