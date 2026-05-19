namespace API_ASP.NET_Core.Models;

/// <summary>
/// Réponse du GET global Expédition.
/// Contrat JSON v1.2 uniquement.
/// </summary>
public sealed class ExpeditionPreparationResponseDto
{
    public string Statut { get; set; } = "SUCCESS";
    public string SchemaVersion { get; set; } = "1.2";
    public string DateTournee { get; set; } = string.Empty;
    public string DatePreparable { get; set; } = string.Empty;
    public bool DateModifiable { get; set; } = false;
    public string FuseauHoraireMetier { get; set; } = "Europe/Paris";
    public string DateGenerationApi { get; set; } = string.Empty;
    public string? Message { get; set; }
    public List<ExpeditionArticlePreparableDto> ArticlesPreparables { get; set; } = new();
    public List<ExpeditionPreparationTourneeDto> Tournees { get; set; } = new();
    public ExpeditionReglesDto Regles { get; set; } = new();
}

/// <summary>
/// Article préparé côté Expédition.
/// Seuls ROLLS, TAPIS et SACS sont autorisés.
/// ROLLS_VIDES est volontairement exclu.
/// </summary>
public sealed class ExpeditionArticlePreparableDto
{
    public string CodeArticle { get; set; } = string.Empty;
    public string Libelle { get; set; } = string.Empty;
    public string TypeQuantite { get; set; } = "LIVREE_PREVUE";
    public bool QuantiteNullable { get; set; } = true;
    public int OrdreAffichage { get; set; }
}

/// <summary>
/// Tournée contenue dans le chargement global Expédition.
/// </summary>
public sealed class ExpeditionPreparationTourneeDto
{
    public string CodeTournee { get; set; } = string.Empty;
    public string? LibelleTournee { get; set; }
    public string StatutPreparationWeb { get; set; } = "PRETE_VERROUILLAGE";
    public List<ExpeditionPreparationLigneDto> Lignes { get; set; } = new();
}

/// <summary>
/// Ligne de tournée préparée côté Expédition.
/// </summary>
public sealed class ExpeditionPreparationLigneDto
{
    public string IdLigneSource { get; set; } = string.Empty;
    public int? OrdreArret { get; set; }
    public ExpeditionClientDto Client { get; set; } = new();
    public ExpeditionPointLivraisonDto PointLivraison { get; set; } = new();
    public ExpeditionInfosLectureDto InfosLecture { get; set; } = new();
    public ExpeditionPreparationInitialeDto PreparationInitiale { get; set; } = new();
}

public sealed class ExpeditionClientDto
{
    public string NumClient { get; set; } = string.Empty;
    public string? NomClient { get; set; }
    public string? NomAffiche { get; set; }
}

public sealed class ExpeditionPointLivraisonDto
{
    public string? CodePDL { get; set; }
    public string? DescriptionPDL { get; set; }
    public string? AdresseLigne1 { get; set; }
    public string? AdresseLigne2 { get; set; }
    public string? AdresseLigne3 { get; set; }
    public string? Ville { get; set; }
    public string? CodePostal { get; set; }
}

public sealed class ExpeditionInfosLectureDto
{
    public string? Horaire { get; set; }
    public string? CodeTournee { get; set; }
    public string? LibelleTournee { get; set; }
    public string? Instructions { get; set; }
    public bool EstFerme { get; set; }
    public string? DateFermeture { get; set; }
    public string? MotifFermeture { get; set; }
    public string? ZoneDechargement { get; set; }
}

/// <summary>
/// Valeurs initiales proposées au module web Expédition.
/// </summary>
public sealed class ExpeditionPreparationInitialeDto
{
    public string? CommentaireExceptionnel { get; set; }
    public List<ExpeditionQuantitePrevueDto> QuantitesPrevues { get; set; } = new();
}

public sealed class ExpeditionQuantitePrevueDto
{
    public string CodeArticle { get; set; } = string.Empty;
    public string Libelle { get; set; } = string.Empty;
    public int? QuantiteLivreePrevue { get; set; }
}

