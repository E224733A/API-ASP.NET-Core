namespace API_ASP.NET_Core.Models;

public sealed class SynchronisationLigneDetailDto
{
    public long IdTourneeLigne { get; set; }

    public long IdTourneeMobile { get; set; }

    public string IdLigneSource { get; set; } = string.Empty;

    public int? OrdreArret { get; set; }

    public string NumClient { get; set; } = string.Empty;

    public string NomClient { get; set; } = string.Empty;

    public string? NomAffiche { get; set; }

    public string? CodePDL { get; set; }

    public string? DescriptionPDL { get; set; }

    public string? AdresseLigne1 { get; set; }

    public string? AdresseLigne2 { get; set; }

    public string? AdresseLigne3 { get; set; }

    public string? Ville { get; set; }

    public string? CodePostal { get; set; }

    public string? JourTournee { get; set; }

    public string? SchemaLivraison { get; set; }

    public string CodeTournee { get; set; } = string.Empty;

    public string? LibelleTournee { get; set; }

    public string? JourTourneeRetour { get; set; }

    public string? CodeTourneeRetour { get; set; }

    public string? LibelleTourneeRetour { get; set; }

    public string? Instructions { get; set; }

    public string? ZoneDechargement { get; set; }

    public string? Zone { get; set; }

    public string? TypeLinge { get; set; }

    public bool EstFerme { get; set; }

    public DateTime? DateFermeture { get; set; }

    public string? MotifFermeture { get; set; }

    public int QuantiteLivree { get; set; }

    public int QuantiteReprise { get; set; }

    public string? PrecisionLivreur { get; set; }

    public string StatutPassage { get; set; } = string.Empty;

    public string? CommentaireLivreur { get; set; }

    public DateTime? HeureValidation { get; set; }

    public bool EstValidee { get; set; }

    public DateTime DateCreation { get; set; }

    public DateTime? DateModification { get; set; }
}