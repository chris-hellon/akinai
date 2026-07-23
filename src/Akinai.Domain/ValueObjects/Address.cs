namespace Akinai.Domain.ValueObjects;

public record Address
{
    public string Street1 { get; }
    public string? Street2 { get; }
    public string City { get; }
    public string State { get; }
    public string PostalCode { get; }
    public string Country { get; }

    public Address(
        string street1,
        string city,
        string state,
        string postalCode,
        string country,
        string? street2 = null)
    {
        Street1 = street1;
        Street2 = street2;
        City = city;
        State = state;
        PostalCode = postalCode;
        Country = country;
    }

    public override string ToString()
    {
        var street = Street2 != null ? $"{Street1}, {Street2}" : Street1;
        return $"{street}, {City}, {State} {PostalCode}, {Country}";
    }
}
