using Akinai.Domain.Entities;
using Akinai.Domain.Enums;
using Akinai.Domain.Exceptions;
using FluentAssertions;

namespace Akinai.Tests;

/// <summary>
/// The settlement analysis stored against a matched payment. The rule that matters is
/// that a withheld payment is never confused with a short one — chasing a client for tax
/// they have already remitted on your behalf is the failure this exists to prevent.
/// </summary>
public class PaymentSettlementTests
{
    private static BankTransaction Credit(decimal amount = 900m, string currency = "GBP") =>
        BankTransaction.Create(
            userId: "user-1",
            transactionDate: new DateOnly(2026, 10, 1),
            description: "FUSE HOSTELS PAYMENT",
            amount: amount,
            currency: currency,
            sourceFileName: "statement.csv",
            sourceRowNumber: 1);

    [Fact]
    public void A_fresh_transaction_has_no_settlement_analysis()
    {
        var tx = Credit();

        tx.SettlementCause.Should().BeNull();
        tx.WithholdingCertificateReceived.Should().BeFalse();
    }

    [Fact]
    public void Recording_a_settlement_stores_what_the_classifier_found()
    {
        var tx = Credit();
        tx.MatchToInvoice(Guid.NewGuid(), MatchConfidence.High);

        tx.RecordSettlement("WithholdingTax", 0.10m, 100m, null, 0m);

        tx.SettlementCause.Should().Be("WithholdingTax");
        tx.WithholdingPercentage.Should().Be(0.10m);
        tx.WithholdingAmount.Should().Be(100m);
        tx.UnexplainedAmount.Should().Be(0m);
    }

    [Fact]
    public void A_withholding_certificate_can_be_recorded_once_tax_was_withheld()
    {
        var tx = Credit();
        tx.MatchToInvoice(Guid.NewGuid(), MatchConfidence.High);
        tx.RecordSettlement("WithholdingTax", 0.10m, 100m, null, 0m);

        tx.MarkWithholdingCertificateReceived();

        tx.WithholdingCertificateReceived.Should().BeTrue();
    }

    [Fact]
    public void A_certificate_cannot_be_recorded_when_no_tax_was_withheld()
    {
        // Otherwise the year-end view would show certificates outstanding for payments
        // that never had any tax taken.
        var tx = Credit();
        tx.MatchToInvoice(Guid.NewGuid(), MatchConfidence.High);
        tx.RecordSettlement("FxSpread", null, null, -17.53m, 0m);

        var record = () => tx.MarkWithholdingCertificateReceived();

        record.Should().Throw<DomainException>();
    }

    [Fact]
    public void Unmatching_clears_the_analysis()
    {
        // It described this payment against that invoice. Left behind it would be stale,
        // and worse, believed.
        var tx = Credit();
        tx.MatchToInvoice(Guid.NewGuid(), MatchConfidence.High);
        tx.RecordSettlement("WithholdingTax", 0.10m, 100m, null, 0m);
        tx.MarkWithholdingCertificateReceived();

        tx.UnmatchInvoice();

        tx.SettlementCause.Should().BeNull();
        tx.WithholdingPercentage.Should().BeNull();
        tx.WithholdingAmount.Should().BeNull();
        tx.WithholdingCertificateReceived.Should().BeFalse();
        tx.FxGainOrLoss.Should().BeNull();
        tx.UnexplainedAmount.Should().BeNull();
    }
}
