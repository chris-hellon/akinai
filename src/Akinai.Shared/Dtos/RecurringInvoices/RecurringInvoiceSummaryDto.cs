namespace Akinai.Shared.Dtos.RecurringInvoices;

public record RecurringInvoiceSummaryDto(
    Guid Id,
    Guid ClientId,
    string ClientName,
    string TemplateName,
    string Currency,
    string FrequencyDescription,
    DateOnly NextInvoiceDate,
    bool IsActive,
    int GeneratedCount,
    decimal EstimatedTotal);
