using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using InvoiceSoftware.Domain.Entities;
using InvoiceSoftware.Domain.Enums;
using InvoiceSoftware.Infrastructure.Data;
using InvoiceSoftware.Shared.Api.Localization;
using InvoiceSoftware.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace InvoiceSoftware.Infrastructure.Handlers.Localization;

public class UpdateUserLanguageSettingsHandler(
    IDbContextFactory<ApplicationDbContext> dbFactory,
    ICurrentUserService currentUserService,
    IEntitlementService entitlementService)
    : IHandle<UpdateUserLanguageSettings>
{
    public async Task<HttpResult> Handle(
        UpdateUserLanguageSettings request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
            return HttpResult.Unauthorized();

        if (!Enum.TryParse<SupportedLanguage>(request.DefaultLanguage, out var defaultLang))
            return HttpResult.NotFound();

        if (!Enum.TryParse<SupportedLanguage>(request.InvoiceLanguage, out var invoiceLang))
            return HttpResult.NotFound();

        if (!Enum.TryParse<SupportedLanguage>(request.EstimateLanguage, out var estimateLang))
            return HttpResult.NotFound();

        // Other languages are a paid feature. Checked across all three settings,
        // because any one of them is enough to put a non-English invoice in
        // front of a client.
        var entitlements = await entitlementService.GetAsync(userId, cancellationToken);
        if (!entitlements.AllLanguages
            && (defaultLang != SupportedLanguage.English
                || invoiceLang != SupportedLanguage.English
                || estimateLang != SupportedLanguage.English))
        {
            return HttpResult.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var settings = await db.UserLanguagePreferences
            .FirstOrDefaultAsync(s => s.UserId == userId, cancellationToken);

        if (settings == null)
        {
            settings = UserLanguagePreference.Create(userId);
            db.UserLanguagePreferences.Add(settings);
        }

        settings.UpdateDefaultLanguage(defaultLang);
        settings.UpdateInvoiceLanguage(invoiceLang);
        settings.UpdateEstimateLanguage(estimateLang);

        await db.SaveChangesAsync(cancellationToken);
        return HttpResult.Ok();
    }
}
