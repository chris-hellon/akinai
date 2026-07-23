using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using Akinai.Domain.Entities;
using Akinai.Infrastructure.Data;
using Akinai.Shared.Api.ProjectSections;
using Akinai.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace Akinai.Infrastructure.Handlers.ProjectSections;

public class CreateProjectSectionHandler(IDbContextFactory<ApplicationDbContext> dbFactory, ICurrentUserService currentUserService) : IHandle<CreateProjectSection, Guid>
{
    public async Task<HttpResult<Guid>> Handle(CreateProjectSection request, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId)) return HttpResult<Guid>.Unauthorized();

        // The parent project must be the caller's.
        var ownsProject = await db.Projects.AnyAsync(pr => pr.Id == request.ProjectId && pr.Client.UserId == userId, cancellationToken);
        if (!ownsProject) return HttpResult<Guid>.NotFound();

        var maxOrder = await db.ProjectSections
            .Where(s => s.ProjectId == request.ProjectId)
            .MaxAsync(s => (int?)s.Order, cancellationToken) ?? -1;

        var section = ProjectSection.Create(
            request.ProjectId,
            request.Name,
            request.Description,
            maxOrder + 1);

        db.ProjectSections.Add(section);
        await db.SaveChangesAsync(cancellationToken);

        return HttpResult<Guid>.Created(section.Id);
    }
}
