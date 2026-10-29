using System.Net;
using Akinai.Infrastructure.Handlers.Invoices;
using Akinai.Infrastructure.Services;
using Akinai.Shared.Api.Invoices;
using Akinai.Shared.Billing;
using Akinai.Shared.Dtos.Billing;
using Akinai.Shared.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Akinai.Tests;

/// <summary>
/// A sent invoice is a document someone else is holding. Editing it would change what
/// they owe after they were told, and would leave the evidence pack — the hash of the
/// PDF that actually went out — describing a document that no longer exists.
///
/// The UI already hides the Edit button and the edit page refuses to load, but both are
/// bypassable by anyone who can issue an HTTP request. This is the gate that is not.
/// </summary>
public class InvoiceEditLockTests
{
    private static UpdateInvoiceHandler Handler(InvoiceTestContext ctx) =>
        new(ctx.Factory, ctx.CurrentUser, InvoiceTestContext.FakePlan.Pro,
            new InvoiceTotalService(new InvoiceTestContext.ParityRates()),
            new InvoicePaymentCurrencyService(new InvoiceTestContext.ParityRates()));

    private static UpdateInvoice Edit(Guid id) => new()
    {
        Id = id,
        IssueDate = new DateOnly(2026, 7, 1),
        DueDate = new DateOnly(2026, 7, 31),
        TaxRate = 99m,
        Notes = "edited after the fact",
    };

    private static async Task<InvoiceTestContext> SeededAsync()
    {
        var ctx = new InvoiceTestContext();
        await ctx.SeedAsync(clientRate: 100m, taxRate: 0m, entryDays: (1, 1m));
        return ctx;
    }

    private static async Task SetStatusAsync(InvoiceTestContext ctx, Action<Akinai.Domain.Entities.Invoice> transition)
    {
        await using var db = ctx.NewContext();
        var invoice = await db.Invoices.FirstAsync(i => i.Id == ctx.InvoiceId);
        transition(invoice);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task A_draft_can_still_be_edited()
    {
        using var ctx = await SeededAsync();

        var result = await Handler(ctx).Handle(Edit(ctx.InvoiceId), default);

        result.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task A_sent_invoice_is_refused()
    {
        using var ctx = await SeededAsync();
        await SetStatusAsync(ctx, i => i.MarkSentManually());

        var result = await Handler(ctx).Handle(Edit(ctx.InvoiceId), default);

        result.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_paid_invoice_is_refused()
    {
        using var ctx = await SeededAsync();
        await SetStatusAsync(ctx, i => { i.MarkSentManually(); i.MarkAsPaid(new DateOnly(2026, 8, 1)); });

        var result = await Handler(ctx).Handle(Edit(ctx.InvoiceId), default);

        result.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_refused_edit_changes_nothing()
    {
        // A 403 that had already written half the request would be worse than no gate at
        // all — the invoice would be neither the old one nor the new one.
        using var ctx = await SeededAsync();
        await SetStatusAsync(ctx, i => i.MarkSentManually());

        await Handler(ctx).Handle(Edit(ctx.InvoiceId), default);

        await using var db = ctx.NewContext();
        var invoice = await db.Invoices.FirstAsync(i => i.Id == ctx.InvoiceId);
        invoice.TaxRate.Should().Be(0m);
        invoice.Notes.Should().NotBe("edited after the fact");
    }
}
