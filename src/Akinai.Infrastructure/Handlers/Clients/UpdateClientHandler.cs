using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using Akinai.Domain.ValueObjects;
using Akinai.Infrastructure.Data;
using Akinai.Infrastructure.Services;
using Akinai.Shared.Api.Clients;
using Akinai.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace Akinai.Infrastructure.Handlers.Clients;

public class UpdateClientHandler(IDbContextFactory<ApplicationDbContext> dbFactory, ICurrentUserService currentUserService,
    IEntitlementService entitlementService) : IHandle<UpdateClient>
{
    public async Task<HttpResult> Handle(UpdateClient request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId)) return HttpResult.Unauthorized();

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var client = await db.Clients.FirstOrDefaultAsync(c => c.Id == request.Id && c.UserId == userId, cancellationToken);
        if (client is null) return HttpResult.NotFound();

        // Currency is gated across every path that can introduce one —
        // see CurrencyPolicy. Gating client creation alone would be no gate,
        // since the same currency can arrive by editing a client, changing the
        // default, or adding bank details for it.
        var entitlements = await entitlementService.GetAsync(userId, cancellationToken);
        if (!entitlements.MultiCurrency
            && !await CurrencyPolicy.AllowsAsync(db, userId, request.Currency, cancellationToken))
        {
            return HttpResult.Forbidden();
        }

        Address? billingAddress = null;
        if (!string.IsNullOrEmpty(request.Street))
        {
            billingAddress = new Address(
                request.Street,
                request.City ?? "",
                request.State ?? "",
                request.PostalCode ?? "",
                request.Country ?? "");
        }

        client.Update(
            request.Name,
            request.Email,
            request.DefaultHourlyRate,
            request.Currency,
            request.CompanyName,
            request.Phone,
            billingAddress);

        if (request.IsActive)
            client.Activate();
        else
            client.Deactivate();

        await db.SaveChangesAsync(cancellationToken);
        return HttpResult.NoContent();
    }
}
