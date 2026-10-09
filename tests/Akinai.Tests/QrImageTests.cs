using Akinai.Shared.Payments;
using FluentAssertions;

namespace Akinai.Tests;

public class QrImageTests
{
    [Fact]
    public void A_payload_becomes_an_inline_svg_data_uri()
    {
        var uri = QrImage.DataUri("BCD\n002\n1\nSCT\n\nAcme Ltd\nGB82WEST12345698765432");

        uri.Should().StartWith("data:image/svg+xml;base64,");
    }

    [Fact]
    public void The_data_uri_decodes_to_real_svg()
    {
        var uri = QrImage.DataUri("hello")!;
        var svg = System.Text.Encoding.UTF8.GetString(
            Convert.FromBase64String(uri["data:image/svg+xml;base64,".Length..]));

        svg.Should().StartWith("<svg").And.Contain("</svg>");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Nothing_to_encode_produces_no_image(string? payload)
    {
        // Callers render no QR at all rather than an empty box.
        QrImage.DataUri(payload).Should().BeNull();
    }

    [Fact]
    public void Different_payloads_produce_different_codes()
    {
        QrImage.DataUri("invoice-1").Should().NotBe(QrImage.DataUri("invoice-2"));
    }

    [Fact]
    public void The_same_payload_is_stable()
    {
        // Stability matters for the PDF: a code that differs between the on-screen invoice
        // and the printed one would be impossible to reason about in a dispute.
        QrImage.DataUri("invoice-1").Should().Be(QrImage.DataUri("invoice-1"));
    }

    [Fact]
    public void A_long_payload_still_encodes()
    {
        // EPC allows 140 characters of remittance text on top of everything else.
        var payload = new string('A', 300);

        QrImage.DataUri(payload).Should().NotBeNull();
    }
}
