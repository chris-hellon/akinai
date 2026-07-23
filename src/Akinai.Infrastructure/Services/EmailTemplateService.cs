using Akinai.Domain.Entities;
using System.Text;

namespace Akinai.Infrastructure.Services;

public class EmailTemplateService
{
    private const string InvoiceDetailsMarker = "%%INVOICE_DETAILS%%";

    public string GenerateInvoiceEmailHtml(
        Invoice invoice,
        decimal total,
        string companyName,
        string? emailBodyTemplate = null,
        string? publicViewUrl = null)
    {
        var currencySymbol = GetCurrencySymbol(invoice.Currency);

        // Use custom template or default
        var bodyTemplate = emailBodyTemplate ?? GetDefaultEmailBody();

        // First, replace {InvoiceDetails} with a marker (so it doesn't get wrapped in <p> tags)
        var contentWithMarker = bodyTemplate.Replace("{InvoiceDetails}", InvoiceDetailsMarker);

        // Replace all other placeholders
        var processedContent = contentWithMarker
            .Replace("{InvoiceNumber}", invoice.InvoiceNumber)
            .Replace("{ClientName}", invoice.Client.Name)
            .Replace("{Amount}", $"{currencySymbol}{total:N2}")
            .Replace("{DueDate}", invoice.DueDate.ToString("MMMM d, yyyy"))
            .Replace("{CompanyName}", companyName);

        // Convert plain text to HTML paragraphs
        var messageHtml = ConvertToHtmlParagraphs(processedContent);

        // Now replace the marker with the actual InvoiceDetails HTML
        var invoiceDetailsHtml = GenerateInvoiceDetailsHtml(currencySymbol, total, invoice.DueDate, publicViewUrl);
        messageHtml = messageHtml.Replace(InvoiceDetailsMarker, invoiceDetailsHtml);

        return GenerateFullEmailHtml(invoice, companyName, messageHtml);
    }

    private static string GetDefaultEmailBody()
    {
        return @"Dear {ClientName},

Please find attached invoice {InvoiceNumber} for {Amount}.

Payment is due by {DueDate}.

{InvoiceDetails}

Thank you for your business.";
    }

