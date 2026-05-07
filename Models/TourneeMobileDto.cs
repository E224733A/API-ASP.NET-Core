namespace API_ASP.NET_Core.Models;

/// <summary>
/// Détail complet d'une tournée chargée par l'application mobile.
/// </summary>
/// <remarks>
/// Ce modèle est renvoyé par GET /api/tournees/jour.
/// Il représente le contrat de chargement du matin : en-tête de tournée, livreur,
/// informations de chargement, articles saisissables et lignes clients.
/// </remarks>
public class TourneeMobileDto
{
    /// <summary>
    /// Version du schéma JSON de chargement.
    /// </summary>
    /// <remarks>
    /// Exemple : 1.1
    /// </remarks>
    public string SchemaVersion { get; init; } = "1.1";

    /// <summary>
    /// Date de la tournée chargée.
    /// </summary>
    /// <remarks>
    /// Format : yyyy-MM-dd.
    ///
    /// Exemple : 2026-04-28
    /// </remarks>
    public string DateTournee { get; init; } = default!;

    /// <summary>
    /// Indique si la date peut être modifiée côté application mobile.
    /// </summary>
    /// <remarks>
    /// Pour le fonctionnement prévu, la date est affichée en lecture seule.
    /// </remarks>
    public bool DateModifiable { get; init; } = false;

    /// <summary>
    /// Numéro du jour de tournée.
    /// </summary>
    /// <remarks>
    /// Exemple : 2 pour mardi.
    /// </remarks>
    public int? JourTournee { get; init; }

    /// <summary>
    /// Libellé du jour de tournée.
    /// </summary>
    /// <remarks>
    /// Exemple : Mardi
    /// </remarks>
    public string? JourLibelle { get; init; }

    /// <summary>
    /// Code de la tournée chargée.
    /// </summary>
    /// <remarks>
    /// Exemple : 2001
    /// </remarks>
    public string CodeTournee { get; init; } = default!;

    /// <summary>
    /// Libellé lisible de la tournée.
    /// </summary>
    /// <remarks>
    /// Exemple : MDR VENDEE
    /// </remarks>
    public string? LibelleTournee { get; init; }

    /// <summary>
    /// Statut de synchronisation initial de la tournée côté mobile.
    /// </summary>
    /// <remarks>
    /// Au chargement du matin, la valeur par défaut est NON_ENVOYEE.
    /// </remarks>
    public string StatutSynchronisation { get; init; } = "NON_ENVOYEE";

    /// <summary>
    /// Informations du livreur associé à la tournée.
    /// </summary>
    public LivreurDto Livreur { get; init; } = default!;

    /// <summary>
    /// Informations techniques sur le chargement de la tournée.
    /// </summary>
    public ChargementDto? Chargement { get; init; }

    /// <summary>
    /// Liste des articles que le livreur peut saisir dans l'application mobile.
    /// </summary>
    /// <remarks>
    /// Cette liste permet au mobile de construire les champs de quantités sans coder
    /// directement les articles dans l'application.
    /// </remarks>
    public IList<ArticleSaisissableDto> ArticlesSaisissables { get; init; }
        = new List<ArticleSaisissableDto>();

    /// <summary>
    /// Liste des lignes clients ou points de livraison de la tournée.
    /// </summary>
    public IList<TourneeLigneMobileDto> Lignes { get; init; }
        = new List<TourneeLigneMobileDto>();
}

/// <summary>
/// Informations de génération du chargement envoyé au mobile.
/// </summary>
public class ChargementDto
{
    /// <summary>
    /// Date et heure de génération du JSON par l'API.
    /// </summary>
    public DateTimeOffset DateGenerationApi { get; init; }

    /// <summary>
    /// Nombre de points de livraison envoyés au mobile.
    /// </summary>
    public int NombrePointsEnvoyes { get; init; }
}

/// <summary>
/// Article pouvant être saisi par le livreur dans l'application mobile.
/// </summary>
public class ArticleSaisissableDto
{
    /// <summary>
    /// Code technique de l'article.
    /// </summary>
    /// <remarks>
    /// Exemples : ROLLS, TAPIS, SACS, VETEMENTS, EXPES.
    /// </remarks>
    public string CodeArticle { get; init; } = default!;

    /// <summary>
    /// Libellé lisible de l'article.
    /// </summary>
    /// <remarks>
    /// Exemples : Rolls, Tapis, Sacs.
    /// </remarks>
    public string Libelle { get; init; } = default!;
}

