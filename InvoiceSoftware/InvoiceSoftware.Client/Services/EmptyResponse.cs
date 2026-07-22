namespace InvoiceSoftware.Client.Services;

/// <summary>
/// Several "singleton" endpoints (business profile, currency payment settings,
/// business logo …) return <c>HttpResult&lt;T?&gt;.Ok(null)</c> when the user
/// hasn't created that record yet. EasyApi cannot represent a 200 with a null
/// body — it throws with a message like "[200 OK] with missing Response".
///
/// That is an empty state, not a failure, so callers use this to tell the two
/// apart instead of showing a scary error to someone who just signed up.
/// </summary>
public static class EmptyResponse
{
    public static bool IsEmptyOk(Exception ex) =>
        ex.Message.Contains("[200", StringComparison.Ordinal);
}
