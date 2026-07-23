using BlazorUtils.EasyApi;

namespace Akinai.Shared.Api.Attachments;

[Route("api/attachments")]
public class UploadAttachment : IPost<Guid>
{
    [BodyParam]
    public string FileName { get; init; } = null!;

    [BodyParam]
    public string ContentType { get; init; } = null!;

    [BodyParam]
    public byte[] FileData { get; init; } = null!;

    [BodyParam]
    public string LinkedEntityType { get; init; } = null!;

    [BodyParam]
    public Guid LinkedEntityId { get; init; }

    [BodyParam]
    public string? Description { get; init; }
}
