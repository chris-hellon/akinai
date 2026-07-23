namespace InvoiceSoftware;

/// <summary>
/// The only /api routes reachable without signing in.
///
/// Each of these is authorised by an unguessable token in the URL and is used
/// by people who have no account — a client opening an invoice or estimate
/// link you emailed them, or their client portal. Everything else under /api
/// requires an authenticated user (see the gate in Program.cs).
///
/// Keep this list as small as possible. Adding an entry makes that route
/// world-readable to anyone who can guess or obtain the token.
/// </summary>
public static class ApiPublicPaths
{
    private static readonly string[] Prefixes =
    [
        "/api/invoices/public/",   // GetPublicInvoice          — emailed invoice link
        "/api/estimates/public/",  // GetPublicEstimate, Accept/RejectEstimatePublic
        "/api/portal/",            // GetClientPortal* — the client portal
        "/api/plans"               // GetPlans — the pricing section, before signup
    ];

    public static bool IsPublic(PathString path)
    {
        var value = path.Value;
        if (string.IsNullOrEmpty(value)) return false;

        foreach (var prefix in Prefixes)
        {
            if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
