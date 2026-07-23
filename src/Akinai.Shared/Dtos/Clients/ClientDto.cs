namespace Akinai.Shared.Dtos.Clients;

public record ClientDto(
    Guid Id,
    string Name,
    string? CompanyName,
    string Email,
    string? Phone,
    decimal DefaultHourlyRate,
    string Currency,
    bool IsActive);
