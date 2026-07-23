using Akinai.Domain.Exceptions;

namespace Akinai.Domain.ValueObjects;

public record Hours
{
    public decimal Value { get; }

    public Hours(decimal value)
    {
        if (value < 0)
            throw new DomainException("Hours cannot be negative");

        Value = Math.Round(value, 2);
    }

    public static Hours Zero => new(0);

    public static Hours operator +(Hours a, Hours b) => new(a.Value + b.Value);
    public static Hours operator -(Hours a, Hours b) => new(a.Value - b.Value);

    public override string ToString() => $"{Value:N2}h";
}
