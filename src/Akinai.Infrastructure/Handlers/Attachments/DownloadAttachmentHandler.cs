using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using Akinai.Infrastructure.Data;
using Akinai.Shared.Api.Attachments;
using Akinai.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace Akinai.Infrastructure.Handlers.Attachments;

public class DownloadAttachmentHandler(IDbContextFactory<ApplicationDbContext> dbFactory, ICurrentUserService currentUserService)
    : IHandle<DownloadAttachment, byte[]?>
{
    public async Task<HttpResult<byte[]?>> Handle(DownloadAttachment request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
            return HttpResult<byte[]?>.Unauthorized();

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var attachment = await db.Attachments
            .FirstOrDefaultAsync(a => a.Id == request.Id && a.UserId == userId, cancellationToken);

        if (attachment == null)
            return HttpResult<byte[]?>.NotFound();

        return HttpResult<byte[]?>.Ok(attachment.FileData);
    }
}
