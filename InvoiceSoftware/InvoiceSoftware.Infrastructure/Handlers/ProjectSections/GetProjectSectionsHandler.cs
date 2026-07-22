using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using InvoiceSoftware.Infrastructure.Data;
using InvoiceSoftware.Shared.Api.ProjectSections;
using InvoiceSoftware.Shared.Dtos.ProjectSections;
using InvoiceSoftware.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace InvoiceSoftware.Infrastructure.Handlers.ProjectSections;

public class GetProjectSectionsHandler(IDbContextFactory<ApplicationDbContext> dbFactory, ICurrentUserService currentUserService) : IHandle<GetProjectSections, List<ProjectSectionDto>>
{
    public async Task<HttpResult<List<ProjectSectionDto>>> Handle(GetProjectSections request, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId)) return HttpResult<List<ProjectSectionDto>>.Unauthorized();

        var sections = await db.ProjectSections
            .Where(s => s.ProjectId == request.ProjectId && s.Project.Client.UserId == userId)
            .OrderBy(s => s.Order)
            .Select(s => new ProjectSectionDto(
                s.Id,
                s.ProjectId,
                s.Name,
                s.Description,
                s.Order,
                s.Jobs.Count))
            .ToListAsync(cancellationToken);

        return HttpResult<List<ProjectSectionDto>>.Ok(sections);
    }
}
