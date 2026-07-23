using BlazorUtils.EasyApi;
using InvoiceSoftware.Shared.Billing;

namespace InvoiceSoftware.Shared.Api.Admin;

/// <summary>Every tier, for the admin editor. Admin-only via the /api/admin gate.</summary>
[Route("api/admin/plans")]
public class GetPlanSettings : IGet<List<PlanDefinition>>;
