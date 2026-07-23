using BlazorUtils.EasyApi;

namespace Akinai.Shared.Api.Admin;

/// <summary>
/// Updates what one tier costs and unlocks.
///
/// The tier itself is the route — you edit "Pro", you cannot create "Team".
/// Adding a tier stays a code change, because the entitlement checks branch on
/// the BillingPlan enum and could not gate a tier they have never heard of.
/// </summary>
[Route("api/admin/plans/{Plan}")]
public class SavePlanSettings : IPut
{
    [RouteParam] public string Plan { get; init; } = "";

    [BodyParam] public string Name { get; init; } = "";
    [BodyParam] public string Tagline { get; init; } = "";
    [BodyParam] public decimal MonthlyPrice { get; init; }
    [BodyParam] public decimal AnnualPrice { get; init; }
    [BodyParam] public string Currency { get; init; } = "GBP";
    [BodyParam] public string CurrencySymbol { get; init; } = "£";

    /// <summary>Null means unlimited.</summary>
    [BodyParam] public int? InvoicesPerMonth { get; init; }

    [BodyParam] public bool MultiCurrency { get; init; }
    [BodyParam] public bool AllLanguages { get; init; }
    [BodyParam] public bool BankImport { get; init; }
    [BodyParam] public bool RecurringBilling { get; init; }
    [BodyParam] public bool Estimates { get; init; }
    [BodyParam] public bool CustomTemplates { get; init; }
    [BodyParam] public bool RemoveBranding { get; init; }
    [BodyParam] public decimal? AiSpendLimitUsd { get; init; }

    /// <summary>Marketing bullets, one per line.</summary>
    [BodyParam] public string Highlights { get; init; } = "";
}
