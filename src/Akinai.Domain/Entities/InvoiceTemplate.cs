using Akinai.Domain.Common;
using Akinai.Domain.Enums;
using Akinai.Domain.Exceptions;

namespace Akinai.Domain.Entities;

public class InvoiceTemplate : AggregateRoot, IAuditableEntity
{
    public string? UserId { get; private set; }
    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }
    public bool IsDefault { get; private set; }
    public bool IsSystem { get; private set; }
    public InvoiceTemplateType TemplateType { get; private set; }

    public string PrimaryColor { get; private set; } = "#0e5c66"; // Indigo
    public string AccentColor { get; private set; } = "#059669";
    public string TextColor { get; private set; } = "#1F2937";
    public string BackgroundColor { get; private set; } = "#FFFFFF";

    public bool ShowLogo { get; private set; } = true;
    public bool ShowPaymentQR { get; private set; } = true;
    public bool ShowBankDetails { get; private set; } = true;
    public bool ShowItemDescriptions { get; private set; } = true;

    public string HeaderLayout { get; private set; } = "standard"; // standard, centered, minimal
    public string ItemsLayout { get; private set; } = "table"; // table, list, cards
    public string FooterLayout { get; private set; } = "standard"; // standard, minimal, detailed

    public string? FontFamily { get; private set; }
    public string? CustomCss { get; private set; }

    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = null!;
    public DateTime? ModifiedAt { get; set; }
    public string? ModifiedBy { get; set; }

    private InvoiceTemplate() { }

    public static InvoiceTemplate Create(
        string? userId,
        string name,
        InvoiceTemplateType templateType,
        string? description = null,
        bool isSystem = false)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Template name is required");

        return new InvoiceTemplate
        {
            UserId = userId,
            Name = name,
            Description = description,
            TemplateType = templateType,
            IsSystem = isSystem
        };
    }

    public static InvoiceTemplate CreateSystemTemplate(
        string name,
        InvoiceTemplateType templateType,
        string primaryColor,
        string accentColor,
        string? description = null)
    {
        var template = Create(null, name, templateType, description, isSystem: true);
        template.PrimaryColor = primaryColor;
        template.AccentColor = accentColor;
        return template;
    }

    public void Update(
        string name,
        string? description,
        InvoiceTemplateType templateType)
    {
        if (IsSystem)
            throw new DomainException("Cannot modify a system template");
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Template name is required");

        Name = name;
        Description = description;
        TemplateType = templateType;
    }

    public void UpdateColors(
        string primaryColor,
        string accentColor,
        string textColor,
        string backgroundColor)
    {
        if (IsSystem)
            throw new DomainException("Cannot modify a system template");

        PrimaryColor = ValidateColor(primaryColor, nameof(primaryColor));
        AccentColor = ValidateColor(accentColor, nameof(accentColor));
        TextColor = ValidateColor(textColor, nameof(textColor));
        BackgroundColor = ValidateColor(backgroundColor, nameof(backgroundColor));
    }

    public void UpdateDisplayOptions(
        bool showLogo,
        bool showPaymentQR,
        bool showBankDetails,
        bool showItemDescriptions)
    {
        if (IsSystem)
            throw new DomainException("Cannot modify a system template");

        ShowLogo = showLogo;
        ShowPaymentQR = showPaymentQR;
        ShowBankDetails = showBankDetails;
        ShowItemDescriptions = showItemDescriptions;
    }

    public void UpdateLayouts(
        string headerLayout,
        string itemsLayout,
        string footerLayout)
    {
        if (IsSystem)
            throw new DomainException("Cannot modify a system template");

        HeaderLayout = headerLayout;
        ItemsLayout = itemsLayout;
        FooterLayout = footerLayout;
    }

    public void UpdateCustomization(string? fontFamily, string? customCss)
    {
        if (IsSystem)
            throw new DomainException("Cannot modify a system template");

        FontFamily = fontFamily;
        CustomCss = customCss;
    }

    public void SetAsDefault()
    {
        IsDefault = true;
    }

    public void ClearDefault()
    {
        IsDefault = false;
    }

    private static string ValidateColor(string color, string paramName)
    {
        if (string.IsNullOrWhiteSpace(color))
            throw new DomainException($"{paramName} is required");

        if (!color.StartsWith('#') || (color.Length != 7 && color.Length != 4))
            throw new DomainException($"{paramName} must be a valid hex color code");

        return color.ToUpperInvariant();
    }
}
