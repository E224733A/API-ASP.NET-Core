namespace API_ASP.NET_Core.Models;

public class SynchronisationResumeDto
{
    public long IdTourneeMobile { get; set; }
    public Guid IdSynchronisation { get; set; }
    public DateTime DateTournee { get; set; }
    public string? CodeTournee { get; set; }
    public string? LibelleTournee { get; set; }

    public int IdLivreur { get; set; }
    public string? CodeLivreur { get; set; }
    public string? NomLivreur { get; set; }

    public string? StatutSynchronisation { get; set; }
    public DateTime? DateChargementMobile { get; set; }
    public DateTime DateReceptionApi { get; set; }
    public DateTime? DateEnvoi { get; set; }

    public bool EstVerrouillee { get; set; }
    public int NombrePointsPrevus { get; set; }
    public int NombrePointsSaisis { get; set; }

    public string? CommentaireGlobal { get; set; }
    public string? NomAppareil { get; set; }
    public string? VersionApplication { get; set; }

    public int NombreFaits { get; set; }
    public int NombreNonFaits { get; set; }
    public int NombreAnomalies { get; set; }
}

public class SynchronisationDetailDto : SynchronisationResumeDto
{
    public List<SynchronisationLigneDto> Lignes { get; set; } = new();
    public List<SynchronisationLogDto> Logs { get; set; } = new();
}

public class SynchronisationLigneDto
{
    public long IdTourneeLigne { get; set; }
    public long IdTourneeMobile { get; set; }

    public int? OrdreArret { get; set; }

    public string? NumClient { get; set; }
    public string? NomClient { get; set; }

    public string? CodePDL { get; set; }
    public string? DescriptionPDL { get; set; }

    public string? AdresseLigne1 { get; set; }
    public string? AdresseLigne2 { get; set; }
    public string? AdresseLigne3 { get; set; }
    public string? Ville { get; set; }
    public string? CodePostal { get; set; }

    public int? JourTournee { get; set; }
    public string? SchemaLivraison { get; set; }
    public string? CodeTournee { get; set; }
    public string? LibelleTournee { get; set; }

    public int? JourTourneeRetour { get; set; }
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

    public int NbExpes { get; set; }
    public int NbRolls { get; set; }
    public int NbVetements { get; set; }
    public int NbTapis { get; set; }
    public int NbSacs { get; set; }
    public int NbRecuperes { get; set; }

    public string? PrecisionLivreur { get; set; }
    public string? StatutPassage { get; set; }
    public string? CommentaireLivreur { get; set; }
    public DateTime? HeureValidation { get; set; }
    public bool EstValidee { get; set; }

    public DateTime DateCreation { get; set; }
    public DateTime? DateModification { get; set; }
}

public class SynchronisationLogDto
{
    public long IdLog { get; set; }
    public long? IdTourneeMobile { get; set; }
    public int? IdLivreur { get; set; }
    public Guid? IdSynchronisation { get; set; }

    public DateTime DateEvenement { get; set; }
    public string? TypeEvenement { get; set; }
    public string? Niveau { get; set; }
    public string? Message { get; set; }
    public string? DetailTechnique { get; set; }

    public string? AdresseIP { get; set; }
    public string? NomAppareil { get; set; }
    public string? VersionApplication { get; set; }
}