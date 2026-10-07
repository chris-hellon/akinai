using Akinai.Domain.Common;
using Akinai.Domain.ValueObjects;

namespace Akinai.Domain.Entities;

public class Client : AggregateRoot, IAuditableEntity
{
    public string UserId { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public string? CompanyName { get; private set; }
    public EmailAddress Email { get; private set; } = null!;
    public PhoneNumber? Phone { get; private set; }
    public Address? BillingAddress { get; private set; }
    public HourlyRate DefaultHourlyRate { get; private set; } = null!;
    public string Currency { get; private set; } = "GBP";
    /// <summary>
    /// The client's VAT/tax registration number. Required on the document for an EU
    /// cross-border B2B supply — without it the invoice is not valid and the client
    /// cannot reclaim.
    /// </summary>
    public string? TaxNumber { get; private set; }

    /// <summary>
    /// Whether this client is a business rather than a consumer. Drives the tax treatment:
    /// the same cross-border invoice is reverse charge to a business and ordinary VAT to a
    /// consumer. Defaults to true, which is what every existing client was.
    /// </summary>
    public bool IsBusiness { get; private set; } = true;

    public string? Notes { get; private set; }
    public bool IsActive { get; private set; } = true;
    public Guid? PortalToken { get; private set; }

    private readonly List<Project> _projects = [];
    public IReadOnlyCollection<Project> Projects => _projects.AsReadOnly();

    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = null!;
    public DateTime? ModifiedAt { get; set; }
    public string? ModifiedBy { get; set; }

    private Client() { }

    public static Client Create(
        string userId,
        string name,
        string email,
        decimal defaultHourlyRate,
        string currency = "GBP",
        string? companyName = null,
        string? phone = null,
        Address? billingAddress = null,
        string? notes = null,
        string? taxNumber = null,
        bool isBusiness = true)
    {
        return new Client
        {
            TaxNumber = taxNumber,
            IsBusiness = isBusiness,
            UserId = userId,
            Name = name,
            CompanyName = companyName,
            Email = new EmailAddress(email),
            Phone = phone != null ? new PhoneNumber(phone) : null,
            DefaultHourlyRate = new HourlyRate(defaultHourlyRate),
            Currency = currency,
            BillingAddress = billingAddress,
            Notes = notes,
            PortalToken = Guid.NewGuid()
        };
    }

    public void Update(
        string name,
        string email,
        decimal defaultHourlyRate,
        string currency = "GBP",
        string? companyName = null,
        string? phone = null,
        Address? billingAddress = null,
        string? notes = null,
        string? taxNumber = null,
        bool isBusiness = true)
    {
        Name = name;
        TaxNumber = taxNumber;
        IsBusiness = isBusiness;
        Email = new EmailAddress(email);
        DefaultHourlyRate = new HourlyRate(defaultHourlyRate);
        Currency = currency;
        CompanyName = companyName;
        Phone = phone != null ? new PhoneNumber(phone) : null;
        BillingAddress = billingAddress;
        Notes = notes;
    }

    public void Deactivate() => IsActive = false;
    public void Activate() => IsActive = true;
    public void RegeneratePortalToken() => PortalToken = Guid.NewGuid();

    public Project AddProject(
        string name,
        string? description = null,
        DateOnly? startDate = null,
        DateOnly? endDate = null)
    {
        var project = Project.Create(Id, name, description, startDate, endDate);
        _projects.Add(project);
        return project;
    }

    /// <summary>
    /// Records the ISO country code for an address captured before codes were stored.
    /// Leaves every other part of the address, including the printed country, untouched.
    /// </summary>
    public void SetBillingCountryCode(string countryCode)
    {
        if (BillingAddress is null) return;

        BillingAddress = new Address(
            BillingAddress.Street1,
            BillingAddress.City,
            BillingAddress.State,
            BillingAddress.PostalCode,
            BillingAddress.Country,
            BillingAddress.Street2,
            countryCode);
    }
}
