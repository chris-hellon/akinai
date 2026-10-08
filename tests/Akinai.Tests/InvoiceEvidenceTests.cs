using Akinai.Domain.Services;
using Akinai.Infrastructure.Handlers.Invoices;
using Akinai.Shared.Api.Invoices;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Akinai.Tests;

public class InvoiceEvidenceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public void A_fingerprint_is_stable_for_the_same_bytes()
    {
        byte[] document = [1, 2, 3, 4, 5];

        DocumentFingerprint.Of(document).Should().Be(DocumentFingerprint.Of([1, 2, 3, 4, 5]));
    }

    [Fact]
    public void A_single_changed_byte_changes_the_fingerprint()
    {
        // The whole value of storing it: an altered document cannot present itself as the
        // one that was sent.
        DocumentFingerprint.Of([1, 2, 3, 4, 5])
            .Should().NotBe(DocumentFingerprint.Of([1, 2, 3, 4, 6]));
    }

    [Fact]
    public void An_empty_document_has_no_fingerprint()
    {
        DocumentFingerprint.Of([]).Should().BeNull();
        DocumentFingerprint.Of(null).Should().BeNull();
    }

    [Fact]
    public void A_fingerprint_is_sixty_four_hex_characters()
    {
        DocumentFingerprint.Of([9, 9, 9])
            .Should().MatchRegex("^[0-9a-f]{64}$");
    }

    [Fact]
    public async Task An_unsent_invoice_has_an_empty_delivery_record()
    {
        using var ctx = new InvoiceTestContext();
        await ctx.SeedAsync(entryDays: [(1, 2m)]);

        var result = await new GetInvoiceEvidenceHandler(ctx.Factory, ctx.CurrentUser)
            .Handle(new GetInvoiceEvidence { Id = ctx.InvoiceId }, Ct);

        result.Response!.SentAtUtc.Should().BeNull();
        result.Response.SentDocumentSha256.Should().BeNull();
        result.Response.ViewCount.Should().Be(0);
    }

    [Fact]
    public async Task A_recorded_delivery_is_reported_back_intact()
    {
        using var ctx = new InvoiceTestContext();
        await ctx.SeedAsync(entryDays: [(1, 2m)]);

        var sentAt = new DateTime(2026, 10, 7, 9, 30, 0, DateTimeKind.Utc);
        var fingerprint = DocumentFingerprint.Of([42, 42, 42])!;

        await using (var db = ctx.NewContext())
        {
            var invoice = await db.Invoices.SingleAsync(i => i.Id == ctx.InvoiceId, Ct);
            invoice.RecordDelivery(sentAt, "client@example.com", fingerprint);
            await db.SaveChangesAsync(Ct);
        }

        var result = await new GetInvoiceEvidenceHandler(ctx.Factory, ctx.CurrentUser)
            .Handle(new GetInvoiceEvidence { Id = ctx.InvoiceId }, Ct);

        result.Response!.SentAtUtc.Should().Be(sentAt);
        result.Response.SentToEmail.Should().Be("client@example.com");
        result.Response.SentDocumentSha256.Should().Be(fingerprint);
    }

    [Fact]
    public async Task Another_users_invoice_is_not_readable()
    {
        using var ctx = new InvoiceTestContext();
        await ctx.SeedAsync(entryDays: [(1, 2m)]);

        var result = await new GetInvoiceEvidenceHandler(ctx.Factory, new InvoiceTestContext.OtherUser())
            .Handle(new GetInvoiceEvidence { Id = ctx.InvoiceId }, Ct);

        result.Response.Should().BeNull();
    }
}