public sealed class ExpeditionReglesDto
{
    public string HeureVerrouillageMetier { get; set; } = "00:05";
    public string FuseauHoraireMetier { get; set; } = "Europe/Paris";
    public string FenetreModification { get; set; } = "Les préparations sont modifiables avant le verrouillage automatique autour de 00:05.";
    public List<string> ArticlesAutorises { get; set; } = new() { "ROLLS", "TAPIS", "SACS" };
    public List<string> ArticlesInterdits { get; set; } = new() { "ROLLS_VIDES" };
    public bool ExclureRollsVides { get; set; } = true;
    public bool QuantitesNullesAutorisees { get; set; } = true;
}

/// <summary>
/// Requête POST globale de verrouillage Expédition.
/// Contrat JSON v1.2 uniquement.
/// </summary>
public sealed class ExpeditionVerrouillageLotRequest
{
    public string SchemaVersion { get; set; } = "1.2";
    public string IdLotVerrouillage { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string DateTournee { get; set; } = string.Empty;
    public string DateVerrouillageDemandee { get; set; } = string.Empty;
    public string FuseauHoraireMetier { get; set; } = string.Empty;
    public List<ExpeditionVerrouillageTourneeRequest> Tournees { get; set; } = new();
}

public sealed class ExpeditionVerrouillageTourneeRequest
{
    public string CodeTournee { get; set; } = string.Empty;
    public string? LibelleTournee { get; set; }
    public string StatutPreparationWeb { get; set; } = string.Empty;
    public List<ExpeditionVerrouillageLigneRequest> Lignes { get; set; } = new();
}

public sealed class ExpeditionVerrouillageLigneRequest
{
    public string IdLigneSource { get; set; } = string.Empty;
    public int? OrdreArret { get; set; }
    public ExpeditionClientDto Client { get; set; } = new();
    public ExpeditionPointLivraisonDto PointLivraison { get; set; } = new();
    public string? CommentaireExceptionnel { get; set; }
    public ExpeditionDerniereModificationDto? DerniereModification { get; set; }
    public List<ExpeditionQuantitePrevueRequest> QuantitesPrevues { get; set; } = new();
}

public sealed class ExpeditionQuantitePrevueRequest
{
    public string CodeArticle { get; set; } = string.Empty;
    public int? QuantiteLivreePrevue { get; set; }
}

public sealed class ExpeditionDerniereModificationDto
{
    public string? Date { get; set; }
    public string? Utilisateur { get; set; }
}

/// <summary>
/// Résultat standard des routes Expédition.
/// </summary>
public sealed class ExpeditionApiResult
{
    public string Statut { get; set; } = string.Empty;
    public string? Code { get; set; }
    public string? Message { get; set; }
    public List<string>? Errors { get; set; }
    public string? IdLotVerrouillage { get; set; }
    public string? DateTournee { get; set; }
    public string? StatutVerrouillage { get; set; }
    public string? DateReceptionApi { get; set; }
    public string? DateSauvegardeSql { get; set; }
    public int? NombreTourneesVerrouillees { get; set; }
    public int? NombreLignesVerrouillees { get; set; }
}

/// <summary>
/// Représentation technique du lot stocké en base.
/// La base cible stocke IdLotVerrouillage en GUID.
/// L'API v1.2 accepte un identifiant métier en string et le convertit en GUID stable côté serveur.
/// </summary>
public sealed class ExpeditionLotVerrouillageDto
{
    public Guid IdLotVerrouillage { get; set; }
    public string EmpreintePayload { get; set; } = string.Empty;
    public DateTime DateTournee { get; set; }
    public string CodeTournee { get; set; } = string.Empty;
    public long? IdPreparationExpedition { get; set; }
}

public sealed class ExpeditionPreparationEtatDto
{
    public long IdPreparationExpedition { get; set; }
    public DateTime DateTournee { get; set; }
    public string CodeTournee { get; set; } = string.Empty;
    public string StatutPreparation { get; set; } = string.Empty;
    public bool EstVerrouille { get; set; }
    public Guid? IdLotVerrouillage { get; set; }
}

public sealed class ExpeditionVerrouillageLotSaveResult
{
    public int NombreTourneesVerrouillees { get; set; }
    public int NombreLignesVerrouillees { get; set; }
    public DateTimeOffset DateReceptionApi { get; set; }
    public DateTimeOffset DateSauvegardeSql { get; set; }
}
