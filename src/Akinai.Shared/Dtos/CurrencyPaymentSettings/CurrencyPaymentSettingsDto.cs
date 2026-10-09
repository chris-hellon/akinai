namespace Akinai.Shared.Dtos.CurrencyPaymentSettings;

public record CurrencyPaymentSettingsDto(
    Guid Id,
    string CurrencyCode,
    string? BankName,
    string? BankAccountName,
    string? BankAccountNumber,
    string? BankSortCode,
    string? BankIban,
    string? BankSwift,
    string? VietQrBankCode,
    // Correspondent bank — needed by senders whose bank has no direct
    // relationship with the beneficiary bank, which is most cross-border wires.
    string? IntermediaryBankName = null,
    string? IntermediaryBankSwift = null,
    string? IntermediaryAccountNumber = null,
    string? BankAddress = null,
    string? PaymentPurposeCode = null,
    string? UpiVpa = null,
    string? PixKey = null,
    string? PromptPayId = null);