/// <summary>
/// Ligne de tournée envoyée au mobile.
/// </summary>
/// <remarks>
/// Une ligne correspond à un arrêt de tournée, généralement lié à un client
/// et à un point de livraison.
/// </remarks>
public class TourneeLigneMobileDto
{
    /// <summary>
    /// Identifiant métier stable de la ligne source.
    /// </summary>
    /// <remarks>
    /// Cet identifiant est renvoyé par le mobile lors de la synchronisation du soir.
    ///
    /// Exemple : 2026-04-28|2001|2|1058|1|1
    /// </remarks>
    public string IdLigneSource { get; init; } = default!;

    /// <summary>
    /// Ordre de passage dans la tournée.
    /// </summary>
    public int? OrdreArret { get; init; }

    /// <summary>
    /// Horaire ou ordre horaire issu des données métier, lorsqu'il existe.
    /// </summary>
    public int? Horaire { get; init; }

    /// <summary>
    /// Informations du client concerné par cette ligne.
    /// </summary>
    public ClientDto Client { get; init; } = default!;

    /// <summary>
    /// Informations du point de livraison concerné.
    /// </summary>
    public PointLivraisonDto PointLivraison { get; init; } = default!;

    /// <summary>
    /// Informations liées à la tournée principale.
    /// </summary>
    public TourneeInfoDto Tournee { get; init; } = default!;

    /// <summary>
    /// Informations de tournée retour lorsqu'elles existent.
    /// </summary>
    public RetourInfoDto Retour { get; init; } = new();

    /// <summary>
    /// Informations complémentaires utiles au livreur.
    /// </summary>
    public InfosLivreurDto InfosLivreur { get; init; } = new();

    /// <summary>
    /// Valeurs initiales de saisie côté mobile.
    /// </summary>
    /// <remarks>
    /// Au chargement du matin, une ligne est généralement initialisée avec le statut A_FAIRE
    /// et estValidee à false.
    /// </remarks>
    public SaisieMobileDto Saisie { get; init; } = new();
}

/// <summary>
/// Informations du client affichées dans la tournée mobile.
/// </summary>
public class ClientDto
{
    /// <summary>
    /// Numéro du client.
    /// </summary>
    public string NumClient { get; init; } = default!;

    /// <summary>
    /// Nom métier ou administratif du client.
    /// </summary>
    public string NomClient { get; init; } = default!;

    /// <summary>
    /// Nom affiché dans l'application mobile, lorsqu'il est disponible.
    /// </summary>
    public string? NomAffiche { get; init; }
}

/// <summary>
/// Informations du point de livraison affiché dans l'application mobile.
/// </summary>
public class PointLivraisonDto
{
    /// <summary>
    /// Code du point de livraison.
    /// </summary>
    public string? CodePDL { get; init; }

    /// <summary>
    /// Description lisible du point de livraison.
    /// </summary>
    public string? DescriptionPDL { get; init; }

    /// <summary>
    /// Première ligne d'adresse.
    /// </summary>
    public string? AdresseLigne1 { get; init; }

    /// <summary>
    /// Deuxième ligne d'adresse.
    /// </summary>
    public string? AdresseLigne2 { get; init; }

    /// <summary>
    /// Troisième ligne d'adresse.
    /// </summary>
    public string? AdresseLigne3 { get; init; }

    /// <summary>
    /// Ville du point de livraison.
    /// </summary>
    public string? Ville { get; init; }

    /// <summary>
    /// Code postal du point de livraison.
    /// </summary>
    public string? CodePostal { get; init; }
}

/// <summary>
/// Informations de la tournée principale pour une ligne.
/// </summary>
public class TourneeInfoDto
{
    /// <summary>
    /// Code de la tournée.
    /// </summary>
    public string CodeTournee { get; init; } = default!;

    /// <summary>
    /// Libellé de la tournée.
    /// </summary>
    public string? LibelleTournee { get; init; }

    /// <summary>
    /// Numéro du jour de tournée.
    /// </summary>
    public int? JourTournee { get; init; }

    /// <summary>
    /// Schéma de livraison issu des données métier, lorsqu'il existe.
    /// </summary>
    public string? SchemaLivraison { get; init; }
}

