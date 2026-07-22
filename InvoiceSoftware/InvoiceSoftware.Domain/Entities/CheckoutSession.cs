using InvoiceSoftware.Domain.Common;
using InvoiceSoftware.Domain.Exceptions;

namespace InvoiceSoftware.Domain.Entities;

/// <summary>
/// A checkout in progress.
///
/// The price is fixed here, server-side, when the session is created — so
/// completing a checkout never takes an amount or a plan from the request. That
/// matters less while payment is simulated and matters entirely once it is not,
/// which is exactly why the shape is the same either way.
///
/// Sessions expire, and each can only be completed once.
/// </summary>
public class CheckoutSession : AggregateRoot
{
    public string UserId { get; private set; } = null!;
    public string Plan { get; private set; } = null!;
    public string Cycle { get; private set; } = null!;
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = null!;

    /// <summary>True when this session is not backed by a real payment provider.</summary>
    public bool Simulated { get; private set; }

    /// <summary>Set by the provider once it exists; null while simulated.</summary>
    public string? ProviderSessionId { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }

    public bool IsCompleted => CompletedAtUtc is not null;
    public bool IsExpired(DateTime utcNow) => utcNow > ExpiresAtUtc;

    private CheckoutSession() { }

    public static CheckoutSession Create(
        string userId,
        string plan,
        string cycle,
        decimal amount,
        string currency,
        bool simulated,
        TimeSpan validFor)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new DomainException("A checkout session requires a user");
        if (amount < 0)
            throw new DomainException("A checkout session cannot have a negative amount");

        var now = DateTime.UtcNow;
        return new CheckoutSession
        {
            UserId = userId,
            Plan = plan,
            Cycle = cycle,
            Amount = amount,
            Currency = currency,
            Simulated = simulated,
            CreatedAtUtc = now,
            ExpiresAtUtc = now.Add(validFor),
        };
    }

    /// <summary>Marks the session used. Completing twice is a domain error, not a no-op.</summary>
    public void Complete()
    {
        if (IsCompleted)
            throw new DomainException("This checkout session has already been completed");

        CompletedAtUtc = DateTime.UtcNow;
    }

    public void SetProviderSessionId(string providerSessionId) => ProviderSessionId = providerSessionId;
}
