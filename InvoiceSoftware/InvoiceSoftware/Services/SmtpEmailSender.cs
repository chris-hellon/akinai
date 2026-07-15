using System.Net;
using System.Net.Mail;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Options;
using InvoiceSoftware.Infrastructure.Data;
using InvoiceSoftware.Infrastructure.Services;

namespace InvoiceSoftware.Services;

public class SmtpSettings
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromEmail { get; set; } = string.Empty;
    public string FromName { get; set; } = "InvoicePro";
    public bool EnableSsl { get; set; } = true;
}

public sealed class SmtpEmailSender : IEmailSender<ApplicationUser>, IEmailSender, IInvoiceEmailSender
{
    private readonly SmtpSettings _settings;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IOptions<SmtpSettings> settings, ILogger<SmtpEmailSender> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public Task SendEmailAsync(string email, string subject, string htmlMessage)
        => SendEmailAsync(email, subject, htmlMessage, null);

    public async Task SendEmailAsync(string email, string subject, string htmlMessage, IEnumerable<EmailAttachment>? attachments)
    {
        try
        {
            using var client = new SmtpClient(_settings.Host, _settings.Port)
            {
                Credentials = new NetworkCredential(_settings.Username, _settings.Password),
                EnableSsl = _settings.EnableSsl
            };

            using var mailMessage = new MailMessage
            {
                From = new MailAddress(_settings.FromEmail, _settings.FromName),
                Subject = subject,
                Body = htmlMessage,
                IsBodyHtml = true
            };
            mailMessage.To.Add(email);

            if (attachments != null)
            {
                foreach (var attachment in attachments)
                {
                    // Stream is owned by the Attachment and disposed with the MailMessage.
                    mailMessage.Attachments.Add(new Attachment(
                        new MemoryStream(attachment.Content), attachment.FileName, attachment.ContentType));
                }
            }

            await client.SendMailAsync(mailMessage);
            _logger.LogInformation("Email sent successfully to {Email}", email);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {Email}", email);
            throw;
        }
    }

    public Task SendConfirmationLinkAsync(ApplicationUser user, string email, string confirmationLink)
    {
        var subject = "Confirm your InvoicePro account";
        var body = BuildEmailTemplate(
            "Confirm Your Email",
            $"Hi {user.UserName ?? "there"},",
            "Thank you for registering with InvoicePro. Please confirm your email address by clicking the button below.",
            confirmationLink,
            "Confirm Email"
        );

        return SendEmailAsync(email, subject, body);
    }

    public Task SendPasswordResetLinkAsync(ApplicationUser user, string email, string resetLink)
    {
        var subject = "Reset your InvoicePro password";
        var body = BuildEmailTemplate(
            "Reset Your Password",
            $"Hi {user.UserName ?? "there"},",
            "We received a request to reset your password. Click the button below to create a new password. If you didn't request this, you can safely ignore this email.",
            resetLink,
            "Reset Password"
        );

        return SendEmailAsync(email, subject, body);
    }

