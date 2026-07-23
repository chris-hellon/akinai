using BlazorUtils.EasyApi;
using Akinai.Shared.Dtos.Jobs;

namespace Akinai.Shared.Api.Jobs;

[Route("api/jobs/active")]
public class GetActiveJobs : IGet<List<ActiveJobDto>>
{
}
