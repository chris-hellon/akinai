using BlazorUtils.EasyApi;

namespace Akinai.Shared.Api.Localization;

[Route("api/localization/clients/{ClientId}")]
public class SetClientLanguageOverride : IPut
{
    [RouteParam]
    public Guid ClientId { get; init; }

    [BodyParam]
    public string PreferredLanguage { get; init; } = null!;

    [BodyParam]
    public bool UseForInvoices { get; init; } = true;

    [BodyParam]
    public bool UseForEstimates { get; init; } = true;
}
