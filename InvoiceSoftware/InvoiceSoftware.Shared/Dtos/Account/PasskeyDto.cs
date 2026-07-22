namespace InvoiceSoftware.Shared.Dtos.Account;

/// <summary>
/// A registered passkey, as shown in the account area.
///
/// <paramref name="CredentialId"/> is the raw credential id in base64url form —
/// the same encoding WebAuthn uses on the wire, so it is safe in a URL and can
/// be round-tripped back to bytes for lookups.
/// </summary>
public record PasskeyDto(string CredentialId, string? Name, DateTimeOffset CreatedAt);

/// <summary>
/// Outcome of registering a passkey.
///
/// Always returned with a 200 so the browser's own failure text (which is the
/// only useful diagnostic when an authenticator refuses) reaches the page
/// instead of being flattened into a bare 400.
/// </summary>
public record AddPasskeyResultDto(bool Succeeded, string? Error, PasskeyDto? Passkey);
