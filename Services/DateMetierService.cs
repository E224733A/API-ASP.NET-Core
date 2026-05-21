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
    /// Règle métier : l'Expédition prépare les tournées du lendemain.
    /// Exemple : le 21/05/2026, le Web Expédition prépare la tournée du 22/05/2026.
    /// </summary>
    public DateOnly GetDateTourneeExpeditionPreparable()
    {
        return GetDateTourneeMobileAutorisee().AddDays(1);
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
