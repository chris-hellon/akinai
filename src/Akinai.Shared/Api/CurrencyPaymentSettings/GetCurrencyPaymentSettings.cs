using BlazorUtils.EasyApi;
using Akinai.Shared.Dtos.CurrencyPaymentSettings;

namespace Akinai.Shared.Api.CurrencyPaymentSettings;

[Route("api/currency-payment-settings/{CurrencyCode}")]
public class GetCurrencyPaymentSettings : IGet<CurrencyPaymentSettingsDto?>
{
    [RouteParam]
    public string CurrencyCode { get; init; } = null!;
}
