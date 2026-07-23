using BlazorUtils.EasyApi;
using Akinai.Shared.Dtos.Signatures;

namespace Akinai.Shared.Api.Signatures;

[Route("api/signatures")]
public class GetSignature : IGet<SignatureDto?>
{
    [QueryStringParam]
    public string LinkedEntityType { get; init; } = null!;

    [QueryStringParam]
    public Guid LinkedEntityId { get; init; }
}
