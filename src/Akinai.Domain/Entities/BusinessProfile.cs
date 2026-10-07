using Akinai.Domain.Common;
using Akinai.Domain.ValueObjects;

namespace Akinai.Domain.Entities;

public class BusinessProfile : AggregateRoot, IAuditableEntity
{
    public string UserId { get; private set; } = null!;
    public string CompanyName { get; private set; } = null!;
    public string? TradingName { get; private set; }
    public EmailAddress Email { get; private set; } = null!;
    public PhoneNumber? Phone { get; private set; }
    public string? Website { get; private set; }
    public Address? Address { get; private set; }

    // Tax/Registration Info
    public string? TaxNumber { get; private set; }
    public string? RegistrationNumber { get; private set; }

    // Banking Info for invoices
    public string? BankName { get; private set; }
    public string? BankAccountName { get; private set; }
    public string? BankAccountNumber { get; private set; }
    public string? BankSortCode { get; private set; }
    public string? BankIban { get; private set; }
    public string? BankSwift { get; private set; }

    // Branding
    public byte[]? Logo { get; private set; }
    public string? LogoContentType { get; private set; }

    // Invoice Settings
    public string DefaultCurrency { get; private set; } = "USD";
    public int DefaultPaymentTermsDays { get; private set; } = 30;
    public string? InvoiceNotes { get; private set; }
    public string? InvoiceFooter { get; private set; }
    public string? InvoiceEmailSubject { get; private set; }
    public string? InvoiceEmailBody { get; private set; }

    // Payment Options
    public string? PayPalMeUsername { get; private set; }
    public string? WiseEmail { get; private set; }
    public string? RevolutUsername { get; private set; }
    public string? VietQrBankCode { get; private set; }

    // Cash position anchor for the cash-flow forecast. The forecast rolls this
    // forward using bank transactions imported after CashBalanceAsOf.
    public decimal? CashBalance { get; private set; }
    public DateOnly? CashBalanceAsOf { get; private set; }

    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = null!;
    public DateTime? ModifiedAt { get; set; }
    public string? ModifiedBy { get; set; }

    private BusinessProfile() { }

    public static BusinessProfile Create(
        string userId,
        string companyName,
        string email)
    {
        return new BusinessProfile
        {
            UserId = userId,
            CompanyName = companyName,
            Email = new EmailAddress(email)
        };
    }

    public void Update(
        string companyName,
        string? tradingName,
        string email,
        string? phone,
        string? website,
        Address? address,
        string? taxNumber,
        string? registrationNumber,
        string? bankName,
        string? bankAccountName,
        string? bankAccountNumber,
        string? bankSortCode,
        string? bankIban,
        string? bankSwift,
        string defaultCurrency,
        int defaultPaymentTermsDays,
        string? invoiceNotes,
        string? invoiceFooter,
        string? invoiceEmailSubject,
        string? invoiceEmailBody,
        string? payPalMeUsername,
        string? wiseEmail,
        string? revolutUsername,
        string? vietQrBankCode)
    {
        CompanyName = companyName;
        TradingName = tradingName;
        Email = new EmailAddress(email);
        Phone = string.IsNullOrWhiteSpace(phone) ? null : new PhoneNumber(phone);
        Website = website;
        Address = address;
        TaxNumber = taxNumber;
        RegistrationNumber = registrationNumber;
        BankName = bankName;
        BankAccountName = bankAccountName;
        BankAccountNumber = bankAccountNumber;
        BankSortCode = bankSortCode;
        BankIban = bankIban;
        BankSwift = bankSwift;
        DefaultCurrency = defaultCurrency;
        DefaultPaymentTermsDays = defaultPaymentTermsDays;
        InvoiceNotes = invoiceNotes;
        InvoiceFooter = invoiceFooter;
        InvoiceEmailSubject = invoiceEmailSubject;
        InvoiceEmailBody = invoiceEmailBody;
        PayPalMeUsername = payPalMeUsername;
        WiseEmail = wiseEmail;
        RevolutUsername = revolutUsername;
        VietQrBankCode = vietQrBankCode;
    }

    public void SetCashBalance(decimal balance, DateOnly asOf)
    {
        CashBalance = balance;
        CashBalanceAsOf = asOf;
    }

    public void UpdateLogo(byte[]? logo, string? contentType)
    {
        Logo = logo;
        LogoContentType = contentType;
    }

    /// <summary>
    /// Records the ISO country code for an address captured before codes were stored.
    /// Leaves every other part of the address, including the printed country, untouched.
    /// </summary>
    public void SetCountryCode(string countryCode)
    {
        if (Address is null) return;

        Address = new Address(
            Address.Street1,
            Address.City,
            Address.State,
            Address.PostalCode,
            Address.Country,
            Address.Street2,
            countryCode);
    }
}
