using BlazorUtils.EasyApi;
using Akinai.Shared.Dtos.Attachments;

namespace Akinai.Shared.Api.Attachments;

[Route("api/attachments")]
public class GetAttachments : IGet<List<AttachmentDto>>
{
    [QueryStringParam]
    public string LinkedEntityType { get; init; } = null!;

    [QueryStringParam]
    public Guid LinkedEntityId { get; init; }
}
