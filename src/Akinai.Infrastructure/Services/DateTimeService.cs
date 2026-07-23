using Akinai.Shared.Services;

namespace Akinai.Infrastructure.Services;

public class DateTimeService : IDateTimeService
{
    public DateTime Now => DateTime.UtcNow;

    public DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    public DateOnly GetWeekStartDate(DateOnly date)
    {
        var diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
        return date.AddDays(-diff);
    }

    public DateOnly GetWeekEndDate(DateOnly date)
    {
        return GetWeekStartDate(date).AddDays(6);
    }
}
