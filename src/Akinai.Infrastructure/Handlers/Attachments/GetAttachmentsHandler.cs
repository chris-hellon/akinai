using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using Akinai.Domain.Enums;
using Akinai.Infrastructure.Data;
using Akinai.Shared.Api.Attachments;
using Akinai.Shared.Dtos.Attachments;
using Akinai.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace Akinai.Infrastructure.Handlers.Attachments;

public class GetAttachmentsHandler(IDbContextFactory<ApplicationDbContext> dbFactory, ICurrentUserService currentUserService)
    : IHandle<GetAttachments, List<AttachmentDto>>
{
    public async Task<HttpResult<List<AttachmentDto>>> Handle(GetAttachments request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
            return HttpResult<List<AttachmentDto>>.Unauthorized();

        if (!Enum.TryParse<LinkedEntityType>(request.LinkedEntityType, out var entityType))
            return HttpResult<List<AttachmentDto>>.Ok(new List<AttachmentDto>());

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var attachments = await db.Attachments
            .Where(a => a.UserId == userId &&
                       a.LinkedEntityType == entityType &&
                       a.LinkedEntityId == request.LinkedEntityId)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(cancellationToken);

        var items = attachments.Select(a => new AttachmentDto(
            a.Id,
            a.FileName,
            a.FileSize,
            a.GetFormattedFileSize(),
            a.ContentType,
            a.LinkedEntityType.ToString(),
            a.LinkedEntityId,
            a.Description,
            a.CreatedAt)).ToList();

        return HttpResult<List<AttachmentDto>>.Ok(items);
    }
}
