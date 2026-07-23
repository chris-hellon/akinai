namespace Akinai.Shared.Dtos.Signatures;

public record SignatureDto(
    Guid Id,
    string LinkedEntityType,
    Guid LinkedEntityId,
    string SignerName,
    string? SignerEmail,
    DateTime SignedAt,
    bool IsValid,
    string? InvalidationReason,
    string SignatureDataBase64);
