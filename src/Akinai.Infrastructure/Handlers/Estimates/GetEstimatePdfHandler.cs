using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using Akinai.Infrastructure.Data;
using Akinai.Infrastructure.Services;
using Akinai.Shared.Api.Estimates;
using Akinai.Shared.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Akinai.Infrastructure.Handlers.Estimates;

public class GetEstimatePdfHandler(
    IDbContextFactory<ApplicationDbContext> dbFactory,
    ICurrentUserService currentUserService,
    PdfGeneratorService pdfGenerator,
    Services.Pdf.IHtmlPdfRenderer htmlRenderer,
    IHttpContextAccessor httpContextAccessor)
    : IHandle<GetEstimatePdf, byte[]?>
{
    public async Task<HttpResult<byte[]?>> Handle(GetEstimatePdf request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
            return HttpResult<byte[]?>.Unauthorized();

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var estimate = await db.Estimates
            .Include(e => e.Client)
            .Include(e => e.LineItems.OrderBy(li => li.Order))
            .Include(e => e.Template)
            .FirstOrDefaultAsync(e => e.Id == request.Id && e.UserId == userId, cancellationToken);

        if (estimate == null)
            return HttpResult<byte[]?>.NotFound();

        // --- Print the page, don't rebuild it -----------------------------
        // The estimate already exists as HTML at /estimate/print/{token}: same
        // component, same data, same translation as the link the client opens.
        // Printing that is what stops the two from disagreeing — and it is how
        // the PDF gets translated at all, since the QuestPDF path below has no
        // notion of language.
        if (estimate.PublicAccessToken is Guid token)
        {
            var httpRequest = httpContextAccessor.HttpContext?.Request;
            if (httpRequest is not null)
            {
                // Loopback, so the render never leaves the machine and no
                // reverse proxy or auth layer sits in the way.
                var origin = $"{httpRequest.Scheme}://127.0.0.1:{httpRequest.Host.Port ?? (httpRequest.IsHttps ? 443 : 80)}";
                var rendered = await htmlRenderer.RenderAsync($"{origin}/estimate/print/{token}", cancellationToken);
                if (rendered is { Length: > 0 })
                    return HttpResult<byte[]?>.Ok(rendered);
            }
        }

        // Fallback: the original QuestPDF generator. Kept for the same reason as
        // the invoice's — a missing or un-launchable Chromium must not stop
        // someone downloading an estimate. This one is English-only, so a
        // translated estimate silently reverts to English here rather than
        // failing; that is the intended trade.
        var businessProfile = await db.BusinessProfiles
            .FirstOrDefaultAsync(b => b.UserId == userId, cancellationToken);

        var pdfBytes = await pdfGenerator.GenerateEstimatePdfAsync(estimate, businessProfile);
        return HttpResult<byte[]?>.Ok(pdfBytes);
    }
}
