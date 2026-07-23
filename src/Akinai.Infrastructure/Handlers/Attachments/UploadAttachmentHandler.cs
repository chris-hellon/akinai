using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using Akinai.Domain.Entities;
using Akinai.Domain.Enums;
using Akinai.Infrastructure.Data;
using Akinai.Shared.Api.Attachments;
using Akinai.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace Akinai.Infrastructure.Handlers.Attachments;

public class UploadAttachmentHandler(IDbContextFactory<ApplicationDbContext> dbFactory, ICurrentUserService currentUserService)
    : IHandle<UploadAttachment, Guid>
{
    public async Task<HttpResult<Guid>> Handle(UploadAttachment request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
            return HttpResult<Guid>.Unauthorized();

        if (!Enum.TryParse<LinkedEntityType>(request.LinkedEntityType, out var entityType))
            return HttpResult<Guid>.NotFound();

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        try
        {
            var attachment = Attachment.Create(
                userId,
                request.FileName,
                request.ContentType,
                request.FileData,
                entityType,
                request.LinkedEntityId,
                request.Description);

            db.Attachments.Add(attachment);
            await db.SaveChangesAsync(cancellationToken);
            return HttpResult<Guid>.Created(attachment.Id);
        }
        catch (Domain.Exceptions.DomainException)
        {
            return HttpResult<Guid>.NotFound();
        }
    }
}
