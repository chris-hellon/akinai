namespace Akinai;

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
        "/api/plans",              // GetPlans — the pricing section, before signup

        // SendContactMessage — the contact form. The odd one out: it is a write,
        // and it is not token-authorised, because the people most likely to need
        // it are the ones who cannot sign in. It reads nothing and returns
        // nothing but success or failure; the handler rate-limits per IP and
        // carries a honeypot.
        "/api/contact"
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
