using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using Akinai.Infrastructure.Data;
using Akinai.Shared.Api.InvoiceTemplates;
using Akinai.Shared.Dtos.InvoiceTemplates;
using Akinai.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace Akinai.Infrastructure.Handlers.InvoiceTemplates;

public class GetInvoiceTemplateHandler(IDbContextFactory<ApplicationDbContext> dbFactory, ICurrentUserService currentUserService)
    : IHandle<GetInvoiceTemplate, InvoiceTemplateDto?>
{
    public async Task<HttpResult<InvoiceTemplateDto?>> Handle(
        GetInvoiceTemplate request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
            return HttpResult<InvoiceTemplateDto?>.Unauthorized();

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var template = await db.InvoiceTemplates
            .FirstOrDefaultAsync(t => t.Id == request.Id &&
                                     (t.IsSystem || t.UserId == userId), cancellationToken);

        if (template == null)
            return HttpResult<InvoiceTemplateDto?>.NotFound();

        var dto = new InvoiceTemplateDto(
            template.Id,
            template.Name,
            template.Description,
            template.IsDefault,
            template.IsSystem,
            template.TemplateType.ToString(),
            template.PrimaryColor,
            template.AccentColor,
            template.TextColor,
            template.BackgroundColor,
            template.ShowLogo,
            template.ShowPaymentQR,
            template.ShowBankDetails,
            template.ShowItemDescriptions,
            template.HeaderLayout,
            template.ItemsLayout,
            template.FooterLayout,
            template.FontFamily,
            template.CustomCss);

        return HttpResult<InvoiceTemplateDto?>.Ok(dto);
    }
}
