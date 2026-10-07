using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using Akinai.Domain.Entities;
using Akinai.Domain.ValueObjects;
using Akinai.Infrastructure.Data;
using Akinai.Shared.Geography;
using Akinai.Infrastructure.Services;
using Akinai.Shared.Api.Clients;
using Akinai.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace Akinai.Infrastructure.Handlers.Clients;

public class CreateClientHandler(
    IDbContextFactory<ApplicationDbContext> dbFactory,
    ICurrentUserService currentUserService,
    IEntitlementService entitlementService) : IHandle<CreateClient, Guid>
{
    public async Task<HttpResult<Guid>> Handle(CreateClient request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
            return HttpResult<Guid>.Unauthorized();

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var entitlements = await entitlementService.GetAsync(userId, cancellationToken);
        if (!entitlements.MultiCurrency
            && !await CurrencyPolicy.AllowsAsync(db, userId, request.Currency, cancellationToken))
        {
            return HttpResult<Guid>.Forbidden();
        }
        Address? billingAddress = null;
        if (!string.IsNullOrEmpty(request.Street))
        {
            billingAddress = new Address(
                request.Street,
                request.City ?? "",
                request.State ?? "",
                request.PostalCode ?? "",
                request.Country ?? "",
                countryCode: Countries.TryParse(request.CountryCode) ?? Countries.TryParse(request.Country));
        }

        var client = Client.Create(
            userId,
            request.Name,
            request.Email,
            request.DefaultHourlyRate,
            request.Currency,
            request.CompanyName,
            request.Phone,
            billingAddress,
            notes: null,
            taxNumber: request.TaxNumber,
            isBusiness: request.IsBusiness);

        db.Clients.Add(client);
        await db.SaveChangesAsync(cancellationToken);
        return HttpResult<Guid>.Created(client.Id);
    }

}
