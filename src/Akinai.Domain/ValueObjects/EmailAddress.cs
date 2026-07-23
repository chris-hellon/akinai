using Akinai.Domain.Exceptions;

namespace Akinai.Domain.ValueObjects;

public record EmailAddress
{
    public string Value { get; }

    public EmailAddress(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException("Email address is required");
        if (!value.Contains('@'))
            throw new DomainException("Invalid email address format");

        Value = value.ToLowerInvariant();
    }

    public override string ToString() => Value;
}
