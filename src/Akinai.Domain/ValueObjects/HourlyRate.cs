using Akinai.Domain.Exceptions;

namespace Akinai.Domain.ValueObjects;

public record HourlyRate
{
    public decimal Value { get; }

    public HourlyRate(decimal value)
    {
        if (value < 0)
            throw new DomainException("Hourly rate cannot be negative");

        Value = Math.Round(value, 2);
    }

    public Money CalculateFor(Hours hours, string currency = "USD")
        => new(Value * hours.Value, currency);

    public override string ToString() => $"${Value:N2}/hr";
}
