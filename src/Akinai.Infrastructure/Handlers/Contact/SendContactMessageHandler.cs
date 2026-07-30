using System.Collections.Concurrent;
using System.Net;
using System.Text.Encodings.Web;
using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using Akinai.Infrastructure.Services;
using Akinai.Shared.Api.Contact;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Akinai.Infrastructure.Handlers.Contact;

/// <summary>
/// Delivers a contact-form message to the support inbox.
///
/// This is the only endpoint in the app that writes on behalf of someone who is
/// not signed in, which makes it the obvious thing to abuse — as an open relay,
/// as a spam target, or as a way to inject markup into an inbox. The three
/// defences are: a per-IP rate limit, a honeypot field, and HTML-encoding every
/// value before it goes anywhere near an email body.
/// </summary>
public class SendContactMessageHandler(
    IInvoiceEmailSender email,
    IOptions<ContactOptions> options,
    IHttpContextAccessor httpContextAccessor,
    ILogger<SendContactMessageHandler> logger)
    : IHandle<SendContactMessage, ContactResultDto>
{
    private readonly ContactOptions _options = options.Value;

    /// <summary>
    /// Recent submissions by IP. In-memory and per-instance, which is the right
    /// trade for a contact form: a shared store would be real infrastructure to
    /// slow down a nuisance, and behind several instances the worst case is a
    /// few extra messages rather than a breach.
    /// </summary>
    private static readonly ConcurrentDictionary<string, DateTimeOffset> LastSentByIp = new();

    private static readonly TimeSpan MinimumGap = TimeSpan.FromSeconds(60);

    public async Task<HttpResult<ContactResultDto>> Handle(
        SendContactMessage request, CancellationToken cancellationToken)
    {
        // Honeypot. Reported as sent so a bot has nothing to learn from the
        // response, and never delivered.
        if (!string.IsNullOrWhiteSpace(request.Website))
        {
            logger.LogInformation("Discarded a contact submission that filled the honeypot field.");
            return HttpResult<ContactResultDto>.Ok(new ContactResultDto(true));
        }

        var name = (request.Name ?? string.Empty).Trim();
        var from = (request.Email ?? string.Empty).Trim();
        var subject = (request.Subject ?? string.Empty).Trim();
        var message = (request.Message ?? string.Empty).Trim();

        if (name.Length is 0 or > 200
            || from.Length is 0 or > 320
            || subject.Length is 0 or > 200
            || message.Length is 0 or > 5000)
        {
            return HttpResult<ContactResultDto>.Ok(
                new ContactResultDto(false, "Please fill in every field, keeping your message under 5000 characters."));
        }

        if (!IsPlausibleEmail(from))
            return HttpResult<ContactResultDto>.Ok(new ContactResultDto(false, "That email address doesn't look right."));

        var ip = httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var now = DateTimeOffset.UtcNow;

        if (LastSentByIp.TryGetValue(ip, out var last) && now - last < MinimumGap)
        {
            return HttpResult<ContactResultDto>.Ok(
                new ContactResultDto(false, "You've just sent a message — please give it a minute before sending another."));
        }

        if (string.IsNullOrWhiteSpace(_options.InboxAddress))
        {
            // Configuration is missing, which is ours to fix, not the sender's
            // to work around. Logged loudly; reported as a plain failure.
            logger.LogError("A contact message arrived but Contact:InboxAddress is not configured.");
            return HttpResult<ContactResultDto>.Ok(
                new ContactResultDto(false, "Sorry — we couldn't send that just now. Please email us directly."));
        }

        // Every value is encoded before interpolation. Without this, a message
        // containing markup would render as markup in whoever opens the mail.
        var body = $"""
            <p><strong>From:</strong> {HtmlEncoder.Default.Encode(name)}
               &lt;{HtmlEncoder.Default.Encode(from)}&gt;</p>
            <p><strong>Subject:</strong> {HtmlEncoder.Default.Encode(subject)}</p>
            <hr />
            <p style="white-space:pre-wrap">{HtmlEncoder.Default.Encode(message)}</p>
            <hr />
            <p style="color:#888;font-size:12px">
              Sent from the Akinai contact form at {now:u}. Reply directly to reach the sender.
            </p>
            """;

        try
        {
            await email.SendEmailAsync(
                _options.InboxAddress,
                $"[Akinai contact] {subject}",
                body);
        }
        catch (Exception ex)
        {
            // Never surfaces the reason. A stranger does not need to know
            // whether our mail host is down or the address was rejected.
            logger.LogError(ex, "Could not deliver a contact message from {Email}.", from);
            return HttpResult<ContactResultDto>.Ok(
                new ContactResultDto(false, "Sorry — we couldn't send that just now. Please try again shortly."));
        }

        // Recorded only on success, so a failed attempt does not lock someone
        // out of retrying for a minute.
        LastSentByIp[ip] = now;
        Sweep(now);

        logger.LogInformation("Contact message delivered from {Email}.", from);
        return HttpResult<ContactResultDto>.Ok(new ContactResultDto(true));
    }

    /// <summary>
    /// Drops entries older than the window. Without it the dictionary grows for
    /// the life of the process — slowly, but for no reason.
    /// </summary>
    private static void Sweep(DateTimeOffset now)
    {
        if (LastSentByIp.Count < 512) return;

        foreach (var (key, when) in LastSentByIp)
        {
            if (now - when > MinimumGap)
                LastSentByIp.TryRemove(key, out _);
        }
    }

    /// <summary>
    /// A shape check, not a validity check — the only way to know an address
    /// works is to send to it, and this address is only ever used as a reply-to.
    /// </summary>
    private static bool IsPlausibleEmail(string value)
    {
        var at = value.IndexOf('@');
        if (at <= 0 || at == value.Length - 1) return false;
        if (value.IndexOf('@', at + 1) >= 0) return false;

        var domain = value[(at + 1)..];
        return domain.Contains('.') && !domain.StartsWith('.') && !domain.EndsWith('.');
    }
}

/// <summary>Where contact-form messages are delivered.</summary>
public class ContactOptions
{
    public const string SectionName = "Contact";

    /// <summary>
    /// The support inbox. Set in appsettings.json, which is gitignored — an
    /// address in source is an address that gets scraped.
    /// </summary>
    public string InboxAddress { get; set; } = string.Empty;
}
