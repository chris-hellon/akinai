using BlazorUtils.EasyApi;
using Akinai.Shared.Dtos.Account;

namespace Akinai.Shared.Api.Account;

/// <summary>
/// Registers the credential the browser produced. <see cref="CredentialJson"/>
/// is the serialised PublicKeyCredential; the server verifies its attestation
/// against the challenge it issued earlier before storing anything.
/// </summary>
[Route("api/account/passkeys/add")]
public class AddPasskey : IPost<AddPasskeyResultDto>
{
    [BodyParam] public string CredentialJson { get; set; } = "";
}
