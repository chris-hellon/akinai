using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using Akinai.Domain.Entities;
using Akinai.Domain.Enums;
using Akinai.Infrastructure.Data;
using Akinai.Shared.Api.InvoiceTemplates;
using Akinai.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace Akinai.Infrastructure.Handlers.InvoiceTemplates;

public class CreateInvoiceTemplateHandler(IDbContextFactory<ApplicationDbContext> dbFactory, ICurrentUserService currentUserService, IEntitlementService entitlementService)
    : IHandle<CreateInvoiceTemplate, Guid>
{
    public async Task<HttpResult<Guid>> Handle(
        CreateInvoiceTemplate request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
            return HttpResult<Guid>.Unauthorized();

        // Custom templates are a paid feature.
        var entitlements = await entitlementService.GetAsync(userId, cancellationToken);
        if (!entitlements.CustomTemplates) return HttpResult<Guid>.Forbidden();

        if (!Enum.TryParse<InvoiceTemplateType>(request.TemplateType, out var templateType))
            return HttpResult<Guid>.NotFound();

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        try
        {
            var template = InvoiceTemplate.Create(
                userId,
                request.Name,
                templateType,
                request.Description);

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

            db.InvoiceTemplates.Add(template);
            await db.SaveChangesAsync(cancellationToken);

            return HttpResult<Guid>.Created(template.Id);
        }
        catch (Domain.Exceptions.DomainException)
        {
            return HttpResult<Guid>.NotFound();
        }
    }
}
