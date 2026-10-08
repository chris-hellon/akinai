using FluentAssertions;
using Akinai.Domain.Entities;
using Akinai.Domain.Enums;
using Akinai.Domain.Exceptions;
using Akinai.Shared.Localization;

namespace Akinai.Tests;

/// <summary>
/// The rules that stop an issued invoice being quietly rewritten, and the
/// document-language fallbacks that must never throw.
/// </summary>
public class InvoiceRulesTests
{
    private static Invoice Draft() => Invoice.Create(
        "INV-00001", Guid.NewGuid(),
        new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31),
        taxRate: 20m, currency: "GBP");

    private static readonly DateTime Now = new(2026, 7, 23, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void A_new_invoice_gets_a_public_token_and_is_a_draft()
    {
        var invoice = Draft();
        invoice.Status.Should().Be(InvoiceStatus.Draft);
        // The token is how a client reaches the invoice with no account. An
        // invoice without one cannot be sent or turned into a PDF.
        invoice.PublicAccessToken.Should().NotBeNull().And.NotBe(Guid.Empty);
    }

    [Fact]
    public void A_sent_invoice_cannot_be_edited()
    {
        // The client already has this document. Changing dates or amounts under
        // them is the single worst thing an invoicing tool can do.
        var invoice = Draft();
        invoice.Send();

        var edit = () => invoice.UpdateDetails(
            new DateOnly(2026, 1, 1), new DateOnly(2026, 2, 1), 0m, "changed");

        edit.Should().Throw<DomainException>();
    }

    [Fact]
    public void A_paid_invoice_cannot_be_voided()
    {
        var invoice = Draft();
        invoice.Send();
        invoice.MarkAsPaid(new DateOnly(2026, 7, 20));

        var voidIt = () => invoice.Void();
        voidIt.Should().Throw<DomainException>();
    }

    [Fact]
    public void An_invoice_cannot_be_paid_twice()
    {
        var invoice = Draft();
        invoice.Send();
        invoice.MarkAsPaid(new DateOnly(2026, 7, 20));

        var again = () => invoice.MarkAsPaid(new DateOnly(2026, 7, 21));
        again.Should().Throw<DomainException>();
    }

    [Fact]
    public void Language_pins_once_set()
    {
        // A sent invoice must keep the language it was sent in, whatever the
        // account default later becomes.
        var invoice = Invoice.Create(
            "INV-2", Guid.NewGuid(), new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31),
            language: SupportedLanguage.Vietnamese);

        invoice.Language.Should().Be(SupportedLanguage.Vietnamese);
    }

    [Fact]
    public void Recording_views_keeps_the_first_and_moves_the_last()
    {
        var invoice = Draft();
        invoice.FirstViewedAtUtc.Should().BeNull();

        invoice.RecordView(Now);
        invoice.RecordView(Now.AddHours(3));

        invoice.ViewCount.Should().Be(2);
        invoice.FirstViewedAtUtc.Should().Be(Now);          // never moves
        invoice.LastViewedAtUtc.Should().Be(Now.AddHours(3));
    }

    [Theory]
    [InlineData("Vietnamese", "HÓA ĐƠN")]
    [InlineData("Chinese", "发票")]
    [InlineData("Korean", "청구서")]
    [InlineData("English", "INVOICE")]
    public void Labels_resolve_for_every_supported_language(string language, string expected)
        => InvoiceLabels.For(language).Invoice.Should().Be(expected);

    [Theory]
    [InlineData("Klingon")]   // never existed
    [InlineData("")]
    [InlineData(null)]
    public void An_unknown_language_falls_back_to_English_rather_than_throwing(string? language)
    {
        // A stale value in the database must not be able to break an invoice.
        InvoiceLabels.For(language).Invoice.Should().Be("INVOICE");
    }

    [Fact]
    public void Expense_categories_are_translated_and_de_pascal_cased()
    {
        InvoiceLabels.ExpenseCategory("English", "OfficeSupplies").Should().Be("Office Supplies");
        InvoiceLabels.ExpenseCategory("Vietnamese", "Travel").Should().Be("Đi lại");
        // An enum member added in code before its translations exist shows the
        // raw name rather than blowing up the invoice.
        InvoiceLabels.ExpenseCategory("English", "SomethingNew").Should().Be("SomethingNew");
    }

    [Fact]
    public void The_translation_cache_key_ignores_surrounding_whitespace()
    {
        // Trailing whitespace from a text box must not read as different text
        // and trigger a second paid translation of identical content.
        TranslationCacheEntry.HashOf("Design work")
            .Should().Be(TranslationCacheEntry.HashOf("  Design work \n"));
    }

    [Fact]
    public void The_translation_cache_key_distinguishes_different_text()
        => TranslationCacheEntry.HashOf("Design work")
            .Should().NotBe(TranslationCacheEntry.HashOf("Design Work"));

    // --- Billing rate is fixed at the moment of invoicing --------------------

    private static TimeEntry UnbilledEntry() => TimeEntry.Create(
        Guid.NewGuid(), new DateOnly(2026, 7, 1), hours: 2m, "Design work", "user-1");

    [Fact]
    public void Billing_an_entry_freezes_the_rate_it_was_billed_at()
    {
        var entry = UnbilledEntry();
        entry.BilledHourlyRate.Should().BeNull("an unbilled entry has no billed rate");

        entry.LinkToInvoice(Guid.NewGuid(), 85m);

        entry.BilledHourlyRate.Should().Be(85m);
        entry.BillingRate.Should().Be(85m);
    }

    [Fact]
    public void A_billed_entry_never_reprices_when_the_job_rate_changes()
    {
        // The rate cascades job → project → client, so a later edit at any of
        // those levels would otherwise restate an invoice the client already
        // holds. Once billed, the entry must not consult the job at all: this
        // entry's Job navigation is never loaded, so reading BillingRate would
        // throw if the frozen rate were not being used.
        var entry = UnbilledEntry();
        entry.LinkToInvoice(Guid.NewGuid(), 85m);

        var rate = () => entry.BillingRate;

        rate.Should().NotThrow().Which.Should().Be(85m);
    }

    [Fact]
    public void Taking_an_entry_off_an_invoice_releases_the_frozen_rate()
    {
        // Back to unbilled: it should price at whatever the job charges now, and
        // re-freeze if it is billed onto another invoice.
        var entry = UnbilledEntry();
        entry.LinkToInvoice(Guid.NewGuid(), 85m);

        entry.UnlinkFromInvoice();

        entry.BilledHourlyRate.Should().BeNull();
        entry.IsBilled.Should().BeFalse();
    }

    [Fact]
    public void An_entry_cannot_be_billed_onto_a_second_invoice()
    {
        var entry = UnbilledEntry();
        entry.LinkToInvoice(Guid.NewGuid(), 85m);

        var relink = () => entry.LinkToInvoice(Guid.NewGuid(), 120m);

        relink.Should().Throw<DomainException>();
        entry.BilledHourlyRate.Should().Be(85m, "the failed attempt must not reprice it");
    }

    // --- Sending is two-phase ------------------------------------------------

    [Fact]
    public void An_in_flight_send_can_be_abandoned_back_to_draft()
    {
        // The status is committed before the document is rendered, so that the PDF shows
        // "Sent". This is what puts it back when the render or the email then fails.
        var invoice = Draft();
        invoice.Send();

        invoice.AbandonSend();

        invoice.Status.Should().Be(InvoiceStatus.Draft);
    }

    [Fact]
    public void A_delivered_invoice_cannot_be_abandoned()
    {
        // The guard that keeps AbandonSend from being a general un-send: once a delivery
        // is recorded the email has gone and the client holds the document.
        var invoice = Draft();
        invoice.Send();
        invoice.RecordDelivery(Now, "client@example.com", "abc123");

        var abandon = () => invoice.AbandonSend();

        abandon.Should().Throw<DomainException>();
        invoice.Status.Should().Be(InvoiceStatus.Sent);
    }

    [Fact]
    public void A_draft_invoice_cannot_be_abandoned()
    {
        var abandon = () => Draft().AbandonSend();

        abandon.Should().Throw<DomainException>();
    }

    [Fact]
    public void An_abandoned_invoice_can_be_sent_again()
    {
        // A failed send must leave the invoice in a state the user can retry from.
        var invoice = Draft();
        invoice.Send();
        invoice.AbandonSend();

        var resend = () => invoice.Send();

        resend.Should().NotThrow();
        invoice.Status.Should().Be(InvoiceStatus.Sent);
    }

    [Fact]
    public void Abandoning_keeps_the_public_token_so_a_sent_link_never_changes()
    {
        // Send() mints the token. If abandoning cleared it, a retry would hand the client
        // a different link from any earlier attempt.
        var invoice = Draft();
        invoice.Send();
        var token = invoice.PublicAccessToken;

        invoice.AbandonSend();

        invoice.PublicAccessToken.Should().Be(token);
    }
}
