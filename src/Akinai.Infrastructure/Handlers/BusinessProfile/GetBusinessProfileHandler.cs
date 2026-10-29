using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using Akinai.Infrastructure.Data;
using Akinai.Shared.Api.BusinessProfile;
using Akinai.Shared.Dtos.BusinessProfile;
using Akinai.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace Akinai.Infrastructure.Handlers.BusinessProfile;

public class GetBusinessProfileHandler(
    IDbContextFactory<ApplicationDbContext> dbFactory,
    ICurrentUserService currentUserService,
    Services.IExchangeRateService exchangeRates)
    : IHandle<GetBusinessProfile, BusinessProfileDto?>
{
    public async Task<HttpResult<BusinessProfileDto?>> Handle(GetBusinessProfile request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId)) return HttpResult<BusinessProfileDto?>.Ok(null);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var profile = await db.BusinessProfiles.FirstOrDefaultAsync(b => b.UserId == userId, cancellationToken);
        if (profile is null) return HttpResult<BusinessProfileDto?>.Ok(null);

        var result = new BusinessProfileDto(
            profile.Id,
            profile.CompanyName,
            profile.TradingName,
            profile.Email.Value,
            profile.Phone?.Value,
            profile.Website,
            profile.Address?.Street1,
            profile.Address?.City,
            profile.Address?.State,
            profile.Address?.PostalCode,
            profile.Address?.Country,
            profile.Address?.CountryCode,
            profile.TaxNumber,
            profile.RegistrationNumber,
            profile.BankName,
            profile.BankAccountName,
            profile.BankAccountNumber,
            profile.BankSortCode,
            profile.BankIban,
            profile.BankSwift,
            profile.Logo != null,
            profile.DefaultCurrency,
            profile.DefaultPaymentTermsDays,
            profile.InvoiceNotes,
            profile.InvoiceFooter,
            profile.InvoiceEmailSubject,
            profile.InvoiceEmailBody,
            profile.PayPalMeUsername,
            profile.WiseEmail,
            profile.RevolutUsername,
            profile.VietQrBankCode,
            // Live rate, not a number typed into settings — see VndPaymentRate.
            await Services.VndPaymentRate.GetAsync(exchangeRates, profile.DefaultCurrency, cancellationToken),
            profile.AutomaticRemindersEnabled);

        return HttpResult<BusinessProfileDto?>.Ok(result);
    }
}
