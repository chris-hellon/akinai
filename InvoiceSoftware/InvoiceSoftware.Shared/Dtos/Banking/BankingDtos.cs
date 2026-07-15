namespace InvoiceSoftware.Shared.Dtos.Banking;

public record BankInstitutionDto(string Id, string Name, string? LogoUrl);

public record BankLinkDto(string AuthUrl, string Reference);

public record BankConnectionDto(
    Guid Id,
    string Provider,
    string InstitutionName,
    string Status,
    DateOnly? ConsentExpiresAt,
    DateTime? LastSyncedAt,
    string? LastError);

public record BankSyncResultDto(int Imported, string Status);
