namespace Akinai.Domain.ValueObjects;

public record PhoneNumber
{
    public string Value { get; }

    public PhoneNumber(string value)
    {
        Value = value != null ? value.Trim() : string.Empty;
    }

    public override string ToString() => Value;
}
