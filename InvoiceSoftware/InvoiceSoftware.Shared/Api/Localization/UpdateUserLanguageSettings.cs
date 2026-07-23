using BlazorUtils.EasyApi;

namespace InvoiceSoftware.Shared.Api.Localization;

[Route("api/localization/settings")]
public class UpdateUserLanguageSettings : IPut
{

    [BodyParam]
    public string InvoiceLanguage { get; init; } = "English";

    [BodyParam]
    public string EstimateLanguage { get; init; } = "English";
}
