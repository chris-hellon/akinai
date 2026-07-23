using InvoiceSoftware.Domain.Common;
using InvoiceSoftware.Domain.Enums;

namespace InvoiceSoftware.Domain.Entities;

public class UserLanguagePreference : AggregateRoot, IAuditableEntity
{
    public string UserId { get; private set; } = null!;
    public SupportedLanguage InvoiceLanguage { get; private set; } = SupportedLanguage.English;
    public SupportedLanguage EstimateLanguage { get; private set; } = SupportedLanguage.English;

    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = null!;
    public DateTime? ModifiedAt { get; set; }
    public string? ModifiedBy { get; set; }

    private UserLanguagePreference() { }

    public static UserLanguagePreference Create(string userId)
    {
        return new UserLanguagePreference
        {
            UserId = userId
        };
    }

    public void UpdateInvoiceLanguage(SupportedLanguage language)
    {
        InvoiceLanguage = language;
    }

    public void UpdateEstimateLanguage(SupportedLanguage language)
    {
        EstimateLanguage = language;
    }

    public void UpdateAllLanguages(SupportedLanguage language)
    {
        InvoiceLanguage = language;
        EstimateLanguage = language;
    }

    public static string GetLanguageCode(SupportedLanguage language)
    {
        return language switch
        {
            SupportedLanguage.English => "en",
            SupportedLanguage.Spanish => "es",
            SupportedLanguage.French => "fr",
            SupportedLanguage.German => "de",
            SupportedLanguage.Chinese => "zh",
            SupportedLanguage.Vietnamese => "vi",
            SupportedLanguage.Russian => "ru",
            SupportedLanguage.Korean => "ko",
            _ => "en"
        };
    }

    public static string GetLanguageName(SupportedLanguage language)
    {
        return language switch
        {
            SupportedLanguage.English => "English",
            SupportedLanguage.Spanish => "Español",
            SupportedLanguage.French => "Français",
            SupportedLanguage.German => "Deutsch",
            SupportedLanguage.Chinese => "中文",
            SupportedLanguage.Vietnamese => "Tiếng Việt",
            SupportedLanguage.Russian => "Русский",
            SupportedLanguage.Korean => "한국어",
            _ => "English"
        };
    }
}
