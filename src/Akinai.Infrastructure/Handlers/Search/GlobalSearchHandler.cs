using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using Akinai.Infrastructure.Data;
using Akinai.Shared.Api.Search;
using Akinai.Shared.Dtos.Search;
using Akinai.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace Akinai.Infrastructure.Handlers.Search;

public class GlobalSearchHandler(
    IDbContextFactory<ApplicationDbContext> dbFactory,
    ICurrentUserService currentUserService)
    : IHandle<GlobalSearch, GlobalSearchResultDto>
{
    private const int PerType = 6;

    public async Task<HttpResult<GlobalSearchResultDto>> Handle(GlobalSearch request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
            return HttpResult<GlobalSearchResultDto>.Unauthorized();

        var q = (request.Query ?? string.Empty).Trim().ToLower();
        if (q.Length == 0)
            return HttpResult<GlobalSearchResultDto>.Ok(GlobalSearchResultDto.Empty());

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var clients = await db.Clients
            .Where(c => c.UserId == userId &&
                (c.Name.ToLower().Contains(q) || (c.CompanyName != null && c.CompanyName.ToLower().Contains(q))))
            .OrderBy(c => c.Name)
            .Take(PerType)
            .Select(c => new GlobalSearchItemDto("Client", c.Name, c.CompanyName, $"/clients/{c.Id}"))
            .ToListAsync(cancellationToken);

        var invoices = await db.Invoices
            .Where(i => i.Client.UserId == userId &&
                (i.InvoiceNumber.ToLower().Contains(q) || i.Client.Name.ToLower().Contains(q)))
            .OrderByDescending(i => i.IssueDate)
            .Take(PerType)
            .Select(i => new GlobalSearchItemDto("Invoice", i.InvoiceNumber, i.Client.Name, $"/invoices/{i.Id}"))
            .ToListAsync(cancellationToken);

        var projects = await db.Projects
            .Where(p => p.Client.UserId == userId &&
                (p.Name.ToLower().Contains(q) || p.Client.Name.ToLower().Contains(q)))
            .OrderBy(p => p.Name)
            .Take(PerType)
            .Select(p => new GlobalSearchItemDto("Project", p.Name, p.Client.Name, $"/projects/{p.Id}"))
            .ToListAsync(cancellationToken);

        var estimates = await db.Estimates
            .Where(e => e.UserId == userId &&
                (e.EstimateNumber.ToLower().Contains(q) || e.Client.Name.ToLower().Contains(q)))
            .OrderByDescending(e => e.EstimateDate)
            .Take(PerType)
            .Select(e => new GlobalSearchItemDto("Estimate", e.EstimateNumber, e.Client.Name, $"/estimates/{e.Id}"))
            .ToListAsync(cancellationToken);

        return HttpResult<GlobalSearchResultDto>.Ok(
            new GlobalSearchResultDto(clients, invoices, projects, estimates));
    }
}
