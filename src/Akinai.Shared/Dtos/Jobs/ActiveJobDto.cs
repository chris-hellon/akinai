namespace Akinai.Shared.Dtos.Jobs;

public record ActiveJobDto(
    Guid Id,
    string Name,
    string ProjectName,
    string ClientName,
    decimal HourlyRate,
    string Currency);
