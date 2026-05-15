namespace API_ASP.NET_Core.Models;

/// <summary>
/// Réponse du GET global Expédition.
/// Cette réponse doit contenir toutes les données nécessaires pour travailler côté application web
/// sans rappeler l'API à chaque ouverture de tournée.
/// </summary>
public sealed class ExpeditionPreparationsApreparerResponse
{
    public string Statut { get; init; } = "SUCCESS";
    public string SchemaVersion { get; init; } = "1.0";
    public string DateTournee { get; init; } = string.Empty;
    public bool DateModifiable { get; init; } = false;
    public DateTimeOffset DateGenerationApi { get; init; }
    public string FuseauHoraireMetier { get; init; } = "Europe/Paris";
    public IList<ExpeditionTourneeApreparerDto> Tournees { get; init; } = new List<ExpeditionTourneeApreparerDto>();
}

public sealed class ExpeditionTourneeApreparerDto
{
    public string CodeTournee { get; init; } = string.Empty;
    public string? LibelleTournee { get; init; }
    public string EtatPreparation { get; init; } = "NON_PREPAREE";
    public bool EstVerrouilleeBd { get; init; }
    public int NombreLignes { get; init; }
    public IList<ExpeditionLigneApreparerDto> Lignes { get; init; } = new List<ExpeditionLigneApreparerDto>();
}

public sealed class ExpeditionLigneApreparerDto
{
    public string IdLigneSource { get; init; } = string.Empty;
    public int? OrdreArret { get; init; }
    public int? Horaire { get; init; }
    public ExpeditionClientDto Client { get; init; } = new();
    public ExpeditionPointLivraisonDto PointLivraison { get; init; } = new();
    public ExpeditionTourneeInfoDto Tournee { get; init; } = new();
    public ExpeditionInfosLivreurDto InfosLivreur { get; init; } = new();
    public IList<ExpeditionArticlePreparableDto> ArticlesPreparables { get; init; } = new List<ExpeditionArticlePreparableDto>();
}

public sealed class ExpeditionClientDto
{
    public string NumClient { get; init; } = string.Empty;
    public string NomClient { get; init; } = string.Empty;
    public string? NomAffiche { get; init; }
}

public sealed class ExpeditionPointLivraisonDto
{
    public string? CodePDL { get; init; }
    public string? DescriptionPDL { get; init; }
    public string? AdresseLigne1 { get; init; }
    public string? AdresseLigne2 { get; init; }
    public string? AdresseLigne3 { get; init; }
    public string? Ville { get; init; }
    public string? CodePostal { get; init; }
}

public sealed class ExpeditionTourneeInfoDto
{
    public string CodeTournee { get; init; } = string.Empty;
    public string? LibelleTournee { get; init; }
    public int? JourTournee { get; init; }
    public string? JourLibelle { get; init; }
    public string? SchemaLivraison { get; init; }
}

public sealed class ExpeditionInfosLivreurDto
{
    public string? Instructions { get; init; }
    public string? ZoneDechargement { get; init; }
    public string? ZoneDechargementAffichee { get; init; }
    public string? Zone { get; init; }
    public string? Precision { get; init; }
    public string? Cle { get; init; }
    public bool EstFerme { get; init; }
    public DateOnly? DateFermeture { get; init; }
    public string? MotifFermeture { get; init; }
}

public sealed class ExpeditionArticlePreparableDto
{
    public string CodeArticle { get; init; } = string.Empty;
    public string LibelleArticle { get; init; } = string.Empty;
    public int? QuantiteLivreePrevue { get; init; }
}

/// <summary>
/// Corps JSON du POST de verrouillage Expédition.
/// L'application web Expédition envoie un lot complet au moment du verrouillage.
/// </summary>
public sealed class ExpeditionVerrouillageRequest
{
    public string SchemaVersion { get; init; } = "1.0";
    public Guid IdLotVerrouillage { get; init; }
    public string DateTournee { get; init; } = string.Empty;
    public DateTimeOffset? DateDeclenchementWeb { get; init; }
    public string? FuseauHoraireWeb { get; init; }
    public IList<ExpeditionTourneeVerrouillageDto> Tournees { get; init; } = new List<ExpeditionTourneeVerrouillageDto>();
}

public sealed class ExpeditionTourneeVerrouillageDto
{
    public string CodeTournee { get; init; } = string.Empty;
    public string? LibelleTournee { get; init; }
    public string StatutPreparation { get; init; } = "PRETE_VERROUILLAGE";
    public IList<ExpeditionLigneVerrouillageDto> Lignes { get; init; } = new List<ExpeditionLigneVerrouillageDto>();
}

public sealed class ExpeditionLigneVerrouillageDto
{
    public string IdLigneSource { get; init; } = string.Empty;
    public int? OrdreArret { get; init; }
    public string NumClient { get; init; } = string.Empty;
    public string? NomClient { get; init; }
    public string? CodePDL { get; init; }
    public string? DescriptionPDL { get; init; }
    public string? CommentaireExceptionnel { get; init; }
    public DateTimeOffset? HeureValidation { get; init; }
    public IList<ExpeditionQuantiteVerrouillageDto> Quantites { get; init; } = new List<ExpeditionQuantiteVerrouillageDto>();
}

public sealed class ExpeditionQuantiteVerrouillageDto
{
    public string CodeArticle { get; init; } = string.Empty;
    public string? LibelleArticle { get; init; }
    public int? QuantiteLivreePrevue { get; init; }
}

public sealed class ExpeditionVerrouillageSuccessResponse
{
    public string Statut { get; init; } = "SUCCESS";
    public string Message { get; init; } = string.Empty;
    public Guid IdLotVerrouillage { get; init; }
    public string DateTournee { get; init; } = string.Empty;
    public int NombreTourneesVerrouillees { get; init; }
    public int NombreLignesRecues { get; init; }
    public int NombreQuantitesRecues { get; init; }
    public DateTimeOffset DateReceptionApi { get; init; }
    public DateTimeOffset DateSauvegardeSql { get; init; }
}

public sealed class ExpeditionErrorResponse
{
    public string Statut { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public IEnumerable<string> Errors { get; init; } = Array.Empty<string>();
}
