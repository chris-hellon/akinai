using Akinai.Domain.Entities;
using Akinai.Infrastructure.Handlers.ClientPortal;
using Akinai.Infrastructure.Handlers.Invoices;
using Akinai.Infrastructure.Services;
using Akinai.Shared.Api.ClientPortal;
using Akinai.Shared.Api.Invoices;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Akinai.Tests;

/// <summary>
/// Handler-level tests against a real database.
///
/// Every case here corresponds to something that actually shipped broken: the invoice
/// list and detail view disagreeing on a total, an emailed total that omitted product
/// lines, a client portal balance derived from raw hours, and rows arriving in
/// whatever order the database felt like. None of it was visible to the domain tests,
/// because none of it lives in the domain.
/// </summary>
public class InvoiceTotalsHandlerTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // 2h + 3h at 100/hr = 500, +20% tax = 600.
    private const decimal ExpectedSubtotal = 500m;
    private const decimal ExpectedTotal = 600m;

    private static async Task<InvoiceTestContext> SeededAsync()
    {
        var ctx = new InvoiceTestContext();
        await ctx.SeedAsync(clientRate: 100m, taxRate: 20m, entryDays: [(3, 2m), (1, 3m)]);
        return ctx;
    }

    [Fact]
    public async Task The_list_and_the_detail_view_report_the_same_total()
    {
        using var ctx = await SeededAsync();

        // A product line is what makes this test bite. With time entries alone, anything
        // that recomputed a total from the rows would land on the same number by luck;
        // with a product on the invoice, only the stored total is right.
        await using (var db = ctx.NewContext())
        {
            db.InvoiceLineItems.Add(
                InvoiceLineItem.Create(ctx.InvoiceId, "Hosting", quantity: 1m, unitPrice: 250m, "GBP", order: 0));
            await db.SaveChangesAsync(Ct);
        }
        await ctx.RecalculateAsync();

        var list = await new GetInvoicesHandler(ctx.Factory, ctx.CurrentUser)
            .Handle(new GetInvoices(), Ct);
        var detail = await new GetInvoiceHandler(
                ctx.Factory, ctx.CurrentUser, new InvoiceTestContext.PassThroughTranslator())
            .Handle(new GetInvoice { Id = ctx.InvoiceId }, Ct);

        var summary = list.Response!.Items.Single();
        summary.Subtotal.Should().Be(ExpectedSubtotal + 250m);
        summary.Total.Should().Be(900m);

        detail.Response!.Total.Should().Be(summary.Total);
        detail.Response.Subtotal.Should().Be(summary.Subtotal);
        detail.Response.TaxAmount.Should().Be(summary.TaxAmount);
    }

    [Fact]
    public async Task Raising_the_job_rate_afterwards_does_not_restate_the_invoice()
    {
        // The whole point of freezing the rate. Before it existed, this doubled every
        // figure on an invoice the client already held.
        using var ctx = await SeededAsync();

        await using (var db = ctx.NewContext())
        {
            var job = await db.Jobs.SingleAsync(j => j.Id == ctx.JobId, Ct);
            job.Update(job.Name, job.Description, job.Notes, job.Status, hourlyRateOverride: 200m);
            await db.SaveChangesAsync(Ct);
        }

        var list = await new GetInvoicesHandler(ctx.Factory, ctx.CurrentUser)
            .Handle(new GetInvoices(), Ct);
        var detail = await new GetInvoiceHandler(
                ctx.Factory, ctx.CurrentUser, new InvoiceTestContext.PassThroughTranslator())
            .Handle(new GetInvoice { Id = ctx.InvoiceId }, Ct);

        list.Response!.Items.Single().Total.Should().Be(ExpectedTotal);
        detail.Response!.Total.Should().Be(ExpectedTotal);
        detail.Response.LineItems.Should().OnlyContain(li => li.UnitPrice == 100m);
    }

    [Fact]
    public async Task Re_snapshotting_an_invoice_does_not_reprice_it_at_the_new_rate()
    {
        // Editing an invoice re-runs InvoiceTotalService. That must not quietly pull in
        // a rate change made since it was raised.
        using var ctx = await SeededAsync();

        await using (var db = ctx.NewContext())
        {
            var job = await db.Jobs.SingleAsync(j => j.Id == ctx.JobId, Ct);
            job.Update(job.Name, job.Description, job.Notes, job.Status, hourlyRateOverride: 200m);
            await db.SaveChangesAsync(Ct);
        }

        await ctx.RecalculateAsync();

        await using var check = ctx.NewContext();
        var invoice = await check.Invoices.SingleAsync(i => i.Id == ctx.InvoiceId, Ct);
        invoice.Total.Should().Be(ExpectedTotal);
    }

    [Fact]
    public async Task Line_items_come_back_in_date_order()
    {
        // Seeded deliberately out of order: the 3rd is inserted before the 1st.
        using var ctx = await SeededAsync();

        var detail = await new GetInvoiceHandler(
                ctx.Factory, ctx.CurrentUser, new InvoiceTestContext.PassThroughTranslator())
            .Handle(new GetInvoice { Id = ctx.InvoiceId }, Ct);

        detail.Response!.LineItems.Select(li => li.Date)
            .Should().BeInAscendingOrder()
            .And.Equal(new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 3));
    }

    [Fact]
    public async Task The_emailed_total_counts_product_line_items()
    {
        // It used to sum time and expenses only, so any invoice carrying products was
        // emailed an amount lower than the invoice itself said.
        using var ctx = await SeededAsync();

        await using (var db = ctx.NewContext())
        {
            db.InvoiceLineItems.Add(
                InvoiceLineItem.Create(ctx.InvoiceId, "Hosting", quantity: 1m, unitPrice: 250m, "GBP", order: 0));
            await db.SaveChangesAsync(Ct);
        }
        await ctx.RecalculateAsync();

        var html = await new GetInvoiceEmailTemplateHandler(
                ctx.Factory, ctx.CurrentUser, new EmailTemplateService(), new InvoiceTestContext.NoHttpContext())
            .Handle(new GetInvoiceEmailTemplate { Id = ctx.InvoiceId }, Ct);

        // 500 + 250 = 750 subtotal, +20% = 900.
        html.Response.Should().Contain("900");
        html.Response.Should().NotContain("600.00");
    }

    [Fact]
    public async Task The_client_portal_shows_the_amount_the_client_was_invoiced()
    {
        // The portal used to re-derive this from raw hours, so it under-reported the
        // balance — missing billing rounding, expenses and product lines.
        using var ctx = await SeededAsync();

        await using (var db = ctx.NewContext())
        {
            db.InvoiceLineItems.Add(
                InvoiceLineItem.Create(ctx.InvoiceId, "Hosting", quantity: 1m, unitPrice: 250m, "GBP", order: 0));
            var invoice = await db.Invoices.SingleAsync(i => i.Id == ctx.InvoiceId, Ct);
            invoice.Send();
            await db.SaveChangesAsync(Ct);
        }
        await ctx.RecalculateAsync();

        var portal = await new GetClientPortalInvoicesHandler(ctx.Factory)
            .Handle(new GetClientPortalInvoices { ClientToken = ctx.PortalToken }, Ct);

        portal.Response!.Items.Single().Total.Should().Be(900m);
    }
}
