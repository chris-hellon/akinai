using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using Akinai.Infrastructure.Data;
using Akinai.Shared.Api.Estimates;
using Akinai.Shared.Dtos.Estimates;
using Akinai.Shared.Dtos.InvoiceTemplates;
using Akinai.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace Akinai.Infrastructure.Handlers.Estimates;

public class GetEstimateHandler(IDbContextFactory<ApplicationDbContext> dbFactory, ICurrentUserService currentUserService)
    : IHandle<GetEstimate, EstimateDetailDto?>
{
    public async Task<HttpResult<EstimateDetailDto?>> Handle(GetEstimate request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
            return HttpResult<EstimateDetailDto?>.Unauthorized();

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var estimate = await db.Estimates
            .Include(e => e.Client)
            .Include(e => e.LineItems.OrderBy(li => li.Order))
            .Include(e => e.Template)
            .FirstOrDefaultAsync(e => e.Id == request.Id && e.UserId == userId, cancellationToken);

        if (estimate == null)
            return HttpResult<EstimateDetailDto?>.NotFound();

        var lineItems = estimate.LineItems.Select(li => new EstimateLineItemDto(
            li.Id,
            li.ProductId,
            li.Description,
            li.Quantity,
            li.UnitPrice.Amount,
            li.UnitPrice.Currency,
            li.Order,
            li.LineTotal.Amount)).ToList();

        InvoiceTemplateDto? templateDto = null;
        if (estimate.Template != null)
        {
            templateDto = new InvoiceTemplateDto(
                estimate.Template.Id,
                estimate.Template.Name,
                estimate.Template.Description,
                estimate.Template.IsDefault,
                estimate.Template.IsSystem,
                estimate.Template.TemplateType.ToString(),
                estimate.Template.PrimaryColor,
                estimate.Template.AccentColor,
                estimate.Template.TextColor,
                estimate.Template.BackgroundColor,
                estimate.Template.ShowLogo,
                estimate.Template.ShowPaymentQR,
                estimate.Template.ShowBankDetails,
                estimate.Template.ShowItemDescriptions,
                estimate.Template.HeaderLayout,
                estimate.Template.ItemsLayout,
                estimate.Template.FooterLayout,
                estimate.Template.FontFamily,
                estimate.Template.CustomCss);
        }

        var dto = new EstimateDetailDto(
            estimate.Id,
            estimate.EstimateNumber,
            estimate.ClientId,
            estimate.Client.Name,
            estimate.Client.CompanyName,
            estimate.Client.Email.Value,
            estimate.Client.Phone?.Value,
            estimate.Client.BillingAddress?.Street1,
            estimate.Client.BillingAddress?.City,
            estimate.Client.BillingAddress?.State,
            estimate.Client.BillingAddress?.PostalCode,
            estimate.Client.BillingAddress?.Country,
            estimate.Currency,
            estimate.EstimateDate,
            estimate.ExpiryDate,
            estimate.ValidDays,
            estimate.Status.ToString(),
            estimate.TaxRate,
            estimate.Notes,
            estimate.Terms,
            estimate.PublicAccessToken,
            estimate.ConvertedInvoiceId,
            estimate.AcceptedDate,
            estimate.RejectedDate,
            lineItems,
            estimate.CalculateSubtotal().Amount,
            estimate.CalculateTax().Amount,
            estimate.CalculateTotal().Amount,
            estimate.CreatedAt,
            estimate.ModifiedAt,
            estimate.TemplateId,
            templateDto,
            estimate.Language?.ToString());

        return HttpResult<EstimateDetailDto?>.Ok(dto);
    }
}
