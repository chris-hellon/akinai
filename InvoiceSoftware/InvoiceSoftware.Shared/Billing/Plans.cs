namespace InvoiceSoftware.Shared.Billing;

/// <summary>
/// What a plan costs and what it unlocks.
///
/// A limit of <c>null</c> means unlimited. Prices are held here rather than in
/// the database so that a plan change is a code change with a diff and a
/// review, not a row someone can edit by accident.
/// </summary>
public record PlanDefinition(
    BillingPlan Plan,
    string Name,
    string Tagline,
    decimal MonthlyPrice,
    decimal AnnualPrice,
    string Currency,
    string CurrencySymbol,
    int? InvoicesPerMonth,
    bool MultiCurrency,
    bool AllLanguages,
    bool BankImport,
    bool RecurringBilling,
    bool Estimates,
    bool CustomTemplates,
    bool RemoveBranding,
    IReadOnlyList<string> Highlights)
{
    public decimal PriceFor(BillingCycle cycle) => cycle == BillingCycle.Annual ? AnnualPrice : MonthlyPrice;

    /// <summary>Months of the monthly price saved by paying annually.</summary>
    public decimal AnnualMonthsFree => MonthlyPrice == 0 ? 0 : Math.Round(12 - (AnnualPrice / MonthlyPrice), 1);

    public bool IsPaid => MonthlyPrice > 0;
}

/// <summary>
/// The plan catalogue. Everything that gates a feature reads from here, so the
/// pricing page and the server-side checks can never drift apart.
///
/// The split follows what actually makes this product different: billing
/// across borders. Someone invoicing domestically in one currency can work
/// free forever; the cross-border machinery — currencies, languages, bank
/// reconciliation, retainers — is what's paid for.
/// </summary>
public static class Plans
{
    public const int FreeInvoicesPerMonth = 5;

    public static readonly PlanDefinition Free = new(
        Plan: BillingPlan.Free,
        Name: "Free",
        Tagline: "Invoice domestically, in one currency.",
        MonthlyPrice: 0m,
        AnnualPrice: 0m,
        Currency: "GBP",
        CurrencySymbol: "£",
        InvoicesPerMonth: FreeInvoicesPerMonth,
        MultiCurrency: false,
        AllLanguages: false,
        BankImport: false,
        RecurringBilling: false,
        Estimates: false,
        CustomTemplates: false,
        RemoveBranding: false,
        Highlights:
        [
            "Unlimited clients and projects",
            $"{FreeInvoicesPerMonth} invoices per month",
            "Time tracking and timesheets",
            "One currency, English invoices",
            "Expenses and reporting",
        ]);

    public static readonly PlanDefinition Pro = new(
        Plan: BillingPlan.Pro,
        Name: "Pro",
        Tagline: "Bill in their language. Get paid on their rail.",
        MonthlyPrice: 12m,
        AnnualPrice: 120m,
        Currency: "GBP",
        CurrencySymbol: "£",
        InvoicesPerMonth: null,
        MultiCurrency: true,
        AllLanguages: true,
        BankImport: true,
        RecurringBilling: true,
        Estimates: true,
        CustomTemplates: true,
        RemoveBranding: true,
        Highlights:
        [
            "Everything in Free, plus:",
            "Unlimited invoices",
            "Every currency, with reporting currency",
            "Invoices and estimates in all six languages",
            "Bank statement import, matched to open invoices",
            "Recurring invoices and expenses",
            "Estimates your clients sign online",
            "Your own branding on invoices",
        ]);

    public static IReadOnlyList<PlanDefinition> All { get; } = [Free, Pro];

    public static PlanDefinition For(BillingPlan plan) => plan switch
    {
        BillingPlan.Pro => Pro,
        _ => Free,
    };
}
