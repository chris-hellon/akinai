using BlazorUtils.EasyApi;

namespace InvoiceSoftware.Shared.Api.Account;

/// <summary>Removes a passkey from the account.</summary>
[Route("api/account/passkeys/{CredentialId}/delete")]
public class DeletePasskey : IPost
{
    [RouteParam] public string CredentialId { get; set; } = "";
}
