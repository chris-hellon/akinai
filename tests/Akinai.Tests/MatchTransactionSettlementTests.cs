using Akinai.Domain.Entities;
using Akinai.Infrastructure.Handlers.BankImport;
using Akinai.Shared.Api.BankImport;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Akinai.Tests;

/// <summary>
/// End to end through the match handler: the classification has to actually be written
/// when a payment is matched, not merely be computable.
/// </summary>
public class MatchTransactionSettlementTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static MatchTransactionHandler Handler(InvoiceTestContext ctx) =>
        new(ctx.Factory, ctx.CurrentUser,
            new InvoiceTestContext.NoNotifications(),
            new InvoiceTestContext.ParityRates());

    private static async Task<Guid> AddCreditAsync(InvoiceTestContext ctx, decimal amount, string currency = "GBP")
    {
        await using var db = ctx.NewContext();
        var tx = BankTransaction.Create(
            InvoiceTestContext.UserId, new DateOnly(2026, 10, 1), "CLIENT PAYMENT",
            amount, currency, "statement.csv", 1);
        db.BankTransactions.Add(tx);
        await db.SaveChangesAsync(Ct);
        return tx.Id;
    }

    [Fact]
    public async Task A_ten_percent_shortfall_from_India_is_recorded_as_withholding_not_a_debt()
    {
        // 2h + 3h at 100/hr with no tax = 500 invoiced. 450 received is exactly 10% short,
        // which is India's withholding rate on service fees. Chasing this client would be
        // chasing them for tax they have already remitted on your behalf.
        using var ctx = new InvoiceTestContext();
        await ctx.SeedAsync(100m, 0m, sellerCountry: "GB", buyerCountry: "IN",
            sellerTaxNumber: "GB980780684", buyerTaxNumber: null, entryDays: [(1, 2m), (3, 3m)]);
        var txId = await AddCreditAsync(ctx, 450m);

        var result = await Handler(ctx).Handle(
            new MatchTransaction { Id = txId, InvoiceId = ctx.InvoiceId, MarkInvoiceAsPaid = true }, Ct);

        result.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);

        await using var db = ctx.NewContext();
        var tx = await db.BankTransactions.SingleAsync(b => b.Id == txId, Ct);
        tx.SettlementCause.Should().Be("WithholdingTax");
        tx.WithholdingPercentage.Should().Be(0.10m);
        tx.WithholdingAmount.Should().Be(50m);
        tx.UnexplainedAmount.Should().Be(0m, "nothing is actually outstanding");
        tx.WithholdingCertificateReceived.Should().BeFalse("the certificate is still owed");
    }

    [Fact]
    public async Task An_exact_payment_is_recorded_as_an_exact_match()
    {
        using var ctx = new InvoiceTestContext();
        await ctx.SeedAsync(100m, 0m, sellerCountry: "GB", buyerCountry: "FR",
            sellerTaxNumber: "GB980780684", buyerTaxNumber: "FR12345678901",
            entryDays: [(1, 2m), (3, 3m)]);
        var txId = await AddCreditAsync(ctx, 500m);

        await Handler(ctx).Handle(
            new MatchTransaction { Id = txId, InvoiceId = ctx.InvoiceId, MarkInvoiceAsPaid = true }, Ct);

        await using var db = ctx.NewContext();
        var tx = await db.BankTransactions.SingleAsync(b => b.Id == txId, Ct);
        tx.SettlementCause.Should().Be("ExactMatch");
        tx.WithholdingPercentage.Should().BeNull();
    }

    [Fact]
    public async Task A_genuine_shortfall_is_left_unexplained_so_it_can_be_chased()
    {
        // 380 against 500 matches no rule — this is the one case where the client really
        // does still owe money, and it must not be dressed up as anything else.
        using var ctx = new InvoiceTestContext();
        await ctx.SeedAsync(100m, 0m, sellerCountry: "GB", buyerCountry: "FR",
            sellerTaxNumber: "GB980780684", buyerTaxNumber: "FR12345678901",
            entryDays: [(1, 2m), (3, 3m)]);
        var txId = await AddCreditAsync(ctx, 380m);

        await Handler(ctx).Handle(
            new MatchTransaction { Id = txId, InvoiceId = ctx.InvoiceId, MarkInvoiceAsPaid = false }, Ct);

        await using var db = ctx.NewContext();
        var tx = await db.BankTransactions.SingleAsync(b => b.Id == txId, Ct);
        tx.SettlementCause.Should().Be("UnexplainedShortfall");
        tx.UnexplainedAmount.Should().BeGreaterThan(0m);
    }

    [Fact]
    public async Task The_withholding_certificate_can_be_recorded_through_the_handler()
    {
        using var ctx = new InvoiceTestContext();
        await ctx.SeedAsync(100m, 0m, sellerCountry: "GB", buyerCountry: "IN",
            sellerTaxNumber: "GB980780684", buyerTaxNumber: null, entryDays: [(1, 2m), (3, 3m)]);
        var txId = await AddCreditAsync(ctx, 450m);
        await Handler(ctx).Handle(
            new MatchTransaction { Id = txId, InvoiceId = ctx.InvoiceId, MarkInvoiceAsPaid = true }, Ct);

        var result = await new RecordWithholdingCertificateHandler(ctx.Factory, ctx.CurrentUser)
            .Handle(new RecordWithholdingCertificate { Id = txId }, Ct);

        result.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);

        await using var db = ctx.NewContext();
        var tx = await db.BankTransactions.SingleAsync(b => b.Id == txId, Ct);
        tx.WithholdingCertificateReceived.Should().BeTrue();
    }

    [Fact]
    public async Task Recording_a_certificate_on_a_payment_with_no_withholding_is_rejected()
    {
        using var ctx = new InvoiceTestContext();
        await ctx.SeedAsync(100m, 0m, sellerCountry: "GB", buyerCountry: "FR",
            sellerTaxNumber: "GB980780684", buyerTaxNumber: "FR12345678901",
            entryDays: [(1, 2m), (3, 3m)]);
        var txId = await AddCreditAsync(ctx, 500m);
        await Handler(ctx).Handle(
            new MatchTransaction { Id = txId, InvoiceId = ctx.InvoiceId, MarkInvoiceAsPaid = true }, Ct);

        var result = await new RecordWithholdingCertificateHandler(ctx.Factory, ctx.CurrentUser)
            .Handle(new RecordWithholdingCertificate { Id = txId }, Ct);

        result.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Another_users_transaction_cannot_have_a_certificate_recorded()
    {
        using var ctx = new InvoiceTestContext();
        await ctx.SeedAsync(entryDays: [(1, 2m)]);
        var txId = await AddCreditAsync(ctx, 450m);

        var result = await new RecordWithholdingCertificateHandler(
                ctx.Factory, new InvoiceTestContext.OtherUser())
            .Handle(new RecordWithholdingCertificate { Id = txId }, Ct);

        result.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound);
    }
}