/// <summary>
/// Informations de tournée retour associées à une ligne.
/// </summary>
public class RetourInfoDto
{
    /// <summary>
    /// Numéro de jour de tournée retour.
    /// </summary>
    /// <remarks>
    /// Donnée brute fournie par l'API.
    /// Les règles d'affichage propres à l'application mobile sont appliquées côté mobile.
    /// </remarks>
    public int? JourTourneeRetour { get; init; }

    /// <summary>
    /// Libellé du jour de retour.
    /// </summary>
    public string? JourRetourLibelle { get; init; }

    /// <summary>
    /// Code de la tournée retour.
    /// </summary>
    public string? CodeTourneeRetour { get; init; }

    /// <summary>
    /// Libellé de la tournée retour.
    /// </summary>
    public string? LibelleTourneeRetour { get; init; }
}

/// <summary>
/// Informations complémentaires destinées au livreur.
/// </summary>
public class InfosLivreurDto
{
    /// <summary>
    /// Instructions spécifiques à afficher au livreur.
    /// </summary>
    public string? Instructions { get; init; }

    /// <summary>
    /// Commentaire issu de la fiche de tournée.
    /// </summary>
    public string? CommentaireFiche { get; init; }

    /// <summary>
    /// Zone de déchargement brute issue des données métier.
    /// </summary>
    /// <remarks>
    /// L'API fournit cette valeur brute.
    /// Toute règle d'affichage spécifique à l'écran mobile reste gérée côté application mobile.
    /// </remarks>
    public string? ZoneDechargement { get; init; }

    /// <summary>
    /// Zone ou information complémentaire issue des données métier.
    /// </summary>
    public string? Zone { get; init; }

    /// <summary>
    /// Précision utile au livreur.
    /// </summary>
    public string? Precision { get; init; }

    /// <summary>
    /// Information de clé ou d'accès lorsqu'elle existe.
    /// </summary>
    public string? Cle { get; init; }

    /// <summary>
    /// Indique si le client ou point de livraison est fermé.
    /// </summary>
    public bool EstFerme { get; init; }

    /// <summary>
    /// Date de fermeture lorsqu'elle existe.
    /// </summary>
    public DateOnly? DateFermeture { get; init; }

    /// <summary>
    /// Motif de fermeture lorsqu'il existe.
    /// </summary>
    public string? MotifFermeture { get; init; }
}

/// <summary>
/// Données de saisie initiales envoyées au mobile pour une ligne de tournée.
/// </summary>
public class SaisieMobileDto
{
    /// <summary>
    /// Précision saisie par le livreur.
    /// </summary>
    /// <remarks>
    /// Au chargement du matin, ce champ est généralement vide.
    /// </remarks>
    public string? PrecisionLivreur { get; init; }

    /// <summary>
    /// Statut de passage de la ligne.
    /// </summary>
    /// <remarks>
    /// Au chargement du matin, la valeur par défaut est A_FAIRE.
    /// </remarks>
    public string StatutPassage { get; init; } = "A_FAIRE";

    /// <summary>
    /// Commentaire saisi par le livreur.
    /// </summary>
    /// <remarks>
    /// Au chargement du matin, ce champ est généralement vide.
    /// </remarks>
    public string? CommentaireLivreur { get; init; }

    /// <summary>
    /// Heure de validation de la ligne.
    /// </summary>
    /// <remarks>
    /// Au chargement du matin, ce champ est généralement vide.
    /// </remarks>
    public DateTimeOffset? HeureValidation { get; init; }

    /// <summary>
    /// Indique si la ligne a été validée.
    /// </summary>
    /// <remarks>
    /// Au chargement du matin, la valeur par défaut est false.
    /// </remarks>
    public bool EstValidee { get; init; } = false;

    /// <summary>
    /// Quantités initiales par article.
    /// </summary>
    public IList<QuantiteSaisieMobileDto> Quantites { get; init; }
        = new List<QuantiteSaisieMobileDto>();
}

/// <summary>
/// Quantité initiale envoyée au mobile pour un article.
/// </summary>
public class QuantiteSaisieMobileDto
{
    /// <summary>
    /// Code technique de l'article.
    /// </summary>
    public string CodeArticle { get; init; } = default!;

    /// <summary>
    /// Libellé lisible de l'article.
    /// </summary>
    public string Libelle { get; init; } = default!;

    /// <summary>
    /// Quantité livrée prévue ou saisie pour cet article.
    /// </summary>
    public int QuantiteLivree { get; init; }

    /// <summary>
    /// Quantité récupérée prévue ou saisie pour cet article.
    /// </summary>
    public int QuantiteRecuperee { get; init; }
}