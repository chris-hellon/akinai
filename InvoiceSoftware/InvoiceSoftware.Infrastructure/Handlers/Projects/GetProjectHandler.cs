using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using InvoiceSoftware.Infrastructure.Data;
using InvoiceSoftware.Shared.Api.Projects;
using InvoiceSoftware.Shared.Dtos.Jobs;
using InvoiceSoftware.Shared.Dtos.Projects;
using InvoiceSoftware.Shared.Dtos.ProjectSections;
using InvoiceSoftware.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace InvoiceSoftware.Infrastructure.Handlers.Projects;

public class GetProjectHandler(IDbContextFactory<ApplicationDbContext> dbFactory, ICurrentUserService currentUserService) : IHandle<GetProject, ProjectDetailDto?>
{
    public async Task<HttpResult<ProjectDetailDto?>> Handle(GetProject request, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId)) return HttpResult<ProjectDetailDto?>.Unauthorized();

        var project = await db.Projects
            .Include(p => p.Client)
            .Include(p => p.Jobs)
                .ThenInclude(j => j.Section)
            .Include(p => p.Sections)
                .ThenInclude(s => s.Jobs)
            .FirstOrDefaultAsync(p => p.Id == request.Id && p.Client.UserId == userId, cancellationToken);

        if (project is null) return HttpResult<ProjectDetailDto?>.NotFound();

        var result = new ProjectDetailDto(
            project.Id,
            project.ClientId,
            project.Client.Name,
            project.Name,
            project.Description,
            project.Status.ToString(),
            project.StartDate,
            project.EndDate,
            project.HourlyRateOverride?.Value,
            project.Jobs.Select(j => new JobDto(
                j.Id,
                j.ProjectId,
                project.Name,
                project.ClientId,
                project.Client.Name,
                j.SectionId,
                j.Section?.Name,
                j.Name,
                j.Description,
                j.Status.ToString(),
                j.EstimatedHours?.Value,
                j.HourlyRateOverride?.Value)).ToList(),
            project.Sections.OrderBy(s => s.Order).Select(s => new ProjectSectionDto(
                s.Id,
                s.ProjectId,
                s.Name,
                s.Description,
                s.Order,
                s.Jobs.Count)).ToList());

        return HttpResult<ProjectDetailDto?>.Ok(result);
    }
}
