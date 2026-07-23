using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using InvoiceSoftware.Infrastructure.Data;
using InvoiceSoftware.Shared.Api.CurrencyPaymentSettings;
using InvoiceSoftware.Shared.Dtos.CurrencyPaymentSettings;
using InvoiceSoftware.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace InvoiceSoftware.Infrastructure.Handlers.CurrencyPaymentSettings;

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
                c.PaymentPurposeCode))
            .ToListAsync(cancellationToken);

        return HttpResult<List<CurrencyPaymentSettingsDto>>.Ok(settings);
    }
}
