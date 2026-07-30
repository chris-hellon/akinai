using Akinai.Shared.Dtos.InvoiceTemplates;

namespace Akinai.Shared.Dtos.Estimates;

public record EstimateDetailDto(
    Guid Id,
    string EstimateNumber,
    Guid ClientId,
    string ClientName,
    string? ClientCompanyName,
    string ClientEmail,
    string? ClientPhone,
    string? ClientStreet,
    string? ClientCity,
    string? ClientState,
    string? ClientPostalCode,
    string? ClientCountry,
    string Currency,
    DateOnly EstimateDate,
    DateOnly? ExpiryDate,
    int ValidDays,
    string Status,
    decimal TaxRate,
    string? Notes,
    string? Terms,
    Guid? PublicAccessToken,
    Guid? ConvertedInvoiceId,
    DateOnly? AcceptedDate,
    DateOnly? RejectedDate,
    List<EstimateLineItemDto> LineItems,
    decimal Subtotal,
    decimal TaxAmount,
    decimal Total,
    DateTime CreatedAt,
    DateTime? ModifiedAt,
    Guid? TemplateId = null,
    InvoiceTemplateDto? Template = null,
    /// <summary>
    /// The language pinned on this estimate, or null to inherit. Mirrors
    /// InvoiceDetailDto.InvoiceLanguage so the two edit forms behave alike.
    /// </summary>
    string? EstimateLanguage = null);
