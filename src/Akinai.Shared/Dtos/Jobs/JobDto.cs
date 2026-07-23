namespace Akinai.Shared.Dtos.Jobs;

public record JobDto(
    Guid Id,
    Guid ProjectId,
    string ProjectName,
    Guid ClientId,
    string ClientName,
    Guid? SectionId,
    string? SectionName,
    string Name,
    string? Description,
    string Status,
    decimal? EstimatedHours,
    decimal? HourlyRateOverride);