    private static string GenerateInvoiceDetailsHtml(string currencySymbol, decimal total, DateOnly dueDate, string? publicViewUrl)
    {
        // Hidden for now: there is no public invoice-viewing website yet, so the
        // "View Invoice Online" button would lead nowhere. Re-enable by restoring
        // `!string.IsNullOrEmpty(publicViewUrl)` once a public site exists.
        var hasViewLink = false;

        var viewButtonHtml = hasViewLink
            ? $@"<table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" style=""margin-top: 20px;"">
                            <tr>
                                <td align=""center"">
                                    <a href=""{publicViewUrl}"" style=""display: inline-block; padding: 12px 28px; background: linear-gradient(135deg, #147d87 0%, #0e5c66 100%); color: #ffffff; font-size: 14px; font-weight: 600; text-decoration: none; border-radius: 8px; box-shadow: 0 4px 14px rgba(14, 92, 102, 0.35);"">View Invoice Online</a>
                                </td>
                            </tr>
                        </table>"
            : "";

        return $@"<table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" style=""margin: 8px 0 16px 0;"">
    <tr>
        <td>
            <table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" style=""background: linear-gradient(135deg, #e7f2f3 0%, #c9e4e6 100%); border-radius: 12px; border: 1px solid #c9e4e6; border-collapse: separate; overflow: hidden;"">
                <tr>
                    <td style=""padding: 24px;"">
                        <table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"">
                            <tr>
                                <td>
                                    <p style=""margin: 0 0 4px 0; font-size: 13px; color: #6b7280; text-transform: uppercase; letter-spacing: 0.5px;"">Amount Due</p>
                                    <p style=""margin: 0; font-size: 32px; font-weight: 700; color: #0e5c66;"">{currencySymbol}{total:N2}</p>
                                </td>
                                <td align=""right"">
                                    <p style=""margin: 0 0 4px 0; font-size: 13px; color: #6b7280; text-transform: uppercase; letter-spacing: 0.5px;"">Due Date</p>
                                    <p style=""margin: 0; font-size: 18px; font-weight: 600; color: #111827;"">{dueDate:MMMM d, yyyy}</p>
                                </td>
                            </tr>
                        </table>{viewButtonHtml}
                    </td>
                </tr>
            </table>
        </td>
    </tr>
</table>";
    }

    private static string ConvertToHtmlParagraphs(string text)
    {
        var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        var result = new StringBuilder();
        var currentParagraph = new StringBuilder();

        foreach (var line in lines)
        {
            // Check if this line is just our marker
            if (line.Trim() == InvoiceDetailsMarker)
            {
                // Flush any current paragraph first
                if (currentParagraph.Length > 0)
                {
                    result.Append($@"<p style=""margin: 0 0 16px 0; font-size: 16px; color: #374151; line-height: 1.6;"">{currentParagraph}</p>");
                    currentParagraph.Clear();
                }
                // Add the marker as-is (will be replaced later)
                result.Append(InvoiceDetailsMarker);
            }
            else if (string.IsNullOrWhiteSpace(line))
            {
                // Empty line = end of paragraph
                if (currentParagraph.Length > 0)
                {
                    result.Append($@"<p style=""margin: 0 0 16px 0; font-size: 16px; color: #374151; line-height: 1.6;"">{currentParagraph}</p>");
                    currentParagraph.Clear();
                }
            }
            else
            {
                // Add line to current paragraph (with line break if not first line)
                if (currentParagraph.Length > 0)
                {
                    currentParagraph.Append("<br>");
                }
                currentParagraph.Append(line);
            }
        }

        // Flush any remaining paragraph
        if (currentParagraph.Length > 0)
        {
            result.Append($@"<p style=""margin: 0 0 16px 0; font-size: 16px; color: #374151; line-height: 1.6;"">{currentParagraph}</p>");
        }

        return result.ToString();
    }

    private static string GenerateFullEmailHtml(Invoice invoice, string companyName, string messageHtml)
    {
        return $@"<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Invoice {invoice.InvoiceNumber} from {companyName}</title>
</head>
<body style=""margin: 0; padding: 0; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif; background-color: #f3f4f6;"">
    <table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" style=""background-color: #f3f4f6;"">
        <tr>
            <td align=""center"" style=""padding: 40px 20px;"">
                <table role=""presentation"" width=""600"" cellspacing=""0"" cellpadding=""0"" style=""background-color: #ffffff; border-radius: 12px; box-shadow: 0 4px 6px rgba(0, 0, 0, 0.05);"">
                    <!-- Header -->
                    <tr>
                        <td style=""padding: 40px 40px 30px 40px; border-bottom: 1px solid #e5e7eb;"">
                            <table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"">
                                <tr>
                                    <td>
                                        <h1 style=""margin: 0 0 8px 0; font-size: 24px; font-weight: 700; color: #111827;"">{companyName}</h1>
                                        <p style=""margin: 0; font-size: 14px; color: #6b7280;"">Invoice {invoice.InvoiceNumber}</p>
                                    </td>
                                    <td align=""right"">
                                        <span style=""display: inline-block; padding: 6px 16px; background: linear-gradient(135deg, #e7f2f3 0%, #c9e4e6 100%); color: #0e5c66; font-size: 12px; font-weight: 600; border-radius: 20px; text-transform: uppercase; letter-spacing: 0.5px;"">Invoice</span>
                                    </td>
                                </tr>
                            </table>
                        </td>
                    </tr>

                    <!-- Message Content -->
                    <tr>
                        <td style=""padding: 30px 40px 20px 40px;"">
                            {messageHtml}
                        </td>
                    </tr>

                    <!-- Footer -->
                    <tr>
                        <td style=""padding: 20px 40px; background-color: #f9fafb; border-top: 1px solid #e5e7eb; border-radius: 0 0 12px 12px;"">
                            <p style=""margin: 0; font-size: 12px; color: #9ca3af; text-align: center;"">This email was sent regarding invoice {invoice.InvoiceNumber}. Payment is due by {invoice.DueDate:MMMM d, yyyy}.</p>
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>";
    }

    private static string GetCurrencySymbol(string currencyCode) => currencyCode switch
    {
        "USD" => "$",
        "GBP" => "£",
        "EUR" => "€",
        "AUD" => "A$",
        "CAD" => "C$",
        "VND" => "₫",
        _ => currencyCode
    };
}
