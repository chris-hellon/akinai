namespace Akinai.Shared.Dtos.InvoiceTemplates;

public record InvoiceTemplateDto(
    Guid Id,
    string Name,
    string? Description,
    bool IsDefault,
    bool IsSystem,
    string TemplateType,
    string PrimaryColor,
    string AccentColor,
    string TextColor,
    string BackgroundColor,
    bool ShowLogo,
    bool ShowPaymentQR,
    bool ShowBankDetails,
    bool ShowItemDescriptions,
    string HeaderLayout,
    string ItemsLayout,
    string FooterLayout,
    string? FontFamily,
    string? CustomCss);
