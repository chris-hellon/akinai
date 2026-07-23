using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using InvoiceSoftware.Domain.Enums;
using InvoiceSoftware.Infrastructure.Data;
using InvoiceSoftware.Shared.Api.Localization;
using InvoiceSoftware.Shared.Dtos.Localization;
using InvoiceSoftware.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace InvoiceSoftware.Infrastructure.Handlers.Localization;

public class GetUserLanguageSettingsHandler(IDbContextFactory<ApplicationDbContext> dbFactory, ICurrentUserService currentUserService)
    : IHandle<GetUserLanguageSettings, UserLanguageSettingsDto?>
{
    public async Task<HttpResult<UserLanguageSettingsDto?>> Handle(
        GetUserLanguageSettings request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
            return HttpResult<UserLanguageSettingsDto?>.Unauthorized();

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var settings = await db.UserLanguagePreferences
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);

        if (settings == null)
        {
            // Return defaults
            return HttpResult<UserLanguageSettingsDto?>.Ok(new UserLanguageSettingsDto(
                SupportedLanguage.English.ToString(),
                SupportedLanguage.English.ToString()));
        }

        var dto = new UserLanguageSettingsDto(
            settings.InvoiceLanguage.ToString(),
            settings.EstimateLanguage.ToString());

        return HttpResult<UserLanguageSettingsDto?>.Ok(dto);
    }
}
