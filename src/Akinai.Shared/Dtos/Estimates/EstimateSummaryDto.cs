namespace Akinai.Shared.Dtos.Estimates;

public record EstimateSummaryDto(
    Guid Id,
    string EstimateNumber,
    Guid ClientId,
    string ClientName,
    string Currency,
    DateOnly EstimateDate,
    DateOnly? ExpiryDate,
    string Status,
    decimal Subtotal,
    decimal TaxAmount,
    decimal Total);
