using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using Akinai.Domain.Enums;
using Akinai.Domain.Services;
using Akinai.Infrastructure.Data;
using Akinai.Shared.Api.Invoices;
using Akinai.Shared.Dtos.Timing;
using Akinai.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace Akinai.Infrastructure.Handlers.Invoices;

/// <summary>
/// Predicts when an invoice will be paid from everything this user has been paid before.
/// </summary>
public class GetInvoicePaymentTimingHandler(
    IDbContextFactory<ApplicationDbContext> dbFactory,
    ICurrentUserService currentUserService,
    IDateTimeService clock) : IHandle<GetInvoicePaymentTiming, PaymentTimingDto?>
{
    public async Task<HttpResult<PaymentTimingDto?>> Handle(
        GetInvoicePaymentTiming request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId)) return HttpResult<PaymentTimingDto?>.Unauthorized();

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var invoice = await db.Invoices
            .AsNoTracking()
            .Where(i => i.Id == request.Id && i.Client.UserId == userId)
            .Select(i => new
            {
                i.ClientId,
                CountryCode = i.Client.BillingAddress != null ? i.Client.BillingAddress.CountryCode : null,
                i.IssueDate,
                i.DueDate,
                i.Total,
                i.FirstViewedAtUtc,
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (invoice is null) return HttpResult<PaymentTimingDto?>.NotFound();

        // Everything already settled. Only paid invoices teach anything about timing, and
        // the invoice being asked about is necessarily not one of them.
        var history = await db.Invoices
            .AsNoTracking()
            .Where(i => i.Client.UserId == userId
                        && i.Id != request.Id
                        && i.Status == InvoiceStatus.Paid
                        && i.PaidDate != null)
            .Select(i => new
            {
                i.ClientId,
                CountryCode = i.Client.BillingAddress != null ? i.Client.BillingAddress.CountryCode : null,
                i.IssueDate,
                i.DueDate,
                PaidDate = i.PaidDate!.Value,
                i.Total,
            })
            .ToListAsync(cancellationToken);

        var observations = history.Select(h => new PaymentTiming.Observation(
            h.ClientId, h.CountryCode, h.IssueDate, h.DueDate, h.PaidDate, h.Total));

        var prediction = PaymentTiming.Predict(
            observations,
            new PaymentTiming.Query(
                invoice.ClientId,
                invoice.CountryCode,
                invoice.IssueDate,
                invoice.DueDate,
                invoice.Total,
                FirstViewedOn: invoice.FirstViewedAtUtc is { } v ? DateOnly.FromDateTime(v) : null),
            asOf: DateOnly.FromDateTime(clock.Now));

        return HttpResult<PaymentTimingDto?>.Ok(new PaymentTimingDto(
            prediction.Basis.ToString(),
            prediction.Confidence.ToString(),
            prediction.SampleSize,
            prediction.ExpectedPaymentDate,
            prediction.EarliestLikelyDate,
            prediction.LatestLikelyDate,
            prediction.MedianDaysAfterDue,
            prediction.PaysBeforeDue,
            prediction.IsAlreadyLaterThanUsual,
            prediction.DaysLaterThanUsual,
            prediction.Explanation));
    }
}
