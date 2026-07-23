using BlazorUtils.EasyApi;
using InvoiceSoftware.Shared.Billing;

namespace InvoiceSoftware.Shared.Api.Billing;

/// <summary>
/// The current plan catalogue.
///
/// Public, because the pricing section on the marketing page needs it before
/// anyone has an account. It exposes only what is already printed on that page
/// — names, prices and feature bullets — and nothing account-specific.
/// </summary>
[Route("api/plans")]
public class GetPlans : IGet<List<PlanDefinition>>;
