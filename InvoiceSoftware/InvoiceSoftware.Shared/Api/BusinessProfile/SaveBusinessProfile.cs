using BlazorUtils.EasyApi;

namespace InvoiceSoftware.Shared.Api.BusinessProfile;

[Route("api/business-profile")]
public class SaveBusinessProfile : IPut
{
    [BodyParam]
    public string CompanyName { get; init; } = null!;

    [BodyParam]
    public string? TradingName { get; init; }

    [BodyParam]
    public string Email { get; init; } = null!;

    [BodyParam]
    public string? Phone { get; init; }

    [BodyParam]
    public string? Website { get; init; }

    [BodyParam]
    public string? Street { get; init; }

    [BodyParam]
    public string? City { get; init; }

    [BodyParam]
    public string? State { get; init; }

    [BodyParam]
    public string? PostalCode { get; init; }

    [BodyParam]
    public string? Country { get; init; }

    [BodyParam]
    public string? TaxNumber { get; init; }

    [BodyParam]
    public string? RegistrationNumber { get; init; }

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
    public string DefaultCurrency { get; init; } = "USD";

    [BodyParam]
    public int DefaultPaymentTermsDays { get; init; } = 30;

    [BodyParam]
    public string? InvoiceNotes { get; init; }

    [BodyParam]
    public string? InvoiceFooter { get; init; }

    [BodyParam]
    public string? InvoiceEmailSubject { get; init; }

    [BodyParam]
    public string? InvoiceEmailBody { get; init; }

    [BodyParam]
    public string? PayPalMeUsername { get; init; }

    [BodyParam]
    public string? WiseEmail { get; init; }

    [BodyParam]
    public string? RevolutUsername { get; init; }

    [BodyParam]
    public string? VietQrBankCode { get; init; }
}
