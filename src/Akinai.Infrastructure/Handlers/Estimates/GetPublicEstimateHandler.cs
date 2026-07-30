using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using Akinai.Infrastructure.Data;
using Akinai.Shared.Api.Estimates;
using Akinai.Shared.Dtos.Estimates;
using Microsoft.EntityFrameworkCore;

namespace Akinai.Infrastructure.Handlers.Estimates;

public class GetPublicEstimateHandler(
    IDbContextFactory<ApplicationDbContext> dbFactory,
    Akinai.Shared.Services.IInvoiceTranslator translator,
    Akinai.Shared.Services.IEntitlementService entitlements)
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

        // --- Translation --------------------------------------------------
        // Server-side, exactly as for invoices, so the estimate arrives in the
        // client's language whether they open the page or the PDF renderer
        // prints it. Fixed headings come from EstimateLabels in the renderer
        // using the Language field below — no API call, no cost.
        //
        // Line descriptions, notes and terms are what the user typed, so those
        // go through the translator. Terms in particular matter: an estimate a
        // client accepts is the basis of what they agreed to, and terms they
        // could not read are not terms they agreed to.
        var language = await translator.ResolveForInvoiceAsync(
            estimate.UserId, estimate.ClientId, ToShared(estimate.Language), cancellationToken);

        var notes = estimate.Notes;
        var terms = estimate.Terms;

        if (language != Akinai.Shared.Enums.SupportedLanguage.English)
        {
            var texts = new Dictionary<string, string>();
            for (var i = 0; i < lineItems.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(lineItems[i].Description))
                    texts[$"li:{i}"] = lineItems[i].Description;
            }
            if (!string.IsNullOrWhiteSpace(notes)) texts["notes"] = notes;
            if (!string.IsNullOrWhiteSpace(terms)) texts["terms"] = terms;

            if (texts.Count > 0)
            {
                // One call for the whole estimate, deduplicated by content
                // inside the translator. Anything untranslatable comes back as
                // the original, so this cannot fail the render.
                var translated = await translator.TranslateAsync(
                    estimate.UserId, language, texts, cancellationToken);

                for (var i = 0; i < lineItems.Count; i++)
                {
                    if (translated.TryGetValue($"li:{i}", out var description)
                        && !string.IsNullOrWhiteSpace(description))
                    {
                        lineItems[i] = lineItems[i] with { Description = description };
                    }
                }

                if (translated.TryGetValue("notes", out var n) && !string.IsNullOrWhiteSpace(n)) notes = n;
                if (translated.TryGetValue("terms", out var t) && !string.IsNullOrWhiteSpace(t)) terms = t;
            }
        }

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
            notes,
            terms,
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
            estimate.Template?.FooterLayout,
            language.ToString(),
            await Handlers.DocumentBranding.ShowForAsync(entitlements, estimate.UserId, cancellationToken));

        return HttpResult<PublicEstimateDto?>.Ok(dto);
    }

    /// <summary>Domain enum to shared enum; both declare the same members in the same order.</summary>
    private static Akinai.Shared.Enums.SupportedLanguage? ToShared(Domain.Enums.SupportedLanguage? language)
        => language is null ? null : (Akinai.Shared.Enums.SupportedLanguage)(int)language.Value;
}
