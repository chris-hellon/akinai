using BlazorUtils.EasyApi;

namespace Akinai.Shared.Api.Dashboard;

/// <summary>Currencies the user can report their dashboard in (client currencies + business default).</summary>
[Route("api/dashboard/reporting-currencies")]
public class GetReportingCurrencies : IGet<List<string>>
{
}
