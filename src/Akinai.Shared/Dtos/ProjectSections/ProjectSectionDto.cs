namespace Akinai.Shared.Dtos.ProjectSections;

public record ProjectSectionDto(
    Guid Id,
    Guid ProjectId,
    string Name,
    string? Description,
    int Order,
    int JobCount);
