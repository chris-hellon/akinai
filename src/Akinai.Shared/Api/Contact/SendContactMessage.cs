using BlazorUtils.EasyApi;

namespace Akinai.Shared.Api.Contact;

/// <summary>
/// A message from the public contact form.
///
/// Anonymous by necessity — the people most likely to need it are the ones who
/// cannot sign in. That makes it the one unauthenticated write in the app, so
/// the handler rate-limits by IP and carries a honeypot field.
/// </summary>
[Route("api/contact")]
public class SendContactMessage : IPost<ContactResultDto>
{
    [BodyParam] public string Name { get; set; } = string.Empty;

    [BodyParam] public string Email { get; set; } = string.Empty;

    [BodyParam] public string Subject { get; set; } = string.Empty;

    [BodyParam] public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Honeypot. Hidden from people by CSS and left empty by them; bots that
    /// fill every input give themselves away. A filled value is accepted and
    /// silently discarded, because telling a bot it failed only teaches it.
    ///
    /// Non-nullable with a default despite being optional in spirit: EasyApi
    /// requires every [BodyParam] key to be present in the body and throws if
    /// one is missing, so a nullable type here would advertise an optionality
    /// the binder does not actually allow.
    /// </summary>
    [BodyParam] public string Website { get; set; } = string.Empty;
}

/// <summary>
/// Deliberately vague on failure. The form should not become a way to probe
/// whether mail is configured or which addresses exist.
/// </summary>
public record ContactResultDto(bool Sent, string? Error = null);
