namespace Akinai.Shared.Dtos.ClientPortal;

public record ClientPortalEstimateDto(
    Guid Id,
    string EstimateNumber,
    DateOnly EstimateDate,
    DateOnly? ExpiryDate,
    string Status,
    decimal Total,
    string Currency,
    DateOnly? AcceptedDate,
    DateOnly? RejectedDate,
    Guid? PublicAccessToken,
    bool CanAccept,
    bool CanReject);