    public Task SendPasswordResetCodeAsync(ApplicationUser user, string email, string resetCode)
    {
        var subject = "Your InvoicePro password reset code";
        var body = $"""
            <!DOCTYPE html>
            <html>
            <head>
                <meta charset="utf-8">
                <meta name="viewport" content="width=device-width, initial-scale=1.0">
            </head>
            <body style="margin: 0; padding: 0; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif; background-color: #f3f4f6;">
                <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="background-color: #f3f4f6; padding: 40px 20px;">
                    <tr>
                        <td align="center">
                            <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="max-width: 500px; background-color: #ffffff; border-radius: 12px; overflow: hidden; box-shadow: 0 4px 6px rgba(0, 0, 0, 0.05);">
                                <tr>
                                    <td style="background: linear-gradient(135deg, #0e5c66 0%, #147d87 100%); padding: 32px; text-align: center;">
                                        <h1 style="margin: 0; color: #ffffff; font-size: 24px; font-weight: 700;">InvoicePro</h1>
                                    </td>
                                </tr>
                                <tr>
                                    <td style="padding: 40px 32px;">
                                        <h2 style="margin: 0 0 16px; color: #111827; font-size: 20px; font-weight: 600;">Password Reset Code</h2>
                                        <p style="margin: 0 0 8px; color: #4b5563; font-size: 15px; line-height: 1.6;">Hi {user.UserName ?? "there"},</p>
                                        <p style="margin: 0 0 24px; color: #4b5563; font-size: 15px; line-height: 1.6;">Use the following code to reset your password:</p>
                                        <div style="background-color: #f3f4f6; border-radius: 8px; padding: 20px; text-align: center; margin-bottom: 24px;">
                                            <span style="font-size: 32px; font-weight: 700; letter-spacing: 4px; color: #0e5c66;">{resetCode}</span>
                                        </div>
                                        <p style="margin: 0; color: #6b7280; font-size: 13px; line-height: 1.6;">This code will expire in 15 minutes. If you didn't request this, you can safely ignore this email.</p>
                                    </td>
                                </tr>
                                <tr>
                                    <td style="background-color: #f9fafb; padding: 24px 32px; text-align: center; border-top: 1px solid #e5e7eb;">
                                        <p style="margin: 0; color: #9ca3af; font-size: 12px;">&copy; {DateTime.Now.Year} InvoicePro. All rights reserved.</p>
                                    </td>
                                </tr>
                            </table>
                        </td>
                    </tr>
                </table>
            </body>
            </html>
            """;

        return SendEmailAsync(email, subject, body);
    }

    private static string BuildEmailTemplate(string title, string greeting, string message, string buttonUrl, string buttonText)
    {
        return $"""
            <!DOCTYPE html>
            <html>
            <head>
                <meta charset="utf-8">
                <meta name="viewport" content="width=device-width, initial-scale=1.0">
            </head>
            <body style="margin: 0; padding: 0; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif; background-color: #f3f4f6;">
                <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="background-color: #f3f4f6; padding: 40px 20px;">
                    <tr>
                        <td align="center">
                            <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="max-width: 500px; background-color: #ffffff; border-radius: 12px; overflow: hidden; box-shadow: 0 4px 6px rgba(0, 0, 0, 0.05);">
                                <tr>
                                    <td style="background: linear-gradient(135deg, #0e5c66 0%, #147d87 100%); padding: 32px; text-align: center;">
                                        <h1 style="margin: 0; color: #ffffff; font-size: 24px; font-weight: 700;">InvoicePro</h1>
                                    </td>
                                </tr>
                                <tr>
                                    <td style="padding: 40px 32px;">
                                        <h2 style="margin: 0 0 16px; color: #111827; font-size: 20px; font-weight: 600;">{title}</h2>
                                        <p style="margin: 0 0 8px; color: #4b5563; font-size: 15px; line-height: 1.6;">{greeting}</p>
                                        <p style="margin: 0 0 24px; color: #4b5563; font-size: 15px; line-height: 1.6;">{message}</p>
                                        <table role="presentation" cellspacing="0" cellpadding="0" style="margin: 0 auto;">
                                            <tr>
                                                <td style="border-radius: 8px; background: linear-gradient(135deg, #0e5c66 0%, #147d87 100%);">
                                                    <a href="{buttonUrl}" style="display: inline-block; padding: 14px 32px; color: #ffffff; text-decoration: none; font-size: 15px; font-weight: 600;">{buttonText}</a>
                                                </td>
                                            </tr>
                                        </table>
                                        <p style="margin: 24px 0 0; color: #6b7280; font-size: 13px; line-height: 1.6;">If the button doesn't work, copy and paste this link into your browser:</p>
                                        <p style="margin: 8px 0 0; word-break: break-all;"><a href="{buttonUrl}" style="color: #0e5c66; font-size: 13px;">{buttonUrl}</a></p>
                                    </td>
                                </tr>
                                <tr>
                                    <td style="background-color: #f9fafb; padding: 24px 32px; text-align: center; border-top: 1px solid #e5e7eb;">
                                        <p style="margin: 0; color: #9ca3af; font-size: 12px;">&copy; {DateTime.Now.Year} InvoicePro. All rights reserved.</p>
                                    </td>
                                </tr>
                            </table>
                        </td>
                    </tr>
                </table>
            </body>
            </html>
            """;
    }
}
