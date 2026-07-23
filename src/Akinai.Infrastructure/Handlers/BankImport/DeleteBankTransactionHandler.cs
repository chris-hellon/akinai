using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using Akinai.Infrastructure.Data;
using Akinai.Shared.Api.BankImport;
using Akinai.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace Akinai.Infrastructure.Handlers.BankImport;

public class DeleteBankTransactionHandler(
    IDbContextFactory<ApplicationDbContext> dbFactory,
    ICurrentUserService currentUserService)
    : IHandle<DeleteBankTransaction>
{
    public async Task<HttpResult> Handle(DeleteBankTransaction request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
            return HttpResult.Unauthorized();

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await db.BankTransactions
            .Where(bt => bt.Id == request.Id && bt.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);

        return HttpResult.Ok();
    }
}
