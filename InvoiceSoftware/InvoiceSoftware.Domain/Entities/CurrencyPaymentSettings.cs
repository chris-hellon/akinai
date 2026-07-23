using InvoiceSoftware.Domain.Common;

namespace InvoiceSoftware.Domain.Entities;

public class CurrencyPaymentSettings : IAuditableEntity
{
    public Guid Id { get; private set; }
    public string UserId { get; private set; } = null!;
    public string CurrencyCode { get; private set; } = null!;

    // Bank Details
    public string? BankName { get; private set; }
    public string? BankAccountName { get; private set; }
    public string? BankAccountNumber { get; private set; }
    public string? BankSortCode { get; private set; }
    public string? BankIban { get; private set; }
    public string? BankSwift { get; private set; }

    /// <summary>
    /// Correspondent bank, for wires that cannot reach the beneficiary bank
    /// directly.
    ///
    /// A payment from China, Korea or anywhere outside the beneficiary bank's
    /// own network is routed through a correspondent, and the sender's bank
    /// needs its details to complete the transfer. Omitting them is a common
    /// cause of a wire being returned or held for days — which is exactly the
    /// friction this product exists to remove, and the reason these matter more
    /// than a QR code for the currencies that have no usable QR rail.
    /// </summary>
    public string? IntermediaryBankName { get; private set; }
    public string? IntermediaryBankSwift { get; private set; }

    /// <summary>The beneficiary bank's account at the correspondent.</summary>
    public string? IntermediaryAccountNumber { get; private set; }

    /// <summary>
    /// The beneficiary bank's postal address. Asked for on most cross-border
    /// transfer forms, and not derivable from the SWIFT code by the sender.
    /// </summary>
    public string? BankAddress { get; private set; }

    /// <summary>
    /// Purpose-of-payment code or wording the receiving side requires.
    ///
    /// Several jurisdictions mandate one for inbound foreign currency — Korea
    /// and China among them — and the payer's bank rejects the instruction
    /// without it. Free text because the code lists are national and change.
    /// </summary>
    public string? PaymentPurposeCode { get; private set; }

    // Currency-specific payment options
    public string? VietQrBankCode { get; private set; } // Only for VND

    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = null!;
    public DateTime? ModifiedAt { get; set; }
    public string? ModifiedBy { get; set; }

    private CurrencyPaymentSettings() { }

    public static CurrencyPaymentSettings Create(string userId, string currencyCode)
    {
        return new CurrencyPaymentSettings
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CurrencyCode = currencyCode
        };
    }

    public void Update(
        string? bankName,
        string? bankAccountName,
        string? bankAccountNumber,
        string? bankSortCode,
        string? bankIban,
        string? bankSwift,
        string? vietQrBankCode,
        string? intermediaryBankName = null,
        string? intermediaryBankSwift = null,
        string? intermediaryAccountNumber = null,
        string? bankAddress = null,
        string? paymentPurposeCode = null)
    {
        BankName = bankName;
        BankAccountName = bankAccountName;
        BankAccountNumber = bankAccountNumber;
        BankSortCode = bankSortCode;
        BankIban = bankIban;
        BankSwift = bankSwift;
        VietQrBankCode = vietQrBankCode;
        IntermediaryBankName = intermediaryBankName;
        IntermediaryBankSwift = intermediaryBankSwift;
        IntermediaryAccountNumber = intermediaryAccountNumber;
        BankAddress = bankAddress;
        PaymentPurposeCode = paymentPurposeCode;
    }
}
