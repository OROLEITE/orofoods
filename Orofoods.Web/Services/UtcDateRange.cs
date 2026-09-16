namespace Orofoods.Web.Services;

public static class UtcDateRange
{
    public static DateTime MonthStart(DateTime utcDateTime) =>
        new(utcDateTime.Year, utcDateTime.Month, 1, 0, 0, 0, DateTimeKind.Utc);
}
