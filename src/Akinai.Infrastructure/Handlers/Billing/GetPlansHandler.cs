using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using Akinai.Shared.Api.Billing;
using Akinai.Shared.Billing;
using Akinai.Shared.Services;

namespace Akinai.Infrastructure.Handlers.Billing;

public class GetPlansHandler(IPlanCatalog catalog) : IHandle<GetPlans, List<PlanDefinition>>
{
    public async Task<HttpResult<List<PlanDefinition>>> Handle(GetPlans request, CancellationToken cancellationToken)
        => HttpResult<List<PlanDefinition>>.Ok((await catalog.AllAsync(cancellationToken)).ToList());
}
