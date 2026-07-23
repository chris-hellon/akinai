using BlazorUtils.EasyApi;

namespace Akinai.Shared.Api.CurrencyPaymentSettings;

[Route("api/currency-payment-settings")]
public class SaveCurrencyPaymentSettings : IPut
{
    [BodyParam]
    public string CurrencyCode { get; init; } = null!;

    [BodyParam]
    public string? BankName { get; init; }

    [BodyParam]
    public string? BankAccountName { get; init; }

    [BodyParam]
    public string? BankAccountNumber { get; init; }

    [BodyParam]
    public string? BankSortCode { get; init; }

    [BodyParam]
    public string? BankIban { get; init; }

    [BodyParam]
    public string? BankSwift { get; init; }

    [BodyParam]
    public string? VietQrBankCode { get; init; }

    /// <summary>
    /// Correspondent bank, for wires that cannot reach the beneficiary bank
    /// directly — most cross-border payments from Asia.
    /// </summary>
    [BodyParam]
    public string? IntermediaryBankName { get; init; }

    [BodyParam]
    public string? IntermediaryBankSwift { get; init; }

    [BodyParam]
    public string? IntermediaryAccountNumber { get; init; }

    [BodyParam]
    public string? BankAddress { get; init; }

    /// <summary>Purpose-of-payment code some jurisdictions require on inbound FX.</summary>
    [BodyParam]
    public string? PaymentPurposeCode { get; init; }
}
