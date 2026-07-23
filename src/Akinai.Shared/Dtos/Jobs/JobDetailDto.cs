namespace Akinai.Shared.Dtos.Jobs;

public record JobDetailDto(
    Guid Id,
    Guid ProjectId,
    string ProjectName,
    Guid ClientId,
    string ClientName,
    Guid? SectionId,
    string? SectionName,
    string Name,
    string? Description,
    string? Notes,
    string Status,
    string Priority,
    DateOnly? StartDate,
    DateOnly? DueDate,
    decimal? EstimatedHours,
    decimal? HourlyRateOverride,
    decimal TotalHours,
    decimal BilledHours,
    List<JobTaskDto> Tasks);

public record JobTaskDto(
    Guid Id,
    string Title,
    string? Description,
    bool IsComplete,
    int Order,
    DateTime? CompletedAt);
