using BlazorUtils.EasyApi;

namespace Akinai.Shared.Api.Attachments;

[Route("api/attachments/{Id}")]
public class DeleteAttachment : IDelete
{
    [RouteParam]
    public Guid Id { get; init; }
}
