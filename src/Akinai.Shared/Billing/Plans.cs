namespace Akinai.Shared.Billing;

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
    bool ScanToPay,
    bool PaymentInsights,
    bool AutomaticReminders,
    /// <summary>
    /// The delivery record: what was sent, to whom, the document fingerprint, every
    /// opening, every reminder and the signature, as one document that can be printed
    /// and handed over.
    /// </summary>
    bool DeliveryRecord,
    /// <summary>
    /// Monthly AI/translation allowance in USD, or null when the plan has none.
    /// A fuse against runaway usage rather than a real cost centre — see AiPricing.
    /// </summary>
    decimal? AiSpendLimitUsd,
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
        ScanToPay: false,
        PaymentInsights: false,
        AutomaticReminders: false,
        DeliveryRecord: false,
        AiSpendLimitUsd: null,
        Highlights:
        [
            "Unlimited clients and projects",
            $"{FreeInvoicesPerMonth} invoices per month",
            "Time tracking and timesheets",
            "One currency, English invoices",
            "Expenses and reporting",
            "Tax treatment worked out, and VAT numbers checked, before you send",
        ]);

    public static readonly PlanDefinition Pro = new(
        Plan: BillingPlan.Pro,
        Name: "Pro",
        Tagline: "Bill in their language. Get paid on their rail.",
        // £14/mo, or £140 for the year — ten months' price, so "two months free"
        // is exactly true rather than approximately.
        //
        // Pitched below the entry tier of the generalist books (Xero Starter and
        // FreshBooks Lite are both ~£15) while being unmetered where they are not:
        // Xero Starter caps invoices at 20 a month and FreshBooks Lite caps
        // billable clients at 5. Nothing at this price does multi-currency with
        // reporting currency, eight languages, or the local payment rails, which
        // is the whole reason to pick this over them.
        //
        // It was £9. That was underpriced for the feature set and, worse, it read
        // as a hobby project — and raising a price on existing subscribers is
        // harder than starting a little higher.
        MonthlyPrice: 14m,
        AnnualPrice: 140m,
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
        ScanToPay: true,
        PaymentInsights: true,
        AutomaticReminders: true,
        DeliveryRecord: true,
        // ~£1.50 at the time of writing. Denominated in USD because that is
        // what the model providers bill in, so it drifts with the rate and is
        // worth a look occasionally.
        //
        // Not a binding constraint for text features — summarising a timesheet
        // on the cheapest tier costs about $0.0014, so this is roughly 1,400
        // invoices a month. It is headroom for vision work (receipt capture),
        // which is the only thing that could realistically consume it. Raised
        // from $1.33 along with the price; the margin carries it comfortably.
        AiSpendLimitUsd: 2.00m,
        Highlights:
        [
            "Everything in Free, plus:",
            "Unlimited invoices",
            "Every currency, with reporting currency",
            "Invoices and estimates in all eight languages",
            "Bank statement import, matched to open invoices",
            "Recurring invoices and expenses",
            "Estimates your clients sign online",
            "AI-powered invoicing — line items written and translated for you",
            "Scan-to-pay QR codes — SEPA, UPI, PIX, PromptPay and VietQR",
            "Predicted payment dates, learned from how your clients actually pay",
            "Overdue invoices chased for you, in your client's language and working hours",
            "A delivery record for every invoice — proof of what you sent, and when they opened it",
            "Your own branding on invoices",
        ]);

    public static IReadOnlyList<PlanDefinition> All { get; } = [Free, Pro];

    public static PlanDefinition For(BillingPlan plan) => plan switch
    {
        BillingPlan.Pro => Pro,
        _ => Free,
    };
}
