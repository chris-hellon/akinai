namespace InvoiceSoftware.Client.Services;

/// <summary>
/// Lightweight in-app event bus so the header time-tracker can tell open pages (e.g. Timesheet)
/// that a time entry was just logged, and they can refresh without a page reload. The tracker and
/// any listening page share the same circuit/instance, so no SignalR round-trip is needed.
/// </summary>
public sealed class TimeTrackingState
{
    public event Action? OnEntryLogged;

    public void NotifyEntryLogged() => OnEntryLogged?.Invoke();
}
