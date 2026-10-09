using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using Akinai.Infrastructure.Data;
using Akinai.Infrastructure.Services;
using Akinai.Shared.Api.CurrencyPaymentSettings;
using Akinai.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace Akinai.Infrastructure.Handlers.CurrencyPaymentSettings;

public class SaveCurrencyPaymentSettingsHandler(IDbContextFactory<ApplicationDbContext> dbFactory, ICurrentUserService currentUserService,
    IEntitlementService entitlementService)
    : IHandle<SaveCurrencyPaymentSettings>
{
    public async Task<HttpResult> Handle(SaveCurrencyPaymentSettings request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
            return HttpResult.Unauthorized();

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        // Currency is gated across every path that can introduce one —
        // see CurrencyPolicy. Gating client creation alone would be no gate,
        // since the same currency can arrive by editing a client, changing the
        // default, or adding bank details for it.
        var entitlements = await entitlementService.GetAsync(userId, cancellationToken);
        if (!entitlements.MultiCurrency
            && !await CurrencyPolicy.AllowsAsync(db, userId, request.CurrencyCode, cancellationToken))
        {
            return HttpResult.Forbidden();
        }
        var settings = await db.CurrencyPaymentSettings
            .FirstOrDefaultAsync(c => c.UserId == userId && c.CurrencyCode == request.CurrencyCode, cancellationToken);

        if (settings is null)
        {
            settings = Domain.Entities.CurrencyPaymentSettings.Create(userId, request.CurrencyCode);
            db.CurrencyPaymentSettings.Add(settings);
        }

        settings.Update(
            request.BankName,
            request.BankAccountName,
            request.BankAccountNumber,
            request.BankSortCode,
            request.BankIban,
            request.BankSwift,
            request.VietQrBankCode,
            request.IntermediaryBankName,
            request.IntermediaryBankSwift,
            request.IntermediaryAccountNumber,
            request.BankAddress,
            request.PaymentPurposeCode,
            request.UpiVpa,
            request.PixKey,
            request.PromptPayId);

        await db.SaveChangesAsync(cancellationToken);
        return HttpResult.NoContent();
    }
}
