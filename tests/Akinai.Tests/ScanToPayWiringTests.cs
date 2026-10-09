using Akinai.Shared.Payments;
using FluentAssertions;

namespace Akinai.Tests;

/// <summary>
/// The rail-to-currency wiring the invoice uses. These guard the join between a stored
/// payee identifier and a scannable code — the payload formats themselves are covered in
/// PaymentQrPayloadTests.
/// </summary>
public class ScanToPayWiringTests
{
    [Fact]
    public void A_euro_invoice_with_an_IBAN_produces_a_SEPA_code()
    {
        var result = PaymentQrPayload.Build(PaymentRail.Epc, new PaymentQrRequest(
            PayeeName: "Acme Ltd",
            AccountIdentifier: "DE89370400440532013000",
            Amount: 1250.00m, CurrencyCode: "EUR", Reference: "INV-00042",
            BeneficiaryBic: "COBADEFFXXX"));

        result.IsAvailable.Should().BeTrue(result.UnavailableReason ?? "");
        QrImage.DataUri(result.Payload).Should().NotBeNull();
    }

    [Fact]
    public void A_rupee_invoice_with_a_UPI_id_produces_a_code()
    {
        var result = PaymentQrPayload.Build(PaymentRail.Upi, new PaymentQrRequest(
            PayeeName: "Acme Ltd", AccountIdentifier: "acme@okhdfcbank",
            Amount: 5000m, CurrencyCode: "INR", Reference: "INV-00042"));

        result.IsAvailable.Should().BeTrue(result.UnavailableReason ?? "");
    }

    [Fact]
    public void A_baht_invoice_with_a_PromptPay_id_produces_a_code()
    {
        var result = PaymentQrPayload.Build(PaymentRail.PromptPay, new PaymentQrRequest(
            AccountIdentifier: "0812345678",
            Amount: 4500m, CurrencyCode: "THB"));

        result.IsAvailable.Should().BeTrue(result.UnavailableReason ?? "");
    }

    [Fact]
    public void A_zero_amount_produces_nothing_rather_than_an_unscannable_code()
    {
        // This is what hid the QR on a seeded invoice whose totals had been zeroed: the
        // builder refused rather than drawing something that could not be paid.
        var result = PaymentQrPayload.Build(PaymentRail.Epc, new PaymentQrRequest(
            PayeeName: "Acme Ltd", AccountIdentifier: "DE89370400440532013000",
            Amount: 0m, CurrencyCode: "EUR", BeneficiaryBic: "COBADEFFXXX"));

        result.IsAvailable.Should().BeFalse();
    }

    [Fact]
    public void A_missing_payee_identifier_produces_nothing()
    {
        // The common real case: the currency has a rail, but nothing has been entered in
        // settings for it yet.
        var result = PaymentQrPayload.Build(PaymentRail.Pix, new PaymentQrRequest(
            PayeeName: "Acme Ltd", AccountIdentifier: null,
            Amount: 500m, CurrencyCode: "BRL"));

        result.IsAvailable.Should().BeFalse();
    }
}
