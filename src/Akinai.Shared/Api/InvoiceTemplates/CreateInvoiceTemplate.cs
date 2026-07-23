using BlazorUtils.EasyApi;

namespace Akinai.Shared.Api.InvoiceTemplates;

[Route("api/invoice-templates")]
public class CreateInvoiceTemplate : IPost<Guid>
{
    [BodyParam]
    public string Name { get; init; } = null!;

    [BodyParam]
    public string? Description { get; init; }

    [BodyParam]
    public string TemplateType { get; init; } = "Professional";

    [BodyParam]
    public string PrimaryColor { get; init; } = "#0e5c66";

    [BodyParam]
    public string AccentColor { get; init; } = "#059669";

    [BodyParam]
    public string TextColor { get; init; } = "#1F2937";

    [BodyParam]
    public string BackgroundColor { get; init; } = "#FFFFFF";

    [BodyParam]
    public bool ShowLogo { get; init; } = true;

    [BodyParam]
    public bool ShowPaymentQR { get; init; } = true;

    [BodyParam]
    public bool ShowBankDetails { get; init; } = true;

    [BodyParam]
    public bool ShowItemDescriptions { get; init; } = true;

    [BodyParam]
    public string HeaderLayout { get; init; } = "standard";

    [BodyParam]
    public string ItemsLayout { get; init; } = "table";

    [BodyParam]
    public string FooterLayout { get; init; } = "standard";

    [BodyParam]
    public string? FontFamily { get; init; }
}
