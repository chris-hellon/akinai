using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using Akinai.Infrastructure.Data;
using Akinai.Shared.Api.InvoiceTemplates;
using Akinai.Shared.Dtos.InvoiceTemplates;
using Akinai.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace Akinai.Infrastructure.Handlers.InvoiceTemplates;

public class GetInvoiceTemplatesHandler(IDbContextFactory<ApplicationDbContext> dbFactory, ICurrentUserService currentUserService)
    : IHandle<GetInvoiceTemplates, List<InvoiceTemplateDto>>
{
    public async Task<HttpResult<List<InvoiceTemplateDto>>> Handle(
        GetInvoiceTemplates request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
            return HttpResult<List<InvoiceTemplateDto>>.Unauthorized();

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var templates = await db.InvoiceTemplates
            .Where(t => t.IsSystem || t.UserId == userId)
            .OrderBy(t => t.IsSystem ? 0 : 1)
            .ThenBy(t => t.Name)
            .ToListAsync(cancellationToken);

        var items = templates.Select(t => new InvoiceTemplateDto(
            t.Id,
            t.Name,
            t.Description,
            t.IsDefault,
            t.IsSystem,
            t.TemplateType.ToString(),
            t.PrimaryColor,
            t.AccentColor,
            t.TextColor,
            t.BackgroundColor,
            t.ShowLogo,
            t.ShowPaymentQR,
            t.ShowBankDetails,
            t.ShowItemDescriptions,
            t.HeaderLayout,
            t.ItemsLayout,
            t.FooterLayout,
            t.FontFamily,
            t.CustomCss)).ToList();

        return HttpResult<List<InvoiceTemplateDto>>.Ok(items);
    }
}
