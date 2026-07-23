using BlazorUtils.EasyApi;
using Akinai.Shared.Api.BusinessProfile;
using Akinai.Shared.Api.Dashboard;
using Akinai.Shared.Dtos.BusinessProfile;
using Microsoft.JSInterop;

namespace Akinai.Client.Services;

/// <summary>
/// The dashboard's chosen reporting currency, shared between the header selector and the
/// dashboard. Defaults to the business default currency; the choice persists in localStorage.
/// </summary>
public sealed class ReportingCurrencyState(
    ICall<GetReportingCurrencies, List<string>> getCurrencies,
    ICall<GetBusinessProfile, BusinessProfileDto?> getProfile,
    IJSRuntime js)
{
    private const string StorageKey = "reportingCurrency";
    private bool _initialized;

    public string Currency { get; private set; } = "USD";
    public List<string> Available { get; private set; } = new() { "USD" };

    public event Action? OnChange;

    public async Task InitializeAsync()
    {
        if (_initialized) return;
        _initialized = true;

        try
        {
            var list = await getCurrencies.Call(new GetReportingCurrencies());
            if (list is { Count: > 0 })
                Available = list;

            string? saved = null;
            try { saved = await js.InvokeAsync<string?>("localStorage.getItem", StorageKey); } catch { }

            if (!string.IsNullOrWhiteSpace(saved) &&
                Available.Contains(saved, StringComparer.OrdinalIgnoreCase))
            {
                Currency = Available.First(c => string.Equals(c, saved, StringComparison.OrdinalIgnoreCase));
            }
            else
            {
                var profile = await getProfile.Call(new GetBusinessProfile());
                var def = profile?.DefaultCurrency;
                Currency = !string.IsNullOrWhiteSpace(def) && Available.Contains(def, StringComparer.OrdinalIgnoreCase)
                    ? Available.First(c => string.Equals(c, def, StringComparison.OrdinalIgnoreCase))
                    : Available[0];
            }

            OnChange?.Invoke();
        }
        catch
        {
            // Keep the default.
        }
    }

    public async Task SetAsync(string currency)
    {
        if (string.IsNullOrWhiteSpace(currency) || string.Equals(currency, Currency, StringComparison.OrdinalIgnoreCase))
            return;

        Currency = currency;
        try { await js.InvokeVoidAsync("localStorage.setItem", StorageKey, currency); } catch { }
        OnChange?.Invoke();
    }
}
