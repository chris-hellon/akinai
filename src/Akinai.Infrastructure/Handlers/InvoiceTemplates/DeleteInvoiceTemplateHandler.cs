using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using Akinai.Infrastructure.Data;
using Akinai.Shared.Api.InvoiceTemplates;
using Akinai.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace Akinai.Infrastructure.Handlers.InvoiceTemplates;

public class DeleteInvoiceTemplateHandler(IDbContextFactory<ApplicationDbContext> dbFactory, ICurrentUserService currentUserService)
    : IHandle<DeleteInvoiceTemplate>
{
    public async Task<HttpResult> Handle(
        DeleteInvoiceTemplate request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
            return HttpResult.Unauthorized();

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var template = await db.InvoiceTemplates
            .FirstOrDefaultAsync(t => t.Id == request.Id && t.UserId == userId, cancellationToken);

        if (template == null)
            return HttpResult.NotFound();

        if (template.IsSystem)
            return HttpResult.NotFound();

        if (template.IsDefault)
            return HttpResult.NotFound();

        db.InvoiceTemplates.Remove(template);
        await db.SaveChangesAsync(cancellationToken);

        return HttpResult.Ok();
    }
}
