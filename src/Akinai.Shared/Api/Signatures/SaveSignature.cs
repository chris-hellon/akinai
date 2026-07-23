using BlazorUtils.EasyApi;

namespace Akinai.Shared.Api.Signatures;

[Route("api/signatures")]
public class SaveSignature : IPost<Guid>
{
    [BodyParam]
    public string LinkedEntityType { get; init; } = null!;

    [BodyParam]
    public Guid LinkedEntityId { get; init; }

    [BodyParam]
    public byte[] SignatureData { get; init; } = null!;

    [BodyParam]
    public string SignerName { get; init; } = null!;

    [BodyParam]
    public string? SignerEmail { get; init; }
}
