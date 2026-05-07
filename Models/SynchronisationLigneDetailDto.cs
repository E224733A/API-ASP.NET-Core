namespace API_ASP.NET_Core.Models;

/// <summary>
/// Détail d'une ligne de tournée enregistrée lors d'une synchronisation mobile.
/// </summary>
/// <remarks>
/// Ce modèle est utilisé lors de la consultation d'une synchronisation déjà reçue.
/// Il permet de vérifier les informations envoyées par le mobile pour un arrêt de tournée.
/// </remarks>
public sealed class SynchronisationLigneDetailDto
{
    /// <summary>
    /// Identifiant technique de la ligne de tournée enregistrée.
    /// </summary>
    public long IdTourneeLigne { get; set; }

    /// <summary>
    /// Identifiant technique de la tournée mobile associée.
    /// </summary>
    public long IdTourneeMobile { get; set; }

    /// <summary>
    /// Identifiant métier stable de la ligne source.
    /// </summary>
    /// <remarks>
    /// Cet identifiant permet de relier la ligne synchronisée à la ligne chargée le matin.
    ///
    /// Exemple : 2026-04-28|2001|2|1058|1|1
    /// </remarks>
    public string IdLigneSource { get; set; } = string.Empty;

    /// <summary>
    /// Ordre de passage dans la tournée.
    /// </summary>
    public int? OrdreArret { get; set; }

    /// <summary>
    /// Numéro du client.
    /// </summary>
    public string NumClient { get; set; } = string.Empty;

    /// <summary>
    /// Nom métier ou administratif du client.
    /// </summary>
    public string NomClient { get; set; } = string.Empty;

    /// <summary>
    /// Nom affiché côté application mobile, lorsqu'il existe.
    /// </summary>
    public string? NomAffiche { get; set; }

    /// <summary>
    /// Code du point de livraison.
    /// </summary>
    public string? CodePDL { get; set; }

    /// <summary>
    /// Description lisible du point de livraison.
    /// </summary>
    public string? DescriptionPDL { get; set; }

    /// <summary>
    /// Première ligne d'adresse du point de livraison.
    /// </summary>
    public string? AdresseLigne1 { get; set; }

    /// <summary>
    /// Deuxième ligne d'adresse du point de livraison.
    /// </summary>
    public string? AdresseLigne2 { get; set; }

    /// <summary>
    /// Troisième ligne d'adresse du point de livraison.
    /// </summary>
    public string? AdresseLigne3 { get; set; }

    /// <summary>
    /// Ville du point de livraison.
    /// </summary>
    public string? Ville { get; set; }

    /// <summary>
    /// Code postal du point de livraison.
    /// </summary>
    public string? CodePostal { get; set; }

    /// <summary>
    /// Jour de tournée issu des données métier.
    /// </summary>
    public string? JourTournee { get; set; }

    /// <summary>
    /// Schéma de livraison issu des données métier.
    /// </summary>
    public string? SchemaLivraison { get; set; }

    /// <summary>
    /// Code de la tournée principale.
    /// </summary>
    public string CodeTournee { get; set; } = string.Empty;

    /// <summary>
    /// Libellé de la tournée principale.
    /// </summary>
    public string? LibelleTournee { get; set; }

    /// <summary>
    /// Jour de tournée retour issu des données métier.
    /// </summary>
    /// <remarks>
    /// Donnée brute enregistrée dans la synchronisation.
    /// Les règles d'affichage propres au mobile ne sont pas appliquées dans l'API.
    /// </remarks>
    public string? JourTourneeRetour { get; set; }

    /// <summary>
    /// Code de la tournée retour.
    /// </summary>
    public string? CodeTourneeRetour { get; set; }

    /// <summary>
    /// Libellé de la tournée retour.
    /// </summary>
    public string? LibelleTourneeRetour { get; set; }

    /// <summary>
    /// Instructions ou remarques destinées au livreur.
    /// </summary>
    public string? Instructions { get; set; }

    /// <summary>
    /// Zone de déchargement brute issue des données métier.
    /// </summary>
    /// <remarks>
    /// Cette valeur est conservée comme donnée brute.
    /// L'affichage final reste de la responsabilité de l'application mobile.
    /// </remarks>
    public string? ZoneDechargement { get; set; }

    /// <summary>
    /// Zone ou information complémentaire issue des données métier.
    /// </summary>
    public string? Zone { get; set; }

    /// <summary>
    /// Type de linge ou information associée à la ligne.
    /// </summary>
    public string? TypeLinge { get; set; }

    /// <summary>
    /// Indique si le client ou point de livraison était fermé.
    /// </summary>
    public bool EstFerme { get; set; }

    /// <summary>
    /// Date de fermeture du client ou point de livraison, lorsqu'elle existe.
    /// </summary>
    public DateTime? DateFermeture { get; set; }

    /// <summary>
    /// Motif de fermeture, lorsqu'il existe.
    /// </summary>
    public string? MotifFermeture { get; set; }

    /// <summary>
    /// Quantité totale livrée enregistrée pour la ligne.
    /// </summary>
    /// <remarks>
    /// Ce champ correspond au total global de la ligne.
    /// Le détail par article est disponible dans SynchronisationQuantiteDetailDto.
    /// </remarks>
    public int QuantiteLivree { get; set; }

    /// <summary>
    /// Quantité totale reprise ou récupérée enregistrée pour la ligne.
    /// </summary>
    /// <remarks>
    /// Ce champ correspond au total global de la ligne.
    /// Le détail par article est disponible dans SynchronisationQuantiteDetailDto.
    /// </remarks>
    public int QuantiteReprise { get; set; }

    /// <summary>
    /// Précision libre saisie par le livreur.
    /// </summary>
    public string? PrecisionLivreur { get; set; }

    /// <summary>
    /// Statut de passage envoyé par le mobile.
    /// </summary>
    /// <remarks>
    /// Valeurs principales attendues :
    /// - FAIT ;
    /// - NON_FAIT ;
    /// - ANOMALIE.
    /// </remarks>
    public string StatutPassage { get; set; } = string.Empty;

    /// <summary>
    /// Commentaire saisi par le livreur.
    /// </summary>
    /// <remarks>
    /// Obligatoire lors de l'envoi final si le statut est NON_FAIT ou ANOMALIE.
    /// </remarks>
    public string? CommentaireLivreur { get; set; }

    /// <summary>
    /// Date et heure de validation de la ligne par le livreur.
    /// </summary>
    public DateTime? HeureValidation { get; set; }

    /// <summary>
    /// Indique si la ligne a été validée par le livreur.
    /// </summary>
    public bool EstValidee { get; set; }

    /// <summary>
    /// Date de création de la ligne dans la base mobile/API.
    /// </summary>
    public DateTime DateCreation { get; set; }

    /// <summary>
    /// Date de dernière modification de la ligne.
    /// </summary>
    public DateTime? DateModification { get; set; }
}