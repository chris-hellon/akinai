using Akinai.Domain.Entities;
using Akinai.Domain.Enums;
using Akinai.Domain.Exceptions;
using Akinai.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Akinai.Tests;

/// <summary>
/// Sending commits the status before it renders and emails the document, so a process that
/// dies in between leaves an invoice marked Sent that nobody received.
///
/// Most of these are about what the sweeper must <em>not</em> touch. Returning a delivered
/// or deliberately-marked invoice to draft is far worse than leaving a stuck one alone:
/// the first rewrites a document a client is holding, the second is a row somebody can
/// look at.
/// </summary>
public class AbandonedSendTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly DateTime Now = new(2026, 10, 29, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>Comfortably outside the thirty-minute grace period.</summary>
    private static readonly DateTime LongAgo = Now.AddHours(-2);

    private static async Task<InvoiceStatus> StatusOf(InvoiceTestContext ctx)
    {
        await using var db = ctx.NewContext();
        return (await db.Invoices.SingleAsync(i => i.Id == ctx.InvoiceId, Ct)).Status;
    }

    private static async Task MutateAsync(InvoiceTestContext ctx, Action<Invoice> change)
    {
        await using var db = ctx.NewContext();
        var invoice = await db.Invoices.SingleAsync(i => i.Id == ctx.InvoiceId, Ct);
        change(invoice);
        await db.SaveChangesAsync(Ct);
    }

    [Fact]
    public async Task A_send_that_never_finished_is_returned_to_draft()
    {
        using var ctx = new InvoiceTestContext();
        await ctx.SeedAsync();
        await MutateAsync(ctx, i => i.BeginSend(LongAgo));

        await using (var db = ctx.NewContext())
        {
            (await AbandonedSendSweeper.SweepAsync(db, Now, NullLogger.Instance, Ct))
                .Should().Be(1);
        }

        (await StatusOf(ctx)).Should().Be(InvoiceStatus.Draft);
    }

    [Fact]
    public async Task A_send_still_within_the_grace_period_is_left_alone()
    {
        using var ctx = new InvoiceTestContext();
        await ctx.SeedAsync();

        // Five minutes in. Rendering launches Chromium and the email then waits on SMTP;
        // rolling back a send that is merely slow would deliver the invoice and leave it
        // saying Draft, which is the one outcome worse than leaving it stuck.
        await MutateAsync(ctx, i => i.BeginSend(Now.AddMinutes(-5)));

        await using (var db = ctx.NewContext())
        {
            (await AbandonedSendSweeper.SweepAsync(db, Now, NullLogger.Instance, Ct))
                .Should().Be(0);
        }

        (await StatusOf(ctx)).Should().Be(InvoiceStatus.Sent);
    }

    [Fact]
    public async Task An_invoice_marked_sent_by_hand_is_never_swept()
    {
        using var ctx = new InvoiceTestContext();
        await ctx.SeedAsync();

        // The regression this whole design exists for. A manual mark produces Sent with no
        // delivery record — the exact signature an earlier plan would have swept on — and
        // it is an ordinary thing to do for an invoice posted or sent from the user's own
        // mail client. On a typical account most sent invoices look like this.
        await MutateAsync(ctx, i => i.MarkSentManually());

        await using (var db = ctx.NewContext())
        {
            (await AbandonedSendSweeper.SweepAsync(db, Now, NullLogger.Instance, Ct))
                .Should().Be(0);
        }

        (await StatusOf(ctx)).Should().Be(InvoiceStatus.Sent);
    }

    [Fact]
    public async Task A_delivered_invoice_is_never_swept()
    {
        using var ctx = new InvoiceTestContext();
        await ctx.SeedAsync();
        await MutateAsync(ctx, i =>
        {
            i.BeginSend(LongAgo);
            i.RecordDelivery(LongAgo.AddMinutes(1), "client@example.com", new string('a', 64));
        });

        await using (var db = ctx.NewContext())
        {
            (await AbandonedSendSweeper.SweepAsync(db, Now, NullLogger.Instance, Ct))
                .Should().Be(0);
        }

        (await StatusOf(ctx)).Should().Be(InvoiceStatus.Sent);
    }

    [Fact]
    public async Task An_invoice_that_predates_the_field_is_invisible_to_the_sweeper()
    {
        using var ctx = new InvoiceTestContext();
        await ctx.SeedAsync();

        // Every invoice sent before SendStartedAtUtc existed has it null, which is the
        // same shape as a manual mark. The column was added without a backfill precisely
        // so that history cannot be swept on a guess.
        await MutateAsync(ctx, i => i.MarkSentManually());

        await using var db = ctx.NewContext();
        var invoice = await db.Invoices.SingleAsync(i => i.Id == ctx.InvoiceId, Ct);
        invoice.SendStartedAtUtc.Should().BeNull();
    }

    [Fact]
    public async Task Recording_delivery_ends_the_in_flight_state()
    {
        using var ctx = new InvoiceTestContext();
        await ctx.SeedAsync();
        await MutateAsync(ctx, i =>
        {
            i.BeginSend(LongAgo);
            i.RecordDelivery(LongAgo, "client@example.com", new string('b', 64));
        });

        await using var db = ctx.NewContext();
        var invoice = await db.Invoices.SingleAsync(i => i.Id == ctx.InvoiceId, Ct);

        // Left set, a delivered invoice would be swept as abandoned, and only
        // AbandonSend's own guard would stand between it and being walked back.
        invoice.SendStartedAtUtc.Should().BeNull();
    }

    [Fact]
    public async Task Abandoning_a_send_ends_the_in_flight_state()
    {
        using var ctx = new InvoiceTestContext();
        await ctx.SeedAsync();
        await MutateAsync(ctx, i =>
        {
            i.BeginSend(LongAgo);
            i.AbandonSend();
        });

        await using var db = ctx.NewContext();
        var invoice = await db.Invoices.SingleAsync(i => i.Id == ctx.InvoiceId, Ct);

        invoice.Status.Should().Be(InvoiceStatus.Draft);
        invoice.SendStartedAtUtc.Should().BeNull();
        // Otherwise a retried send would be swept using the first attempt's timestamp,
        // and a second failure would look two hours old the moment it began.
    }

    [Fact]
    public async Task A_delivered_invoice_is_spared_even_if_it_still_looks_in_flight()
    {
        using var ctx = new InvoiceTestContext();
        await ctx.SeedAsync();
        await MutateAsync(ctx, i => i.BeginSend(LongAgo));

        // Forced through SQL because the entity cannot produce this state: RecordDelivery
        // clears SendStartedAtUtc, so a delivered invoice never carries both. That is the
        // point — the sweeper's SentAtUtc check is unreachable through the domain today,
        // and is there in case some later path stamps a delivery without clearing the
        // marker. The one mistake that matters here is un-sending an invoice a client is
        // holding, so the guard is tested directly rather than trusted to stay unreachable.
        await using (var setup = ctx.NewContext())
        {
            await setup.Database.ExecuteSqlRawAsync(
                "UPDATE Invoices SET SentAtUtc = {0} WHERE Id = {1}",
                [LongAgo.AddMinutes(1), ctx.InvoiceId],
                Ct);
        }

        await using (var db = ctx.NewContext())
        {
            (await AbandonedSendSweeper.SweepAsync(db, Now, NullLogger.Instance, Ct))
                .Should().Be(0);
        }

        (await StatusOf(ctx)).Should().Be(InvoiceStatus.Sent);
    }

    [Fact]
    public void A_delivered_invoice_cannot_be_abandoned()
    {
        var invoice = Invoice.Create(
            "INV-00001", Guid.NewGuid(),
            new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31), taxRate: 0m, currency: "GBP");

        invoice.BeginSend(LongAgo);
        invoice.RecordDelivery(LongAgo, "client@example.com", new string('c', 64));

        // The invariant the sweeper leans on: whatever it believes, an invoice the client
        // is holding cannot be walked back to draft.
        var abandon = () => invoice.AbandonSend();
        abandon.Should().Throw<DomainException>();
    }

    [Fact]
    public async Task A_stuck_invoice_can_be_edited_and_sent_again_once_swept()
    {
        using var ctx = new InvoiceTestContext();
        await ctx.SeedAsync();
        await MutateAsync(ctx, i => i.BeginSend(LongAgo));

        await using (var db = ctx.NewContext())
        {
            await AbandonedSendSweeper.SweepAsync(db, Now, NullLogger.Instance, Ct);
        }

        // The point of rolling back rather than just reporting: Edit refuses on a
        // non-draft, so until this happens the invoice can be neither corrected nor
        // legitimately re-sent.
        await using var check = ctx.NewContext();
        var invoice = await check.Invoices.SingleAsync(i => i.Id == ctx.InvoiceId, Ct);
        var resend = () => invoice.BeginSend(Now);
        resend.Should().NotThrow();
    }
}
