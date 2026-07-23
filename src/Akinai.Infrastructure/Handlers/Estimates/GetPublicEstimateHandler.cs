using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using Akinai.Infrastructure.Data;
using Akinai.Shared.Api.Estimates;
using Akinai.Shared.Dtos.Estimates;
using Microsoft.EntityFrameworkCore;

namespace Akinai.Infrastructure.Handlers.Estimates;

public class GetPublicEstimateHandler(IDbContextFactory<ApplicationDbContext> dbFactory)
    : IHandle<GetPublicEstimate, PublicEstimateDto?>
{
    public async Task<HttpResult<PublicEstimateDto?>> Handle(GetPublicEstimate request, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var estimate = await db.Estimates
            .Include(e => e.Client)
            .Include(e => e.LineItems.OrderBy(li => li.Order))
            .Include(e => e.Template)
            .FirstOrDefaultAsync(e => e.PublicAccessToken == request.Token, cancellationToken);

        if (estimate == null)
            return HttpResult<PublicEstimateDto?>.Ok(null);

        // Get business profile for the estimate owner
        var businessProfile = await db.BusinessProfiles
            .FirstOrDefaultAsync(b => b.UserId == estimate.UserId, cancellationToken);

        var lineItems = estimate.LineItems.Select(li => new EstimateLineItemDto(
            li.Id,
            li.ProductId,
            li.Description,
            li.Quantity,
            li.UnitPrice.Amount,
            li.UnitPrice.Currency,
            li.Order,
            li.LineTotal.Amount)).ToList();

        // Build client address
        string? clientAddress = null;
        if (estimate.Client.BillingAddress != null)
        {
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(estimate.Client.BillingAddress.Street1))
                parts.Add(estimate.Client.BillingAddress.Street1);
            var cityLine = string.Join(", ", new[] {
                estimate.Client.BillingAddress.City,
                estimate.Client.BillingAddress.State,
                estimate.Client.BillingAddress.PostalCode
            }.Where(s => !string.IsNullOrEmpty(s)));
            if (!string.IsNullOrEmpty(cityLine)) parts.Add(cityLine);
            if (!string.IsNullOrEmpty(estimate.Client.BillingAddress.Country))
                parts.Add(estimate.Client.BillingAddress.Country);
            clientAddress = string.Join("\n", parts);
        }

        var dto = new PublicEstimateDto(
            estimate.Id,
            estimate.EstimateNumber,
            estimate.Client.Name,
            estimate.Client.CompanyName,
            clientAddress,
            estimate.Client.Email.Value,
            estimate.Currency,
            estimate.EstimateDate,
            estimate.ExpiryDate,
            estimate.AcceptedDate,
            estimate.RejectedDate,
            estimate.Status.ToString(),
            estimate.Notes,
            estimate.Terms,
            lineItems,
            estimate.CalculateSubtotal().Amount,
            estimate.TaxRate,
            estimate.CalculateTax().Amount,
            estimate.CalculateTotal().Amount,
            string.IsNullOrWhiteSpace(businessProfile?.TradingName) ? businessProfile?.CompanyName : businessProfile.TradingName,
            businessProfile?.Email?.Value,
            businessProfile?.Phone?.Value,
            businessProfile?.Address?.Street1,
            businessProfile?.Address?.City,
            businessProfile?.Address?.State,
            businessProfile?.Address?.PostalCode,
            businessProfile?.Address?.Country,
            businessProfile?.TaxNumber,
            businessProfile?.RegistrationNumber,
            businessProfile?.Logo,
            businessProfile?.LogoContentType,
            // Template properties
            estimate.Template?.PrimaryColor,
            estimate.Template?.AccentColor,
            estimate.Template?.ShowLogo ?? true,
            estimate.Template?.HeaderLayout,
            estimate.Template?.ItemsLayout,
            estimate.Template?.FooterLayout);

        return HttpResult<PublicEstimateDto?>.Ok(dto);
    }
}
