namespace API_ASP.NET_Core.Models;

/// <summary>
/// Réponse du GET global Expédition.
/// Contrat JSON v1.2 uniquement.
/// </summary>
/// <remarks>
/// Cette réponse alimente ServeWeb pour préparer la date métier calculée par l'API.
/// Aucun filtre client, livreur ou tournée ne doit être imposé par le client HTTP.
/// </remarks>
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
/// ROLLS = chariots, ROLLS_VIDES = chariots vides.
/// Seuls ROLLS, ROLLS_VIDES, TAPIS et SACS sont autorisés côté Expédition.
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
/// <remarks>
/// StatutPreparationWeb décrit l'état connu côté API au moment du chargement.
/// La décision finale de verrouiller reste portée par ServeWeb après le clic humain.
/// </remarks>
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
/// <remarks>
/// IdLigneSource est l'identifiant stable qui permet de relier le GET Expédition,
/// le verrouillage ServeWeb, le chargement mobile et la synchronisation finale.
/// </remarks>
public sealed class ExpeditionPreparationLigneDto
{
    public string IdLigneSource { get; set; } = string.Empty;
    public int? OrdreArret { get; set; }
    public ExpeditionClientDto Client { get; set; } = new();
    public ExpeditionPointLivraisonDto PointLivraison { get; set; } = new();
    public ExpeditionInfosLectureDto InfosLecture { get; set; } = new();
    public ExpeditionPreparationInitialeDto PreparationInitiale { get; set; } = new();
}

/// <summary>
/// Informations client exposées dans les contrats Expédition.
/// </summary>
public sealed class ExpeditionClientDto
{
    public string NumClient { get; set; } = string.Empty;
    public string? NomClient { get; set; }
    public string? NomAffiche { get; set; }
}

/// <summary>
/// Informations du point de livraison exposées dans les contrats Expédition.
/// </summary>
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

/// <summary>
/// Informations de lecture issues des vues métier, non saisies par l'Expédition.
/// </summary>
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

/// <summary>
/// Quantité prévue affichée ou sauvegardée côté Expédition.
/// </summary>
/// <remarks>
/// Une quantité null signifie que l'Expédition n'a pas renseigné de valeur prévue.
/// Elle ne doit pas être interprétée comme une quantité 0.
/// </remarks>
public sealed class ExpeditionQuantitePrevueDto
{
    public string CodeArticle { get; set; } = string.Empty;
    public string Libelle { get; set; } = string.Empty;
    public int? QuantiteLivreePrevue { get; set; }
}

/// <summary>
/// Règles déclaratives renvoyées à ServeWeb avec le contrat Expédition.
/// </summary>
public sealed class ExpeditionReglesDto
{
    public string HeureVerrouillageMetier { get; set; } = "22:35";
    public string FuseauHoraireMetier { get; set; } = "Europe/Paris";
    public string FenetreModification { get; set; } = "Les préparations sont modifiables avant le verrouillage automatique entre 22:35 et 22:55.";
    public List<string> ArticlesAutorises { get; set; } = new() { "ROLLS", "ROLLS_VIDES", "TAPIS", "SACS" };
    public List<string> ArticlesInterdits { get; set; } = new();
    public bool ExclureRollsVides { get; set; } = false;
    public bool QuantitesNullesAutorisees { get; set; } = true;
}

/// <summary>
/// Requête POST globale de verrouillage Expédition.
/// Contrat JSON v1.2 uniquement.
/// </summary>
/// <remarks>
/// Cette requête est envoyée par ServeWeb lorsque les préparations sont prêtes à être
/// sauvegardées définitivement dans les tables Mobile_*.
/// </remarks>
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

/// <summary>
/// Tournée contenue dans le POST de verrouillage Expédition.
/// </summary>
public sealed class ExpeditionVerrouillageTourneeRequest
{
    public string CodeTournee { get; set; } = string.Empty;
    public string? LibelleTournee { get; set; }
    public string StatutPreparationWeb { get; set; } = string.Empty;

    /// <summary>
    /// Heure du dernier clic humain "Marquer prête pour verrouillage" côté SERVWEB.
    /// Cette valeur alimente Mobile_ExpeditionPreparation.DateModification.
    /// Elle doit être transmise au format ISO 8601 avec offset, puis normalisée en heure Europe/Paris côté API.
    /// </summary>
    public string? DateModification { get; set; }

    public List<ExpeditionVerrouillageLigneRequest> Lignes { get; set; } = new();
}

/// <summary>
/// Ligne contenue dans le POST de verrouillage Expédition.
/// </summary>
/// <remarks>
/// CommentaireExceptionnel suit une règle précise côté repository : null ne modifie pas,
/// chaîne vide désactive, texte non vide remplace le commentaire actif.
/// </remarks>
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

/// <summary>
/// Quantité prévue transmise dans le POST de verrouillage Expédition.
/// </summary>
public sealed class ExpeditionQuantitePrevueRequest
{
    public string CodeArticle { get; set; } = string.Empty;
    public int? QuantiteLivreePrevue { get; set; }
}

/// <summary>
/// Métadonnées optionnelles de dernière modification côté ServeWeb.
/// </summary>
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

/// <summary>
/// État SQL courant d'une préparation Expédition pour une date et une tournée.
/// </summary>
public sealed class ExpeditionPreparationEtatDto
{
    public long IdPreparationExpedition { get; set; }
    public DateTime DateTournee { get; set; }
    public string CodeTournee { get; set; } = string.Empty;
    public string StatutPreparation { get; set; } = string.Empty;
    public bool EstVerrouille { get; set; }
    public Guid? IdLotVerrouillage { get; set; }
}

/// <summary>
/// Résultat de sauvegarde retourné après verrouillage transactionnel d'un lot Expédition.
/// </summary>
public sealed class ExpeditionVerrouillageLotSaveResult
{
    public int NombreTourneesVerrouillees { get; set; }
    public int NombreLignesVerrouillees { get; set; }
    public DateTimeOffset DateReceptionApi { get; set; }
    public DateTimeOffset DateSauvegardeSql { get; set; }
}
