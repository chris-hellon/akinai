using Akinai.Shared.Dtos.CurrencyPaymentSettings;
using Akinai.Shared.Dtos.InvoiceTemplates;

namespace Akinai.Shared.Dtos.Invoices;

public record PublicInvoiceDto(
    Guid Id,
    string InvoiceNumber,
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
    // Business profile info for display
    string? BusinessName,
    string? BusinessAddress,
    string? BusinessEmail,
    string? BusinessPhone,
    byte[]? BusinessLogo,
    string? TaxNumber,
    string? RegistrationNumber,
    // Payment options
    string? PayPalMeUsername,
    string? WiseEmail,
    string? RevolutUsername,
    // VND conversion settings
    string? DefaultCurrency,
    decimal? VndToDefaultCurrencyRate,
    // Currency-specific bank details
    CurrencyPaymentSettingsDto? CurrencyPaymentSettings,
    // Template styling
    InvoiceTemplateDto? Template = null,
    /// <summary>
    /// The language this invoice is written in, as a <c>SupportedLanguage</c>
    /// name. Freeform text in the DTO above has already been translated into
    /// it server-side; the renderer uses this only to pick the matching fixed
    /// labels and date format. Defaults to English so an older cached response
    /// still renders.
    /// </summary>
    string Language = "English",
    /// <summary>
    /// Whether to show the "Created with Akinai" line in the footer.
    ///
    /// Resolved server-side from the plan of the account that issued the
    /// document, never from whoever is looking at it — the viewer is an
    /// anonymous client with no plan of their own. Defaults to true so that a
    /// failure to resolve leaves the branding on rather than silently granting
    /// a paid perk.
    /// </summary>
    bool ShowBranding = true,
    List<string>? RequiredStatements = null,
    /// <summary>
    /// Whether the issuing account's plan includes scan-to-pay. Resolved server-side for
    /// the same reason branding is: the person reading a public invoice is an anonymous
    /// client with no plan of their own.
    /// </summary>
    bool ShowScanToPay = false);
