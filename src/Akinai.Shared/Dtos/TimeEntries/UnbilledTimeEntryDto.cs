namespace Akinai.Shared.Dtos.TimeEntries;

public record UnbilledTimeEntryDto(
    Guid Id,
    Guid JobId,
    string JobName,
    string ProjectName,
    string? SectionName,
    DateOnly Date,
    decimal Hours,
    decimal HourlyRate,
    string Currency);
