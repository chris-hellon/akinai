namespace Akinai.Domain.ValueObjects;

public record Address
{
    public string Street1 { get; }
    public string? Street2 { get; }
    public string City { get; }
    public string State { get; }
    public string PostalCode { get; }
    public string Country { get; }

    /// <summary>
    /// ISO-3166 alpha-2, when it is known. Nullable and kept alongside <see cref="Country"/>
    /// rather than replacing it: the free-text country is what addresses were captured as
    /// before this existed, it is still what gets printed, and converting it in place would
    /// have meant guessing at values that drive tax treatment. Null means "not yet
    /// identified", which the compliance rules report rather than assume around.
    /// </summary>
    public string? CountryCode { get; }

    public Address(
        string street1,
        string city,
        string state,
        string postalCode,
        string country,
        string? street2 = null,
        string? countryCode = null)
    {
        Street1 = street1;
        Street2 = street2;
        City = city;
        State = state;
        PostalCode = postalCode;
        Country = country;
        CountryCode = countryCode;
    }

    public override string ToString()
    {
        var street = Street2 != null ? $"{Street1}, {Street2}" : Street1;
        return $"{street}, {City}, {State} {PostalCode}, {Country}";
    }
}
