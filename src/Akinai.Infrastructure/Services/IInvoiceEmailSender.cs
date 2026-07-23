namespace Akinai.Infrastructure.Services;

/// <summary>A file to attach to an outgoing email.</summary>
public sealed record EmailAttachment(string FileName, byte[] Content, string ContentType);

/// <summary>
/// Sends application emails (e.g. invoices) that may carry attachments.
/// Separate from Identity's <see cref="Microsoft.AspNetCore.Identity.UI.Services.IEmailSender"/>
/// because that interface has no attachment support.
/// </summary>
public interface IInvoiceEmailSender
{
    Task SendEmailAsync(string to, string subject, string htmlBody, IEnumerable<EmailAttachment>? attachments = null);
}
