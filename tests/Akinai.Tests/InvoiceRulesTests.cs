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
}
