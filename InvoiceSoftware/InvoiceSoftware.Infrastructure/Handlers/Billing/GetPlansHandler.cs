using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using InvoiceSoftware.Shared.Api.Billing;
using InvoiceSoftware.Shared.Billing;
using InvoiceSoftware.Shared.Services;

namespace InvoiceSoftware.Infrastructure.Handlers.Billing;

public class GetPlansHandler(IPlanCatalog catalog) : IHandle<GetPlans, List<PlanDefinition>>
{
    public async Task<HttpResult<List<PlanDefinition>>> Handle(GetPlans request, CancellationToken cancellationToken)
        => HttpResult<List<PlanDefinition>>.Ok((await catalog.AllAsync(cancellationToken)).ToList());
}
