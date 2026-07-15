namespace InvoiceSoftware.Domain.Enums;

public enum BankConnectionStatus
{
    /// <summary>Link created; waiting for the user to authenticate at their bank.</summary>
    Pending = 0,
    /// <summary>Authenticated and pulling transactions.</summary>
    Linked = 1,
    /// <summary>Consent lapsed (PSD2 ~90-day expiry) — needs re-authentication.</summary>
    Expired = 2,
    /// <summary>Last sync failed.</summary>
    Error = 3
}
