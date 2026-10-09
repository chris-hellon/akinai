using QRCoder;

namespace Akinai.Shared.Payments;

/// <summary>
/// Turns a payment payload into a QR code, as an inline SVG data URI.
///
/// Generated here rather than fetched from an image service, which is how the VietQR code
/// used to work. Three reasons, in order of how much they matter:
///
/// <list type="number">
/// <item>An invoice is confidential. Calling a third party to draw its QR hands them the
/// amount, the invoice number and the payee's account name, on every render — including
/// when the client opens their copy, and including inside the PDF.</item>
/// <item>A remote image fails quietly. Printed into a PDF over a slow or blocked
/// connection it is simply absent, and nobody finds out until a client cannot pay.</item>
/// <item>Most rails have no such service anyway, so anything beyond VietQR had to be
/// generated locally regardless.</item>
/// </list>
///
/// SVG rather than PNG because it stays sharp in the printed PDF at any size, and embeds
/// in the page without a second request.
/// </summary>
public static class QrImage
{
    /// <summary>
    /// An SVG data URI for <paramref name="payload"/>, or null when there is nothing to
    /// encode — callers render no QR at all rather than an empty box.
    /// </summary>
    /// <param name="payload">The rail-specific payment string.</param>
    public static string? DataUri(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload)) return null;

        // ECC level M: the usual trade-off for a printed payment code. Higher correction
        // survives a worse scan but packs the modules tighter, which hurts more on a phone
        // camera pointed at a screen than the extra redundancy helps.
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.M);

        var svg = new SvgQRCode(data).GetGraphic(
            pixelsPerModule: 4,
            darkColorHex: "#000000",
            lightColorHex: "#ffffff",
            drawQuietZones: true);

        // Base64 rather than raw, so the markup never has to escape the SVG's own quotes.
        var bytes = System.Text.Encoding.UTF8.GetBytes(svg);
        return $"data:image/svg+xml;base64,{Convert.ToBase64String(bytes)}";
    }
}
