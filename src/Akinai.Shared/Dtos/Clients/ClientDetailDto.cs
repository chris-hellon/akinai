namespace Akinai.Shared.Dtos.Clients;

public record ClientDetailDto(
    Guid Id,
    string Name,
    string? CompanyName,
    string Email,
    string? Phone,
    decimal DefaultHourlyRate,
    string Currency,
    bool IsActive,
    string? Street,
    string? City,
    string? State,
    string? PostalCode,
    string? Country,
    int ProjectCount,
    int ActiveProjectCount);
