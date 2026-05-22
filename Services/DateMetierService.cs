namespace API_ASP.NET_Core.Services;

public sealed class DateMetierService
{
    private const string WindowsParisTimeZoneId = "Romance Standard Time";
    private const string IanaParisTimeZoneId = "Europe/Paris";

    /// <summary>
    /// Date métier utilisée par les routes mobile.
    ///
    /// Règle métier : le mobile travaille sur la tournée du jour.
    /// Exemple : le 21/05/2026, le mobile charge et synchronise la tournée du 21/05/2026.
    /// </summary>
    public DateOnly GetDateTourneeMobileAutorisee()
    {
        return DateOnly.FromDateTime(GetNowParis().DateTime);
    }

    /// <summary>
    /// Date métier utilisée par le module Expédition.
    ///
    /// Règle métier : l'Expédition prépare le prochain jour ouvré métier après la date du jour.
    /// Dans cette première version, les jours non ouvrés sont uniquement le samedi et le dimanche.
    ///
    /// Exemples :
    /// - lundi    -> mardi
    /// - mardi    -> mercredi
    /// - mercredi -> jeudi
    /// - jeudi    -> vendredi
    /// - vendredi -> lundi
    /// - samedi   -> lundi
    /// - dimanche -> lundi
    /// </summary>
    public DateOnly GetDateTourneeExpeditionPreparable()
    {
        return GetProchainJourOuvreExpedition(GetDateTourneeMobileAutorisee());
    }

    /// <summary>
    /// Calcule le prochain jour ouvré métier Expédition après une date de référence.
    /// Cette méthode permet de tester facilement la règle sans dépendre de l'heure système.
    /// </summary>
    public DateOnly GetProchainJourOuvreExpedition(DateOnly dateReference)
    {
        var candidate = dateReference.AddDays(1);

        while (EstJourNonOuvreExpedition(candidate))
        {
            candidate = candidate.AddDays(1);
        }

        return candidate;
    }

    /// <summary>
    /// Indique si une date est non ouvrée pour l'Expédition.
    /// Version actuelle : week-end uniquement.
    /// Évolution prévue : ajouter une table calendrier métier pour les jours fériés,
    /// ponts et fermetures exceptionnelles.
    /// </summary>
    public bool EstJourNonOuvreExpedition(DateOnly date)
    {
        return date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
    }

    /// <summary>
    /// Méthode historique conservée pour ne pas casser le mobile ni les validations existantes.
    /// Elle correspond volontairement à la date autorisée pour le mobile, donc à la date du jour.
    /// </summary>
    public DateOnly GetDateTourneeAutorisee()
    {
        return GetDateTourneeMobileAutorisee();
    }

    public DateTimeOffset GetNowParis()
    {
        return TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, GetParisTimeZone());
    }

    public bool EstDateTourneeAutorisee(DateOnly dateTournee)
    {
        return dateTournee == GetDateTourneeAutorisee();
    }

    public bool EstDateTourneeAutorisee(DateTime dateTournee)
    {
        return EstDateTourneeAutorisee(DateOnly.FromDateTime(dateTournee.Date));
    }

    public bool EstDateTourneeExpeditionPreparable(DateOnly dateTournee)
    {
        return dateTournee == GetDateTourneeExpeditionPreparable();
    }

    public bool EstDateTourneeExpeditionPreparable(DateTime dateTournee)
    {
        return EstDateTourneeExpeditionPreparable(DateOnly.FromDateTime(dateTournee.Date));
    }

    private static TimeZoneInfo GetParisTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(WindowsParisTimeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById(IanaParisTimeZoneId);
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById(IanaParisTimeZoneId);
        }
    }
}
