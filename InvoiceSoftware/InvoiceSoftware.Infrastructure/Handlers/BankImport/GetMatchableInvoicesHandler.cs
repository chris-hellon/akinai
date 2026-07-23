using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using InvoiceSoftware.Infrastructure.Data;
using InvoiceSoftware.Shared.Api.BankImport;
using InvoiceSoftware.Shared.Dtos.BankImport;
using InvoiceSoftware.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace InvoiceSoftware.Infrastructure.Handlers.BankImport;

public class GetMatchableInvoicesHandler(
    IDbContextFactory<ApplicationDbContext> dbFactory,
    ICurrentUserService currentUserService)
    : IHandle<GetMatchableInvoices, List<InvoiceOptionDto>>
{
    public async Task<HttpResult<List<InvoiceOptionDto>>> Handle(
        GetMatchableInvoices request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
            return HttpResult<List<InvoiceOptionDto>>.Unauthorized();

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        // Any of the user's invoices can be linked to a transaction, regardless of status
        // — the Status is shown alongside each option so you can see paid vs unpaid.
        var query = db.Invoices.Where(i => i.Client.UserId == userId);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            query = query.Where(i => i.InvoiceNumber.Contains(term) || i.Client.Name.Contains(term));
        }

        var items = await query
            .OrderByDescending(i => i.IssueDate)
            .Take(300)
            .Select(i => new InvoiceOptionDto(
                i.Id,
                i.InvoiceNumber,
                i.Client.Name,
                i.Total,
                i.Currency,
                i.Status.ToString()))
            .ToListAsync(cancellationToken);

        return HttpResult<List<InvoiceOptionDto>>.Ok(items);
    }
}
