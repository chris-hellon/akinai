using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using InvoiceSoftware.Infrastructure.Data;
using InvoiceSoftware.Shared.Api.ProjectSections;
using InvoiceSoftware.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace InvoiceSoftware.Infrastructure.Handlers.ProjectSections;

public class DeleteProjectSectionHandler(IDbContextFactory<ApplicationDbContext> dbFactory, ICurrentUserService currentUserService) : IHandle<DeleteProjectSection>
{
    public async Task<HttpResult> Handle(DeleteProjectSection request, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId)) return HttpResult.Unauthorized();

        var section = await db.ProjectSections.FirstOrDefaultAsync(s => s.Id == request.Id && s.Project.Client.UserId == userId, cancellationToken);
        if (section is null) return HttpResult.NotFound();

        db.ProjectSections.Remove(section);
        await db.SaveChangesAsync(cancellationToken);

        return HttpResult.NoContent();
    }
}
