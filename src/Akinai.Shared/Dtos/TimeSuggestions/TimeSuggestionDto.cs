namespace Akinai.Shared.Dtos.TimeSuggestions;

public record TimeSuggestionDto(
    Guid JobId,
    string JobName,
    Guid ProjectId,
    string ProjectName,
    Guid ClientId,
    string ClientName,
    string SuggestedDescription,
    decimal SuggestedHours,
    int UsageCount,
    DateOnly LastUsed,
    decimal ConfidenceScore);
