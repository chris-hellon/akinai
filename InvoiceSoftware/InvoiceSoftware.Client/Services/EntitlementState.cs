using BlazorUtils.EasyApi;
using InvoiceSoftware.Shared.Api.Billing;
using InvoiceSoftware.Shared.Dtos.Billing;

namespace InvoiceSoftware.Client.Services;

/// <summary>
/// The signed-in account's entitlements, loaded once and shared by every page
/// that needs to know whether to offer a paid feature.
///
/// This exists so the UI can *offer* the right things — it is not the gate.
/// Every paid feature is enforced server-side in its handler; hiding a button
/// has never stopped a crafted request. What this prevents is the other
/// failure: firing a request that the server will refuse and showing the user a
/// raw status code instead of an explanation.
/// </summary>
public sealed class EntitlementState(ICall<GetEntitlements, EntitlementsDto> getEntitlements)
{
    /// <summary>
    /// How long a cached answer is trusted.
    ///
    /// The SignalR push below is the fast path, but it only reaches a client
    /// that holds a hub connection — which today means WebAssembly. Under
    /// Interactive Auto a page may be running on Blazor Server instead, where
    /// no such connection exists, so a plan changed in another tab would
    /// otherwise never arrive. This bounds how long that can be wrong, without
    /// re-fetching on every render.
    /// </summary>
    private static readonly TimeSpan CacheFor = TimeSpan.FromSeconds(30);

    private DateTimeOffset _loadedAtUtc = DateTimeOffset.MinValue;

    public EntitlementsDto? Current { get; private set; }

    public event Action? OnChange;

    /// <summary>
    /// Loads if we have no answer, or the one we have has gone stale. Safe and
    /// cheap to call from every page.
    /// </summary>
    public async Task EnsureLoadedAsync()
    {
        if (Current is not null && DateTimeOffset.UtcNow - _loadedAtUtc < CacheFor) return;
        await ReloadAsync();
    }

    /// <summary>Call after anything that can change the plan, e.g. checkout.</summary>
    public async Task ReloadAsync()
    {
        try
        {
            Current = await getEntitlements.Call(new GetEntitlements());
            _loadedAtUtc = DateTimeOffset.UtcNow;
        }
        catch
        {
            // Billing must never be the reason the app is unusable. With no
            // answer we leave Current null, and the gates below fail *open* —
            // the server still refuses anything that isn't allowed.
            Current = null;
        }
        OnChange?.Invoke();
    }

    /// <summary>
    /// True when the feature should be offered. Unknown entitlements return
    /// true deliberately: the UI is a courtesy, and a page that hid everything
    /// because a billing call failed would be worse than one that lets the
    /// server say no.
    /// </summary>
    public bool Allows(Func<EntitlementsDto, bool> feature)
        => Current is null || feature(Current);

    public bool CanCreateInvoice => Current is null || Current.CanCreateInvoice;
}
