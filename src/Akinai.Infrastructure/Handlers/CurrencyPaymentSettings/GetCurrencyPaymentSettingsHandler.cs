using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using Akinai.Infrastructure.Data;
using Akinai.Shared.Api.CurrencyPaymentSettings;
using Akinai.Shared.Dtos.CurrencyPaymentSettings;
using Akinai.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace Akinai.Infrastructure.Handlers.CurrencyPaymentSettings;

public class GetCurrencyPaymentSettingsHandler(IDbContextFactory<ApplicationDbContext> dbFactory, ICurrentUserService currentUserService)
    : IHandle<GetCurrencyPaymentSettings, CurrencyPaymentSettingsDto?>
{
    public async Task<HttpResult<CurrencyPaymentSettingsDto?>> Handle(
        GetCurrencyPaymentSettings request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
            return HttpResult<CurrencyPaymentSettingsDto?>.Ok(null);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var settings = await db.CurrencyPaymentSettings
            .Where(c => c.UserId == userId && c.CurrencyCode == request.CurrencyCode)
            .Select(c => new CurrencyPaymentSettingsDto(
                c.Id,
                c.CurrencyCode,
                c.BankName,
                c.BankAccountName,
                c.BankAccountNumber,
                c.BankSortCode,
                c.BankIban,
                c.BankSwift,
                c.VietQrBankCode,
                c.IntermediaryBankName,
                c.IntermediaryBankSwift,
                c.IntermediaryAccountNumber,
                c.BankAddress,
                c.PaymentPurposeCode,
                c.UpiVpa,
                c.PixKey,
                c.PromptPayId))
            .FirstOrDefaultAsync(cancellationToken);

        return HttpResult<CurrencyPaymentSettingsDto?>.Ok(settings);
    }
}
