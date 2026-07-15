namespace InvoiceSoftware.Infrastructure.Banking;

/// <summary>Resolves the right bank-data provider by name (existing connections) or by country (new ones).</summary>
public interface IBankProviderRegistry
{
    /// <summary>Provider a connection was created with, for sync/finalize.</summary>
    IBankDataProvider? ByName(string name);

    /// <summary>Highest-priority configured provider that covers the country, for new connections.</summary>
    IBankDataProvider? ForCountry(string country);

    IReadOnlyList<IBankDataProvider> Configured { get; }
}

public class BankProviderRegistry(IEnumerable<IBankDataProvider> providers) : IBankProviderRegistry
{
    private readonly List<IBankDataProvider> _all = providers.ToList();

    public IReadOnlyList<IBankDataProvider> Configured => _all.Where(p => p.IsConfigured).ToList();

    public IBankDataProvider? ByName(string name)
        => _all.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));

    public IBankDataProvider? ForCountry(string country)
    {
        var c = (country ?? "").Trim().ToLowerInvariant();
        return _all
            .Where(p => p.IsConfigured && p.SupportedCountries.Contains(c))
            .OrderByDescending(p => p.Priority)
            .FirstOrDefault();
    }
}
