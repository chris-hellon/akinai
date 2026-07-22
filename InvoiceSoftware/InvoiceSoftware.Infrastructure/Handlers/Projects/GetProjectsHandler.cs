using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using InvoiceSoftware.Domain.Enums;
using InvoiceSoftware.Infrastructure.Data;
using InvoiceSoftware.Shared.Api;
using InvoiceSoftware.Shared.Api.Projects;
using InvoiceSoftware.Shared.Dtos.Projects;
using InvoiceSoftware.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace InvoiceSoftware.Infrastructure.Handlers.Projects;

public class GetProjectsHandler(IDbContextFactory<ApplicationDbContext> dbFactory, ICurrentUserService currentUserService) : IHandle<GetProjects, PaginatedResponse<ProjectDto>>
{
    public async Task<HttpResult<PaginatedResponse<ProjectDto>>> Handle(GetProjects request, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
            return HttpResult<PaginatedResponse<ProjectDto>>.Unauthorized();

        var query = db.Projects
            .Include(p => p.Client)
            .Include(p => p.Jobs)
            .Where(p => p.Client.UserId == userId);

        if (request.ClientId.HasValue)
            query = query.Where(p => p.ClientId == request.ClientId.Value);

        // Status filter
        if (!string.IsNullOrWhiteSpace(request.Status) && Enum.TryParse<ProjectStatus>(request.Status, out var status))
            query = query.Where(p => p.Status == status);

        // Search filter
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim().ToLower();
            query = query.Where(p =>
                p.Name.ToLower().Contains(search) ||
                (p.Description != null && p.Description.ToLower().Contains(search)) ||
                p.Client.Name.ToLower().Contains(search));
        }

        // Get total count before pagination
        var totalCount = await query.CountAsync(cancellationToken);

        // Apply pagination
        var projects = await query
            .OrderBy(p => p.Name)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var items = projects.Select(p => new ProjectDto(
            p.Id,
            p.ClientId,
            p.Client.Name,
            p.Client.Currency,
            p.Name,
            p.Description,
            p.Status.ToString(),
            p.StartDate,
            p.EndDate,
            p.HourlyRateOverride?.Value,
            p.Jobs.Count)).ToList();

        var result = new PaginatedResponse<ProjectDto>
        {
            Items = items,
            Page = request.Page,
            PageSize = request.PageSize,
            TotalCount = totalCount
        };

        return HttpResult<PaginatedResponse<ProjectDto>>.Ok(result);
    }
}
