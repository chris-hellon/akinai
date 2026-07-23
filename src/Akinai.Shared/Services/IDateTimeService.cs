namespace Akinai.Shared.Services;

public interface IDateTimeService
{
    DateTime Now { get; }
    DateOnly Today { get; }
    DateOnly GetWeekStartDate(DateOnly date);
    DateOnly GetWeekEndDate(DateOnly date);
}
