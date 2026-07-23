using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using Akinai.Infrastructure.Data;
using Akinai.Shared.Api;
using Akinai.Shared.Api.Clients;
using Akinai.Shared.Dtos.Clients;
using Akinai.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace Akinai.Infrastructure.Handlers.Clients;

public class GetClientsHandler(IDbContextFactory<ApplicationDbContext> dbFactory, ICurrentUserService currentUserService) : IHandle<GetClients, PaginatedResponse<ClientDto>>
{
    public async Task<HttpResult<PaginatedResponse<ClientDto>>> Handle(GetClients request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
            return HttpResult<PaginatedResponse<ClientDto>>.Unauthorized();

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var query = db.Clients.Where(c => c.UserId == userId);

        if (request.ActiveOnly)
            query = query.Where(c => c.IsActive);

        // Search filter
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim().ToLower();
            query = query.Where(c =>
                c.Name.ToLower().Contains(search) ||
                (c.CompanyName != null && c.CompanyName.ToLower().Contains(search)) ||
                c.Email.Value.ToLower().Contains(search));
        }

        // Get total count before pagination
        var totalCount = await query.CountAsync(cancellationToken);

        // Apply pagination
        var clients = await query
            .OrderBy(c => c.Name)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var items = clients.Select(c => new ClientDto(
            c.Id,
            c.Name,
            c.CompanyName,
            c.Email.Value,
            c.Phone?.Value,
            c.DefaultHourlyRate.Value,
            c.Currency,
            c.IsActive)).ToList();

        var result = new PaginatedResponse<ClientDto>
        {
            Items = items,
            Page = request.Page,
            PageSize = request.PageSize,
            TotalCount = totalCount
        };

        return HttpResult<PaginatedResponse<ClientDto>>.Ok(result);
    }
}
