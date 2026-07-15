using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using InvoiceSoftware.Domain.Enums;
using InvoiceSoftware.Infrastructure.Data;
using InvoiceSoftware.Shared.Api.Dashboard;
using InvoiceSoftware.Shared.Dtos.Dashboard;
using InvoiceSoftware.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace InvoiceSoftware.Infrastructure.Handlers.Dashboard;

public class GetInvoiceStatusBreakdownHandler(IDbContextFactory<ApplicationDbContext> dbFactory, ICurrentUserService currentUserService)
    : IHandle<GetInvoiceStatusBreakdown, InvoiceStatusBreakdownDto?>
{
    public async Task<HttpResult<InvoiceStatusBreakdownDto?>> Handle(
        GetInvoiceStatusBreakdown request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
            return HttpResult<InvoiceStatusBreakdownDto?>.Unauthorized();

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var now = DateOnly.FromDateTime(DateTime.UtcNow);

        var businessProfile = await db.BusinessProfiles
            .FirstOrDefaultAsync(bp => bp.UserId == userId, cancellationToken);
        var reporting = !string.IsNullOrWhiteSpace(request.ReportingCurrency)
            ? request.ReportingCurrency!
            : businessProfile?.DefaultCurrency ?? "USD";

        var invoices = await db.Invoices
            .Include(i => i.CurrencyTotals)
            .Include(i => i.LineItems)
            .Include(i => i.TimeEntries)
                .ThenInclude(te => te.Job)
                    .ThenInclude(j => j.Project)
                        .ThenInclude(p => p.Client)
            .Include(i => i.Client)
            .Where(i => i.Client.UserId == userId)
            .ToListAsync(cancellationToken);

        decimal Amount(Domain.Entities.Invoice invoice) => InvoiceTotals.InReporting(invoice, reporting);

        // Overdue is derived from the due date (Sent/Overdue past due), consistent with the
        // rest of the dashboard, rather than relying solely on the Status enum.
        bool Unpaid(Domain.Entities.Invoice i) => i.Status is InvoiceStatus.Sent or InvoiceStatus.Overdue;
        var draft = invoices.Where(i => i.Status == InvoiceStatus.Draft).ToList();
        var paid = invoices.Where(i => i.Status == InvoiceStatus.Paid).ToList();
        var voided = invoices.Where(i => i.Status == InvoiceStatus.Void).ToList();
        var overdue = invoices.Where(i => Unpaid(i) && i.DueDate < now).ToList();
        var sent = invoices.Where(i => Unpaid(i) && i.DueDate >= now).ToList();

        var dto = new InvoiceStatusBreakdownDto(
            draft.Count, sent.Count, paid.Count, overdue.Count, voided.Count,
            draft.Sum(Amount), sent.Sum(Amount), paid.Sum(Amount), overdue.Sum(Amount));

        return HttpResult<InvoiceStatusBreakdownDto?>.Ok(dto);
    }
}
