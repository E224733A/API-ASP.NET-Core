namespace API_ASP.NET_Core.Models;

public class TourneeMobileDto
{
    // Toujours "1.0" pour pouvoir gérer des versions plus tard.
    public string SchemaVersion { get; init; } = "1.0";
    public string DateTournee { get; init; } = default!;
    public int? JourTournee { get; init; }
    public string? JourLibelle { get; init; }
    public string CodeTournee { get; init; } = default!;
    public string? LibelleTournee { get; init; }
    public string StatutSynchronisation { get; init; } = "NON_ENVOYEE";
    public LivreurDto Livreur { get; init; } = default!;
    public ChargementDto? Chargement { get; init; }
    public IList<TourneeLigneMobileDto> Lignes { get; init; } = new List<TourneeLigneMobileDto>();
}

public class ChargementDto
{
    public DateTime? DateChargement { get; init; }
    public int? NombrePointsEnvoyes { get; init; }
}

public class TourneeLigneMobileDto
{
    public string IdLigneSource { get; init; } = default!;
    public int? OrdreArret { get; init; }
    public int? Horaire { get; init; }
    public ClientDto Client { get; init; } = default!;
    public PointLivraisonDto PointLivraison { get; init; } = default!;
    public TourneeInfoDto Tournee { get; init; } = default!;
    public RetourInfoDto Retour { get; init; } = new();
    public InfosLivreurDto InfosLivreur { get; init; } = new();
    public SaisieDto Saisie { get; init; } = new();
}

public class ClientDto
{
    public string NumClient { get; init; } = default!;
    public string NomClient { get; init; } = default!;
    public string? NomAffiche { get; init; }
}

public class PointLivraisonDto
{
    public string? CodePDL { get; init; }
    public string? DescriptionPDL { get; init; }
    public string? AdresseLigne1 { get; init; }
    public string? AdresseLigne2 { get; init; }
    public string? AdresseLigne3 { get; init; }
    public string? Ville { get; init; }
    public string? CodePostal { get; init; }
}

public class TourneeInfoDto
{
    public string CodeTournee { get; init; } = default!;
    public string? LibelleTournee { get; init; }
    public int? JourTournee { get; init; }
    public string? SchemaLivraison { get; init; }
}

public class RetourInfoDto
{
    public int? JourTourneeRetour { get; init; }
    public string? JourRetourLibelle { get; init; }
    public string? CodeTourneeRetour { get; init; }
    public string? LibelleTourneeRetour { get; init; }
}

public class InfosLivreurDto
{
    public string? Instructions { get; init; }
    public string? CommentaireFiche { get; init; }
    public string? ZoneDechargement { get; init; }
    public string? Zone { get; init; }
    public string? Precision { get; init; }
    public string? Cle { get; init; }
    public bool EstFerme { get; init; }
    public DateOnly? DateFermeture { get; init; }
    public string? MotifFermeture { get; init; }
}

public class SaisieDto
{
    public int NbExpes { get; init; }
    public int NbRolls { get; init; }
    public int NbVetements { get; init; }
    public int NbTapis { get; init; }
    public int NbSacs { get; init; }
    public int NbRecuperes { get; init; }
    public string? PrecisionLivreur { get; init; }
    public string StatutPassage { get; init; } = "A_FAIRE";
    public string? CommentaireLivreur { get; init; }
    public DateTime? HeureValidation { get; init; }
    public bool EstValidee { get; init; }
}
