using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using Akinai.Domain.ValueObjects;
using Akinai.Infrastructure.Data;
using Akinai.Infrastructure.Services;
using Akinai.Shared.Api.BusinessProfile;
using Akinai.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace Akinai.Infrastructure.Handlers.BusinessProfile;

public class SaveBusinessProfileHandler(IDbContextFactory<ApplicationDbContext> dbFactory, ICurrentUserService currentUserService,
    IEntitlementService entitlementService)
    : IHandle<SaveBusinessProfile>
{
    public async Task<HttpResult> Handle(SaveBusinessProfile request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
            return HttpResult.Unauthorized();

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var profile = await db.BusinessProfiles.FirstOrDefaultAsync(b => b.UserId == userId, cancellationToken);

        // Currency is gated across every path that can introduce one —
        // see CurrencyPolicy. Gating client creation alone would be no gate,
        // since the same currency can arrive by editing a client, changing the
        // default, or adding bank details for it.
        var entitlements = await entitlementService.GetAsync(userId, cancellationToken);
        if (!entitlements.MultiCurrency
            && !await CurrencyPolicy.AllowsAsync(db, userId, request.DefaultCurrency, cancellationToken))
        {
            return HttpResult.Forbidden();
        }

        Address? address = null;
        if (!string.IsNullOrWhiteSpace(request.Street))
        {
            address = new Address(
                request.Street,
                request.City ?? "",
                request.State ?? "",
                request.PostalCode ?? "",
                request.Country ?? "");
        }

        if (profile is null)
        {
            profile = Domain.Entities.BusinessProfile.Create(userId, request.CompanyName, request.Email);
            profile.Update(
                request.CompanyName,
                request.TradingName,
                request.Email,
                request.Phone,
                request.Website,
                address,
                request.TaxNumber,
                request.RegistrationNumber,
                request.BankName,
                request.BankAccountName,
                request.BankAccountNumber,
                request.BankSortCode,
                request.BankIban,
                request.BankSwift,
                request.DefaultCurrency,
                request.DefaultPaymentTermsDays,
                request.InvoiceNotes,
                request.InvoiceFooter,
                request.InvoiceEmailSubject,
                request.InvoiceEmailBody,
                request.PayPalMeUsername,
                request.WiseEmail,
                request.RevolutUsername,
                request.VietQrBankCode);
            db.BusinessProfiles.Add(profile);
        }
        else
        {
            profile.Update(
                request.CompanyName,
                request.TradingName,
                request.Email,
                request.Phone,
                request.Website,
                address,
                request.TaxNumber,
                request.RegistrationNumber,
                request.BankName,
                request.BankAccountName,
                request.BankAccountNumber,
                request.BankSortCode,
                request.BankIban,
                request.BankSwift,
                request.DefaultCurrency,
                request.DefaultPaymentTermsDays,
                request.InvoiceNotes,
                request.InvoiceFooter,
                request.InvoiceEmailSubject,
                request.InvoiceEmailBody,
                request.PayPalMeUsername,
                request.WiseEmail,
                request.RevolutUsername,
                request.VietQrBankCode);
        }

        await db.SaveChangesAsync(cancellationToken);
        return HttpResult.NoContent();
    }
}
