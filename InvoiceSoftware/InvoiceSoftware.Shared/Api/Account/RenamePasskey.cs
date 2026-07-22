using BlazorUtils.EasyApi;

namespace InvoiceSoftware.Shared.Api.Account;

/// <summary>Renames a passkey so it is recognisable in the list.</summary>
[Route("api/account/passkeys/{CredentialId}/rename")]
public class RenamePasskey : IPost
{
    [RouteParam] public string CredentialId { get; set; } = "";
    [BodyParam] public string Name { get; set; } = "";
}
