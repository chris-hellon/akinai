using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using Akinai.Infrastructure.Data;
using Akinai.Shared.Api.BankImport;
using Akinai.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace Akinai.Infrastructure.Handlers.BankImport;

public class RecordWithholdingCertificateHandler(
    IDbContextFactory<ApplicationDbContext> dbFactory,
    ICurrentUserService currentUserService) : IHandle<RecordWithholdingCertificate>
{
    public async Task<HttpResult> Handle(
        RecordWithholdingCertificate request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId)) return HttpResult.Unauthorized();

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var transaction = await db.BankTransactions
            .FirstOrDefaultAsync(bt => bt.Id == request.Id && bt.UserId == userId, cancellationToken);

        if (transaction is null) return HttpResult.NotFound();

        try
        {
            transaction.MarkWithholdingCertificateReceived();
        }
        catch (Domain.Exceptions.DomainException)
        {
            // No tax was withheld on this payment, so there is no certificate to record.
            return HttpResult.BadRequest();
        }

        await db.SaveChangesAsync(cancellationToken);
        return HttpResult.Ok();
    }
}
