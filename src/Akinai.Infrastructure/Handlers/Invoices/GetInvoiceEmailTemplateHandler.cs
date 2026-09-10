using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using Akinai.Domain.Entities;
using Akinai.Infrastructure.Data;
using Akinai.Infrastructure.Services;
using Akinai.Shared.Api.Invoices;
using Akinai.Shared.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Akinai.Infrastructure.Handlers.Invoices;

public class GetInvoiceEmailTemplateHandler(
    IDbContextFactory<ApplicationDbContext> dbFactory,
    ICurrentUserService currentUserService,
    EmailTemplateService emailTemplateService,
    IHttpContextAccessor httpContextAccessor)
    : IHandle<GetInvoiceEmailTemplate, string?>
{
    public async Task<HttpResult<string?>> Handle(GetInvoiceEmailTemplate request, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var invoice = await db.Invoices
            .Include(i => i.Client)
            .FirstOrDefaultAsync(i => i.Id == request.Id, cancellationToken);

        if (invoice is null)
            return HttpResult<string?>.NotFound();

        // The snapshot total. The previous local calculation summed only time entries
        // and expenses, so the amount previewed here understated any invoice carrying
        // product line items.
        var total = invoice.Total;

        // Get company name and email template from business profile
        var userId = currentUserService.UserId;
        var companyName = "Your Company";
        string? emailBodyTemplate = null;

        if (!string.IsNullOrEmpty(userId))
        {
            var profile = await db.BusinessProfiles
                .FirstOrDefaultAsync(b => b.UserId == userId, cancellationToken);
            if (profile != null)
            {
                companyName = (string.IsNullOrWhiteSpace(profile.TradingName) ? profile.CompanyName : profile.TradingName) ?? companyName;
                emailBodyTemplate = profile.InvoiceEmailBody;
            }
        }

        // Build public view URL if token exists
        string? publicViewUrl = null;
        if (invoice.PublicAccessToken.HasValue && httpContextAccessor.HttpContext != null)
        {
            var request2 = httpContextAccessor.HttpContext.Request;
            var baseUrl = $"{request2.Scheme}://{request2.Host}";
            publicViewUrl = $"{baseUrl}/invoice/view/{invoice.PublicAccessToken}";
        }

        var html = emailTemplateService.GenerateInvoiceEmailHtml(invoice, total, companyName, emailBodyTemplate, publicViewUrl);

        return HttpResult<string?>.Ok(html);
    }

}
