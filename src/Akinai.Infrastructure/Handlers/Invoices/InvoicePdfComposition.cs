using Akinai.Infrastructure.Services.Pdf;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Akinai.Infrastructure.Handlers.Invoices;

/// <summary>
/// Produces the PDF for an invoice: print the page, and only fall back to rebuilding it.
///
/// There are two renderers in this codebase, and that is a liability rather than a
/// feature. The invoice already exists as HTML at <c>/invoice/print/{token}</c> — the same
/// component, data and translation the client sees when they open the link — so printing
/// it is what keeps the document the client reads and the document they are emailed from
/// drifting apart. The QuestPDF generator is a parallel implementation of the same
/// document and has to be kept in step by hand; it earned its place only as a safety net
/// for a missing or un-launchable Chromium, where an English-only PDF beats an error.
///
/// Both paths go through here so neither can quietly diverge again. They already did once:
/// a corridor-required tax statement was added to the print page and the emailed PDF went
/// out without it, because the send path never touched the renderer the download path used.
/// </summary>
public static class InvoicePdfComposition
{
    /// <param name="fallback">
    /// Rebuilds the document from scratch. Invoked only when the page could not be printed.
    /// </param>
    public static async Task<byte[]> RenderAsync(
        Guid invoiceId,
        Guid? publicAccessToken,
        IHtmlPdfRenderer htmlRenderer,
        IHttpContextAccessor httpContextAccessor,
        ILogger logger,
        Func<Task<byte[]>> fallback,
        CancellationToken cancellationToken)
    {
        if (publicAccessToken is Guid token)
        {
            var request = httpContextAccessor.HttpContext?.Request;
            if (request is not null)
            {
                // Loopback, so the render never leaves the machine and no reverse proxy or
                // auth layer sits in the way.
                var origin = $"{request.Scheme}://127.0.0.1:{request.Host.Port ?? (request.IsHttps ? 443 : 80)}";
                var rendered = await htmlRenderer.RenderAsync($"{origin}/invoice/print/{token}", cancellationToken);
                if (rendered is { Length: > 0 })
                    return rendered;
            }
        }

        // Logged as a warning, not swallowed: the fallback is a safety net, and one that
        // fires on every invoice — as a TLS or launch failure would — is a bug hiding
        // behind an old-looking PDF, which is exactly how this went unnoticed once.
        logger.LogWarning(
            "Invoice {InvoiceId}: HTML PDF render produced nothing (token set: {HasToken}); using the QuestPDF fallback.",
            invoiceId, publicAccessToken is not null);

        return await fallback();
    }
}
