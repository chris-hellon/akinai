using BlazorUtils.EasyApi;

namespace Akinai.Shared.Api.Account;

/// <summary>
/// Issues the WebAuthn creation options (challenge, relying party, excluded
/// credentials) that the browser needs before it can mint a passkey.
///
/// Returns the options as a raw JSON string, because that is exactly what
/// <c>PublicKeyCredential.parseCreationOptionsFromJSON</c> expects — parsing
/// and re-serialising it through a DTO would only risk losing fields.
/// </summary>
[Route("api/account/passkeys/creation-options")]
public class GetPasskeyCreationOptions : IPost<string>;
