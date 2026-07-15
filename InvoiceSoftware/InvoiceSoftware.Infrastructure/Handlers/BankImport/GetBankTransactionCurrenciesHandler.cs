using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using InvoiceSoftware.Infrastructure.Data;
using InvoiceSoftware.Shared.Api.BankImport;
using InvoiceSoftware.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace InvoiceSoftware.Infrastructure.Handlers.BankImport;

public class GetBankTransactionCurrenciesHandler(
    IDbContextFactory<ApplicationDbContext> dbFactory,
    ICurrentUserService currentUserService)
    : IHandle<GetBankTransactionCurrencies, List<string>>
{
    public async Task<HttpResult<List<string>>> Handle(GetBankTransactionCurrencies request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
            return HttpResult<List<string>>.Unauthorized();

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var currencies = await db.BankTransactions
            .Where(bt => bt.UserId == userId)
            .Select(bt => bt.Currency)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync(cancellationToken);

        return HttpResult<List<string>>.Ok(currencies);
    }
}
