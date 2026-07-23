using Akinai.Shared.Dtos.Jobs;
using Akinai.Shared.Dtos.ProjectSections;

namespace Akinai.Shared.Dtos.Projects;

public record ProjectDetailDto(
    Guid Id,
    Guid ClientId,
    string ClientName,
    string Name,
    string? Description,
    string Status,
    DateOnly? StartDate,
    DateOnly? EndDate,
    decimal? HourlyRateOverride,
    List<JobDto> Jobs,
    List<ProjectSectionDto> Sections);
