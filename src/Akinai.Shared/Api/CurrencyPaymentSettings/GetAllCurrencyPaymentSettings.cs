using BlazorUtils.EasyApi;
using Akinai.Shared.Dtos.CurrencyPaymentSettings;

namespace Akinai.Shared.Api.CurrencyPaymentSettings;

[Route("api/currency-payment-settings")]
public class GetAllCurrencyPaymentSettings : IGet<List<CurrencyPaymentSettingsDto>>
{
}
