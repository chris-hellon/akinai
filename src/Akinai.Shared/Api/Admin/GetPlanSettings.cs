using BlazorUtils.EasyApi;
using Akinai.Shared.Billing;

namespace Akinai.Shared.Api.Admin;

/// <summary>Every tier, for the admin editor. Admin-only via the /api/admin gate.</summary>
[Route("api/admin/plans")]
public class GetPlanSettings : IGet<List<PlanDefinition>>;
