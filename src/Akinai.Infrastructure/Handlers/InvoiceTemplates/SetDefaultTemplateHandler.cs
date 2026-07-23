using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using Akinai.Infrastructure.Data;
using Akinai.Shared.Api.InvoiceTemplates;
using Akinai.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace Akinai.Infrastructure.Handlers.InvoiceTemplates;

public class SetDefaultTemplateHandler(IDbContextFactory<ApplicationDbContext> dbFactory, ICurrentUserService currentUserService)
    : IHandle<SetDefaultTemplate>
{
    public async Task<HttpResult> Handle(SetDefaultTemplate request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
            return HttpResult.Unauthorized();

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var template = await db.InvoiceTemplates
            .FirstOrDefaultAsync(t => t.Id == request.Id &&
                                       (t.UserId == userId || t.IsSystem), cancellationToken);

        if (template == null)
            return HttpResult.NotFound();

        // Clear default from all user's templates
        var userTemplates = await db.InvoiceTemplates
            .Where(t => t.UserId == userId && t.IsDefault)
            .ToListAsync(cancellationToken);

        foreach (var ut in userTemplates)
        {
            ut.ClearDefault();
        }

        // Set new default
        template.SetAsDefault();

        await db.SaveChangesAsync(cancellationToken);
        return HttpResult.NoContent();
    }
}
