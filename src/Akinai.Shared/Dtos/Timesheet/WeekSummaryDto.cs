namespace Akinai.Shared.Dtos.Timesheet;

/// <summary>
/// A week of tracked time, rewritten as something a person would actually read.
///
/// Nothing here is saved and nothing is priced. It answers "what did I do this week" —
/// for a status update, a standup, or a client asking — out of the scrappy rows that were
/// logged at the time. The invoice summariser does the same job where money is attached;
/// this one deliberately does not, so it can run over work that will never be billed.
/// </summary>
public record WeekSummaryDto(
    bool Available,
    string? Reason,
    List<ClientWeekSummaryDto> Clients)
{
    public static WeekSummaryDto Unavailable(string reason) => new(false, reason, []);

    public decimal TotalHours => Clients.Sum(c => c.Hours);
}

/// <param name="Hours">
/// Summed from the entries themselves, never from the model. The wording is the only
/// thing a language model contributes.
/// </param>
public record ClientWeekSummaryDto(
    string ClientName,
    decimal Hours,
    List<WeekSummaryLineDto> Lines);

public record WeekSummaryLineDto(string Description, decimal Hours);
