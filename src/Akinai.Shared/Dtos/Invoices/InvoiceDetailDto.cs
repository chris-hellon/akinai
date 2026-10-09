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
    /// <summary>Wording this invoice is required to carry, given the corridor it crosses.</summary>
    List<string> RequiredStatements,
    /// <summary>The tax treatment CorridorRules believes applies, for display.</summary>
    string? TaxTreatment,
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
    DateTime? SentAtUtc = null,
    DateTime? FirstViewedAtUtc = null,
    DateTime? LastViewedAtUtc = null,
    int ViewCount = 0,
    /// <summary>
    /// True when the services rows above are a saved summary rather than one
    /// row per tracked entry. The rows look identical either way, so the owner
    /// needs telling which they are looking at.
    /// </summary>
    bool IsSummarised = false,
    /// <summary>
    /// How many time entries this invoice bills, regardless of how they are
    /// presented. Drives whether summarising is worth offering.
    /// </summary>
    int TimeEntryCount = 0);
