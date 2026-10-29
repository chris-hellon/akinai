using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using Akinai.Infrastructure.Data;
using Akinai.Shared.Api.CurrencyPaymentSettings;
using Akinai.Shared.Dtos.CurrencyPaymentSettings;
using Akinai.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace Akinai.Infrastructure.Handlers.CurrencyPaymentSettings;

public class GetAllCurrencyPaymentSettingsHandler(IDbContextFactory<ApplicationDbContext> dbFactory, ICurrentUserService currentUserService)
    : IHandle<GetAllCurrencyPaymentSettings, List<CurrencyPaymentSettingsDto>>
{
    public async Task<HttpResult<List<CurrencyPaymentSettingsDto>>> Handle(
        GetAllCurrencyPaymentSettings request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
            return HttpResult<List<CurrencyPaymentSettingsDto>>.Ok([]);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var settings = await db.CurrencyPaymentSettings
            .Where(c => c.UserId == userId)
            .OrderBy(c => c.CurrencyCode)
            .Select(CurrencyPaymentSettingsMapping.Projection)
            .ToListAsync(cancellationToken);

        return HttpResult<List<CurrencyPaymentSettingsDto>>.Ok(settings);
    }
}
