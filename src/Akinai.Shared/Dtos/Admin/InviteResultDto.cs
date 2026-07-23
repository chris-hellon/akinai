namespace Akinai.Shared.Dtos.Admin;

/// <summary>
/// Result of inviting a user.
///
/// <see cref="SetPasswordLink"/> is returned so the admin has a fallback when
/// the invite email doesn't arrive (spam, typo, SMTP down). It is a one-time
/// token URL — treat it like a credential and send it over a trusted channel.
/// </summary>
public record InviteResultDto(
    string UserId,
    string Email,
    bool EmailSent,
    string? EmailError,
    string SetPasswordLink);
