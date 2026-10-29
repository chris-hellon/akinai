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

        var result = await new GetInvoiceEvidenceHandler(ctx.Factory, ctx.CurrentUser, InvoiceTestContext.FakePlan.Pro)
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

        var result = await new GetInvoiceEvidenceHandler(ctx.Factory, ctx.CurrentUser, InvoiceTestContext.FakePlan.Pro)
            .Handle(new GetInvoiceEvidence { Id = ctx.InvoiceId }, Ct);

        result.Response!.SentAtUtc.Should().Be(sentAt);
        result.Response.SentToEmail.Should().Be("client@example.com");
        result.Response.SentDocumentSha256.Should().Be(fingerprint);
    }

    [Fact]
    public async Task Reminders_appear_on_the_record_oldest_first()
    {
        using var ctx = new InvoiceTestContext();
        await ctx.SeedAsync(entryDays: [(1, 2m)]);

        await using (var db = ctx.NewContext())
        {
            // Inserted out of order on purpose: the record has to read as the chase
            // happened, and insertion order is not send order once a sweep catches up on
            // several invoices at once.
            db.DunningReminders.Add(Akinai.Domain.Entities.DunningReminder.Record(
                ctx.InvoiceId, ctx.ClientId, InvoiceTestContext.UserId, "FirmReminder",
                new DateTime(2026, 8, 14, 9, 0, 0, DateTimeKind.Utc), "ap@acme.test", "English"));
            db.DunningReminders.Add(Akinai.Domain.Entities.DunningReminder.Record(
                ctx.InvoiceId, ctx.ClientId, InvoiceTestContext.UserId, "GentleReminder",
                new DateTime(2026, 8, 3, 9, 0, 0, DateTimeKind.Utc), "ap@acme.test", "English"));
            await db.SaveChangesAsync(Ct);
        }

        var result = await new GetInvoiceEvidenceHandler(ctx.Factory, ctx.CurrentUser, InvoiceTestContext.FakePlan.Pro)
            .Handle(new GetInvoiceEvidence { Id = ctx.InvoiceId }, Ct);

        result.Response!.Reminders.Select(r => r.Stage)
            .Should().Equal("GentleReminder", "FirmReminder");
        result.Response.Reminders[0].ToEmail.Should().Be("ap@acme.test");
        result.Response.Reminders[0].SentAtUtc.Should().Be(new DateTime(2026, 8, 3, 9, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task Another_invoices_reminders_stay_off_this_record()
    {
        using var ctx = new InvoiceTestContext();
        await ctx.SeedAsync(entryDays: [(1, 2m)]);

        await using (var db = ctx.NewContext())
        {
            var other = Akinai.Domain.Entities.Invoice.Create(
                "INV-00002", ctx.ClientId,
                new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31), taxRate: 20m, currency: "GBP");
            db.Invoices.Add(other);
            await db.SaveChangesAsync(Ct);

            // Same client, different invoice. The reminder carries a denormalised ClientId,
            // so filtering on the wrong one would pull a sibling invoice's chase onto this
            // record and overstate how often this client was pursued over this bill.
            db.DunningReminders.Add(Akinai.Domain.Entities.DunningReminder.Record(
                other.Id, ctx.ClientId, InvoiceTestContext.UserId, "FinalNotice",
                new DateTime(2026, 9, 1, 9, 0, 0, DateTimeKind.Utc), "ap@acme.test", "English"));
            await db.SaveChangesAsync(Ct);
        }

        var result = await new GetInvoiceEvidenceHandler(ctx.Factory, ctx.CurrentUser, InvoiceTestContext.FakePlan.Pro)
            .Handle(new GetInvoiceEvidence { Id = ctx.InvoiceId }, Ct);

        result.Response!.Reminders.Should().BeEmpty();
    }

    [Fact]
    public async Task An_unchased_invoice_has_no_reminders()
    {
        using var ctx = new InvoiceTestContext();
        await ctx.SeedAsync(entryDays: [(1, 2m)]);

        var result = await new GetInvoiceEvidenceHandler(ctx.Factory, ctx.CurrentUser, InvoiceTestContext.FakePlan.Pro)
            .Handle(new GetInvoiceEvidence { Id = ctx.InvoiceId }, Ct);

        result.Response!.Reminders.Should().BeEmpty();
    }

    [Fact]
    public async Task A_free_account_is_refused_the_record()
    {
        using var ctx = new InvoiceTestContext();
        await ctx.SeedAsync(entryDays: [(1, 2m)]);

        var result = await new GetInvoiceEvidenceHandler(
                ctx.Factory, ctx.CurrentUser, InvoiceTestContext.FakePlan.Free)
            .Handle(new GetInvoiceEvidence { Id = ctx.InvoiceId }, Ct);

        // Forbidden, not NotFound: the invoice exists and is theirs, the plan is what is
        // missing. Answering NotFound would send the user looking for a deleted invoice.
        result.StatusCode.Should().Be(System.Net.HttpStatusCode.Forbidden);
        result.Response.Should().BeNull();
    }

    [Fact]
    public async Task The_plan_is_checked_before_anything_is_disclosed()
    {
        using var ctx = new InvoiceTestContext();
        await ctx.SeedAsync(entryDays: [(1, 2m)]);

        await using (var db = ctx.NewContext())
        {
            var invoice = await db.Invoices.SingleAsync(i => i.Id == ctx.InvoiceId, Ct);
            invoice.RecordDelivery(
                new DateTime(2026, 10, 7, 9, 30, 0, DateTimeKind.Utc),
                "client@example.com", DocumentFingerprint.Of([7, 7, 7])!);
            await db.SaveChangesAsync(Ct);
        }

        var result = await new GetInvoiceEvidenceHandler(
                ctx.Factory, ctx.CurrentUser, InvoiceTestContext.FakePlan.Free)
            .Handle(new GetInvoiceEvidence { Id = ctx.InvoiceId }, Ct);

        // The gate is the whole feature: an endpoint that assembles the record and then
        // declines to render it has already disclosed it to anyone holding a cookie.
        result.Response.Should().BeNull();
    }

    [Fact]
    public async Task Another_users_invoice_is_not_readable()
    {
        using var ctx = new InvoiceTestContext();
        await ctx.SeedAsync(entryDays: [(1, 2m)]);

        var result = await new GetInvoiceEvidenceHandler(ctx.Factory, new InvoiceTestContext.OtherUser(), InvoiceTestContext.FakePlan.Pro)
            .Handle(new GetInvoiceEvidence { Id = ctx.InvoiceId }, Ct);

        result.Response.Should().BeNull();
    }
}
