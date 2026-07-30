namespace Akinai.Shared.Dtos.Estimates;

public record PublicEstimateDto(
    Guid Id,
    string EstimateNumber,
    string ClientName,
    string? ClientCompanyName,
    string? ClientAddress,
    string? ClientEmail,
    string Currency,
    DateOnly EstimateDate,
    DateOnly? ExpiryDate,
    DateOnly? AcceptedDate,
    DateOnly? RejectedDate,
    string Status,
    string? Notes,
    string? Terms,
    List<EstimateLineItemDto> LineItems,
    decimal Subtotal,
    decimal TaxRate,
    decimal TaxAmount,
    decimal Total,
    string? BusinessName,
    string? BusinessEmail,
    string? BusinessPhone,
    string? BusinessStreet,
    string? BusinessCity,
    string? BusinessState,
    string? BusinessPostalCode,
    string? BusinessCountry,
    string? TaxNumber,
    string? RegistrationNumber,
    byte[]? BusinessLogo,
    string? BusinessLogoContentType,
    // Template properties
    string? PrimaryColor,
    string? AccentColor,
    bool ShowLogo,
    string? HeaderLayout,
    string? ItemsLayout,
    string? FooterLayout,
    /// <summary>
    /// The language this estimate is written in, as a <c>SupportedLanguage</c>
    /// name. Freeform text above has already been translated into it
    /// server-side; the renderer uses this only to pick the matching fixed
    /// labels and date format.
    /// </summary>
    string Language = "English",
    /// <summary>
    /// Whether to show the "Created with Akinai" line in the footer. Resolved
    /// from the issuing account's plan, never the viewer's. Defaults to true so
    /// a failure to resolve leaves branding on rather than granting a paid perk.
    /// </summary>
    bool ShowBranding = true);
