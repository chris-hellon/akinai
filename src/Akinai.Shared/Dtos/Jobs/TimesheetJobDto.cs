namespace Akinai.Shared.Dtos.Jobs;

public record TimesheetJobDto(
    Guid Id,
    string Name,
    string ProjectName,
    string ClientName,
    decimal HourlyRate,
    bool IsActive);
