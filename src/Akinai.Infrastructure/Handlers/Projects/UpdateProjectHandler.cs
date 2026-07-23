using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using Akinai.Domain.Enums;
using Akinai.Infrastructure.Data;
using Akinai.Shared.Api.Projects;
using Akinai.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace Akinai.Infrastructure.Handlers.Projects;

public class UpdateProjectHandler(IDbContextFactory<ApplicationDbContext> dbFactory, ICurrentUserService currentUserService) : IHandle<UpdateProject>
{
    public async Task<HttpResult> Handle(UpdateProject request, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId)) return HttpResult.Unauthorized();

        var project = await db.Projects.FirstOrDefaultAsync(p => p.Id == request.Id && p.Client.UserId == userId, cancellationToken);
        if (project is null) return HttpResult.NotFound();

        var status = Enum.Parse<ProjectStatus>(request.Status);
        project.Update(request.Name, request.Description, status, request.StartDate, request.EndDate, request.HourlyRateOverride);
        await db.SaveChangesAsync(cancellationToken);
        return HttpResult.NoContent();
    }
}
