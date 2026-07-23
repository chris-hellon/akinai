using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using Akinai.Domain.Enums;
using Akinai.Infrastructure.Data;
using Akinai.Shared.Api.InvoiceTemplates;
using Akinai.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace Akinai.Infrastructure.Handlers.InvoiceTemplates;

public class UpdateInvoiceTemplateHandler(IDbContextFactory<ApplicationDbContext> dbFactory, ICurrentUserService currentUserService)
    : IHandle<UpdateInvoiceTemplate>
{
    public async Task<HttpResult> Handle(
        UpdateInvoiceTemplate request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
            return HttpResult.Unauthorized();

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var template = await db.InvoiceTemplates
            .FirstOrDefaultAsync(t => t.Id == request.Id && t.UserId == userId, cancellationToken);

        if (template == null)
            return HttpResult.NotFound();

        if (template.IsSystem)
            return HttpResult.NotFound();

        if (!Enum.TryParse<InvoiceTemplateType>(request.TemplateType, out var templateType))
            return HttpResult.NotFound();

        try
        {
            template.Update(request.Name, request.Description, templateType);

            template.UpdateColors(
                request.PrimaryColor,
                request.AccentColor,
                request.TextColor,
                request.BackgroundColor);

            template.UpdateDisplayOptions(
                request.ShowLogo,
                request.ShowPaymentQR,
                request.ShowBankDetails,
                request.ShowItemDescriptions);

            template.UpdateLayouts(
                request.HeaderLayout,
                request.ItemsLayout,
                request.FooterLayout);

            template.UpdateCustomization(request.FontFamily, null);

            await db.SaveChangesAsync(cancellationToken);
            return HttpResult.Ok();
        }
        catch (Domain.Exceptions.DomainException)
        {
            return HttpResult.NotFound();
        }
    }
}
