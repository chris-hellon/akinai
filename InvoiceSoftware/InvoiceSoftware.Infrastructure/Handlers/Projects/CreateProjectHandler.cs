using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using InvoiceSoftware.Domain.Entities;
using InvoiceSoftware.Infrastructure.Data;
using InvoiceSoftware.Shared.Api.Projects;
using InvoiceSoftware.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace InvoiceSoftware.Infrastructure.Handlers.Projects;

public class CreateProjectHandler(IDbContextFactory<ApplicationDbContext> dbFactory, ICurrentUserService currentUserService) : IHandle<CreateProject, Guid>
{
    public async Task<HttpResult<Guid>> Handle(CreateProject request, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId)) return HttpResult<Guid>.Unauthorized();

        // The client this project hangs off must be the caller's, otherwise
        // anyone could create a project under someone else's client.
        var ownsClient = await db.Clients.AnyAsync(c => c.Id == request.ClientId && c.UserId == userId, cancellationToken);
        if (!ownsClient) return HttpResult<Guid>.NotFound();

        var project = Project.Create(
            request.ClientId,
            request.Name,
            request.Description,
            request.StartDate,
            request.EndDate,
            request.HourlyRateOverride);

        db.Projects.Add(project);
        await db.SaveChangesAsync(cancellationToken);
        return HttpResult<Guid>.Created(project.Id);
    }
}
