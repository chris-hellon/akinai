namespace Akinai.Shared.Dtos.TimeEntries;

public record TimeEntryDto(
    Guid Id,
    Guid JobId,
    string JobName,
    string ProjectName,
    DateOnly Date,
    decimal Hours,
    string? Description,
    bool IsBilled);
