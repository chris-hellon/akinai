namespace Akinai.Shared.Enums;

public enum RecurrenceFrequency
{
    Day,
    Week,
    Month,
    Year
}

public static class RecurrenceFrequencyExtensions
{
    extension(RecurrenceFrequency frequency)
    {
        public string GetDisplayName() => frequency switch
        {
            RecurrenceFrequency.Day => "Day(s)",
            RecurrenceFrequency.Week => "Week(s)",
            RecurrenceFrequency.Month => "Month(s)",
            RecurrenceFrequency.Year => "Year(s)",
            _ => frequency.ToString()
        };

        public string GetSingular() => frequency switch
        {
            RecurrenceFrequency.Day => "Day",
            RecurrenceFrequency.Week => "Week",
            RecurrenceFrequency.Month => "Month",
            RecurrenceFrequency.Year => "Year",
            _ => frequency.ToString()
        };
    }

    public static RecurrenceFrequency Parse(string frequency) => frequency switch
    {
        "Day" => RecurrenceFrequency.Day,
        "Week" => RecurrenceFrequency.Week,
        "Month" => RecurrenceFrequency.Month,
        "Year" => RecurrenceFrequency.Year,
        _ => RecurrenceFrequency.Month
    };
}
