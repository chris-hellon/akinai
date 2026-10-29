using Akinai.Domain.Entities;
using Akinai.Infrastructure.Handlers.Invoices;
using Akinai.Shared.Api.Invoices;
using FluentAssertions;

namespace Akinai.Tests;

/// <summary>
/// `DunningReminder.InvoiceId` was a bare Guid with an index and no navigation property,
/// so EF created no foreign key and deleting an invoice left its reminders behind as rows
/// nothing could reach. These run against a real relational database, which is the only
/// place a missing constraint shows up at all.
/// </summary>
public class DunningReminderCascadeTests
{
    [Fact]
    public async Task Deleting_an_invoice_takes_its_reminders_with_it()
    {
        using var ctx = new InvoiceTestContext();
        await ctx.SeedAsync();

        await using (var db = ctx.NewContext())
        {
            db.DunningReminders.Add(DunningReminder.Record(
                ctx.InvoiceId, ctx.ClientId, InvoiceTestContext.UserId,
                "FirstReminder", new DateTime(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc),
                "acme@example.com", "English"));
            await db.SaveChangesAsync();
        }

        var handler = new DeleteInvoiceHandler(ctx.Factory, ctx.CurrentUser);
        var result = await handler.Handle(new DeleteInvoice { Id = ctx.InvoiceId }, default);

        result.StatusCode.Should().Be(System.Net.HttpStatusCode.NoContent);

        await using var check = ctx.NewContext();
        check.DunningReminders.Should().BeEmpty(
            "a reminder naming an invoice that no longer exists can never be read again");
    }

    [Fact]
    public async Task A_reminder_cannot_be_recorded_against_an_invoice_that_does_not_exist()
    {
        using var ctx = new InvoiceTestContext();
        await ctx.SeedAsync();

        await using var db = ctx.NewContext();
        db.DunningReminders.Add(DunningReminder.Record(
            Guid.NewGuid(), ctx.ClientId, InvoiceTestContext.UserId,
            "FirstReminder", new DateTime(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc),
            "acme@example.com", "English"));

        // The other half of the constraint, and the reason it is worth having rather than
        // just cleaning up on delete: a typo'd invoice id used to insert quite happily.
        var save = async () => await db.SaveChangesAsync();
        await save.Should().ThrowAsync<Microsoft.EntityFrameworkCore.DbUpdateException>();
    }

    [Fact]
    public async Task Deleting_an_invoice_leaves_reminders_for_other_invoices_alone()
    {
        using var ctx = new InvoiceTestContext();
        await ctx.SeedAsync();

        Guid otherInvoiceId;
        await using (var db = ctx.NewContext())
        {
            var other = Invoice.Create(
                "INV-00002", ctx.ClientId,
                new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31),
                taxRate: 20m, currency: "GBP");
            db.Invoices.Add(other);
            await db.SaveChangesAsync();
            otherInvoiceId = other.Id;

            foreach (var invoiceId in new[] { ctx.InvoiceId, otherInvoiceId })
            {
                db.DunningReminders.Add(DunningReminder.Record(
                    invoiceId, ctx.ClientId, InvoiceTestContext.UserId,
                    "FirstReminder", new DateTime(2026, 9, 1, 9, 0, 0, DateTimeKind.Utc),
                    "acme@example.com", "English"));
            }
            await db.SaveChangesAsync();
        }

        var handler = new DeleteInvoiceHandler(ctx.Factory, ctx.CurrentUser);
        await handler.Handle(new DeleteInvoice { Id = ctx.InvoiceId }, default);

        await using var check = ctx.NewContext();
        var remaining = check.DunningReminders.ToList();

        // Cascade is scoped to the deleted row, not the client. Four late invoices are one
        // relationship for scheduling purposes, which is exactly why this is worth pinning:
        // ClientId is denormalised onto the reminder and must not drag the others out.
        remaining.Should().HaveCount(1);
        remaining[0].InvoiceId.Should().Be(otherInvoiceId);
    }
}
