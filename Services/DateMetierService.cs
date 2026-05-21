namespace API_ASP.NET_Core.Services;

public sealed class DateMetierService
{
    private const string WindowsParisTimeZoneId = "Romance Standard Time";
    private const string IanaParisTimeZoneId = "Europe/Paris";

    public DateOnly GetDateTourneeAutorisee()
    {
        return DateOnly.FromDateTime(GetNowParis().DateTime);
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
