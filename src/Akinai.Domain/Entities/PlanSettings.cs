using Akinai.Domain.Common;

namespace Akinai.Domain.Entities;

/// <summary>
/// The editable settings for one plan tier.
///
/// One row per <c>BillingPlan</c> value, never more. The entitlement checks
/// branch on that enum — <c>plan == BillingPlan.Pro</c> and so on — so a row
/// for a tier the code has never heard of could not be gated by anything.
/// Adding a tier stays a code change; changing what a tier costs and unlocks
/// is a row you can edit.
///
/// The code catalogue in <c>Plans.cs</c> remains the seed and the fallback: if
/// this table is empty or unreachable, resolution falls back to it rather than
/// failing open. A billing outage must never hand out paid features.
/// </summary>
public class PlanSettings : IAuditableEntity
{
    public Guid Id { get; private set; }

    /// <summary>The tier, as the <c>BillingPlan</c> enum name. Unique.</summary>
    public string Plan { get; private set; } = null!;

    public string Name { get; private set; } = null!;
    public string Tagline { get; private set; } = null!;

    public decimal MonthlyPrice { get; private set; }
    public decimal AnnualPrice { get; private set; }
    public string Currency { get; private set; } = "GBP";
    public string CurrencySymbol { get; private set; } = "£";

    /// <summary>Null means unlimited.</summary>
    public int? InvoicesPerMonth { get; private set; }

    public bool MultiCurrency { get; private set; }
    public bool AllLanguages { get; private set; }
    public bool BankImport { get; private set; }
    public bool RecurringBilling { get; private set; }
    public bool Estimates { get; private set; }
    public bool CustomTemplates { get; private set; }
    public bool RemoveBranding { get; private set; }

    /// <summary>
    /// Scan-to-pay QR codes on the invoice — SEPA, UPI, PIX, PromptPay, VietQR.
    /// A convenience that gets you paid faster, rather than something that stops the
    /// invoice being wrong, which is why it sits on the paid side.
    /// </summary>
    public bool ScanToPay { get; private set; }

    /// <summary>
    /// Predicted payment dates from the account's own history, and the payment journey.
    /// Needs a body of settled invoices to say anything, so it is of little use on a plan
    /// capped at five a month in any case.
    /// </summary>
    public bool PaymentInsights { get; private set; }

    /// <summary>
    /// Chasing overdue invoices automatically.
    ///
    /// Paid for a reason beyond its value: it sends mail to third parties from the
    /// deployment's own server, so restricting it to accounts with a card on file keeps a
    /// free signup from spending the sending reputation every other user depends on.
    /// </summary>
    public bool AutomaticReminders { get; private set; }

    /// <summary>
    /// The delivery record for an invoice: what was sent, to whom, the document
    /// fingerprint, every opening, every reminder and the signature.
    ///
    /// A flag of its own rather than folded into PaymentInsights. That one predicts when
    /// a client will pay and is pure analysis; this is the record of what actually
    /// happened, and the two are worth withdrawing independently.
    /// </summary>
    public bool DeliveryRecord { get; private set; }

    /// <summary>Monthly AI allowance in USD; null when the tier has none.</summary>
    public decimal? AiSpendLimitUsd { get; private set; }

    /// <summary>
    /// Marketing bullets, one per line.
    ///
    /// Newline-delimited rather than a child table or JSON: it is edited as a
    /// textarea, order matters, and nothing ever queries an individual bullet.
    /// </summary>
    public string Highlights { get; private set; } = "";

    /// <summary>Where this tier sits on the pricing page, ascending.</summary>
    public int SortOrder { get; private set; }

    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = null!;
    public DateTime? ModifiedAt { get; set; }
    public string? ModifiedBy { get; set; }

    private PlanSettings() { }

    public static PlanSettings Create(
        string plan, string name, string tagline,
        decimal monthlyPrice, decimal annualPrice, string currency, string currencySymbol,
        int? invoicesPerMonth, bool multiCurrency, bool allLanguages, bool bankImport,
        bool recurringBilling, bool estimates, bool customTemplates, bool removeBranding,
        bool scanToPay, bool paymentInsights, bool automaticReminders, bool deliveryRecord,
        decimal? aiSpendLimitUsd, string highlights, int sortOrder)
        => new()
        {
            Id = Guid.NewGuid(),
            Plan = plan,
            Name = name,
            Tagline = tagline,
            MonthlyPrice = monthlyPrice,
            AnnualPrice = annualPrice,
            Currency = currency,
            CurrencySymbol = currencySymbol,
            InvoicesPerMonth = invoicesPerMonth,
            MultiCurrency = multiCurrency,
            AllLanguages = allLanguages,
            BankImport = bankImport,
            RecurringBilling = recurringBilling,
            Estimates = estimates,
            CustomTemplates = customTemplates,
            RemoveBranding = removeBranding,
            ScanToPay = scanToPay,
            PaymentInsights = paymentInsights,
            AutomaticReminders = automaticReminders,
            DeliveryRecord = deliveryRecord,
            AiSpendLimitUsd = aiSpendLimitUsd,
            Highlights = highlights,
            SortOrder = sortOrder,
        };

    /// <summary>
    /// Updates what the tier costs and unlocks. The tier itself cannot be
    /// changed — the row's identity is the enum value it stands for.
    /// </summary>
    public void Update(
        string name, string tagline,
        decimal monthlyPrice, decimal annualPrice, string currency, string currencySymbol,
        int? invoicesPerMonth, bool multiCurrency, bool allLanguages, bool bankImport,
        bool recurringBilling, bool estimates, bool customTemplates, bool removeBranding,
        bool scanToPay, bool paymentInsights, bool automaticReminders, bool deliveryRecord,
        decimal? aiSpendLimitUsd, string highlights)
    {
        Name = name;
        Tagline = tagline;
        MonthlyPrice = monthlyPrice;
        AnnualPrice = annualPrice;
        Currency = currency;
        CurrencySymbol = currencySymbol;
        InvoicesPerMonth = invoicesPerMonth;
        MultiCurrency = multiCurrency;
        AllLanguages = allLanguages;
        BankImport = bankImport;
        RecurringBilling = recurringBilling;
        Estimates = estimates;
        CustomTemplates = customTemplates;
        RemoveBranding = removeBranding;
        ScanToPay = scanToPay;
        PaymentInsights = paymentInsights;
        AutomaticReminders = automaticReminders;
        DeliveryRecord = deliveryRecord;
        AiSpendLimitUsd = aiSpendLimitUsd;
        Highlights = highlights;
    }
}
