using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using Akinai.Domain.Enums;
using Akinai.Infrastructure.Data;
using Akinai.Shared.Api;
using Akinai.Shared.Api.Jobs;
using Akinai.Shared.Dtos.Jobs;
using Akinai.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace Akinai.Infrastructure.Handlers.Jobs;

public class GetJobsHandler(IDbContextFactory<ApplicationDbContext> dbFactory, ICurrentUserService currentUserService) : IHandle<GetJobs, PaginatedResponse<JobDto>>
{
    public async Task<HttpResult<PaginatedResponse<JobDto>>> Handle(GetJobs request, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId)) return HttpResult<PaginatedResponse<JobDto>>.Unauthorized();

        var query = db.Jobs
            .Include(j => j.Project)
                .ThenInclude(p => p.Client)
            .Include(j => j.Section)
            .Where(j => j.Project.Client.UserId == userId);

        if (request.ProjectId.HasValue)
            query = query.Where(j => j.ProjectId == request.ProjectId.Value);

        // Status filter
        if (!string.IsNullOrWhiteSpace(request.Status) && Enum.TryParse<JobStatus>(request.Status, out var status))
            query = query.Where(j => j.Status == status);

        // Search filter
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim().ToLower();
            query = query.Where(j =>
                j.Name.ToLower().Contains(search) ||
                (j.Description != null && j.Description.ToLower().Contains(search)) ||
                j.Project.Name.ToLower().Contains(search) ||
                j.Project.Client.Name.ToLower().Contains(search));
        }

        // Get total count before pagination
        var totalCount = await query.CountAsync(cancellationToken);

        // Apply pagination
        var jobs = await query
            .OrderBy(j => j.Name)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var items = jobs.Select(j => new JobDto(
            j.Id,
            j.ProjectId,
            j.Project.Name,
            j.Project.ClientId,
            j.Project.Client.Name,
            j.SectionId,
            j.Section?.Name,
            j.Name,
            j.Description,
            j.Status.ToString(),
            j.EstimatedHours?.Value,
            j.HourlyRateOverride?.Value)).ToList();

        var result = new PaginatedResponse<JobDto>
        {
            Items = items,
            Page = request.Page,
            PageSize = request.PageSize,
            TotalCount = totalCount
        };

        return HttpResult<PaginatedResponse<JobDto>>.Ok(result);
    }
}
