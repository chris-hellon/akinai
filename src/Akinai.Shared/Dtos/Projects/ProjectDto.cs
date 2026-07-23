namespace Akinai.Shared.Dtos.Projects;

public record ProjectDto(
    Guid Id,
    Guid ClientId,
    string ClientName,
    string Currency,
    string Name,
    string? Description,
    string Status,
    DateOnly? StartDate,
    DateOnly? EndDate,
    decimal? HourlyRateOverride,
    int JobCount);
