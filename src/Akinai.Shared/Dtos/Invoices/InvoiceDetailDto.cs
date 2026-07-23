using Akinai.Shared.Dtos.InvoiceTemplates;

namespace Akinai.Shared.Dtos.Invoices;

public record InvoiceDetailDto(
    Guid Id,
    string InvoiceNumber,
    Guid ClientId,
    string ClientName,
    string? ClientCompanyName,
    string? ClientEmail,
    string? ClientAddress,
    string Currency,
    DateOnly IssueDate,
    DateOnly DueDate,
    DateOnly? PaidDate,
    string Status,
    decimal Subtotal,
    decimal TaxRate,
    decimal TaxAmount,
    decimal Total,
    string? Notes,
    List<InvoiceLineItemDto> LineItems,
    List<InvoiceExpenseLineItemDto> ExpenseLineItems,
    List<InvoiceProductLineItemDto> ProductLineItems,
    Guid? PublicAccessToken = null,
    Guid? TemplateId = null,
    InvoiceTemplateDto? Template = null,
    /// <summary>
    /// The language this invoice will be sent in, as a <c>SupportedLanguage</c>
    /// name. Always populated, so the read-only view can show the client's
    /// labels even when <c>Translated</c> was not requested.
    /// </summary>
    string Language = "English",
    /// <summary>
    /// The language stored on the invoice itself, or null when it inherits.
    ///
    /// Distinct from <see cref="Language"/>, which is the resolved answer. The
    /// edit form must bind to this one: seeding its picker from the resolved
    /// value would turn "inherit" into a pinned choice on the next save.
    /// </summary>
    string? InvoiceLanguage = null,
    /// <summary>When the client first and last opened the link, and how often.</summary>
    DateTime? FirstViewedAtUtc = null,
    DateTime? LastViewedAtUtc = null,
    int ViewCount = 0);
