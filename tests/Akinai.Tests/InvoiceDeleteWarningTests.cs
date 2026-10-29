using Akinai.Shared.Invoices;
using FluentAssertions;

namespace Akinai.Tests;

/// <summary>
/// Deleting an invoice is a hard delete by design, so the dialog is the only safeguard.
/// These pin the wording down because wording is exactly what tends to get softened
/// later — and a warning that no longer mentions the evidence it destroys is the same
/// as no warning.
/// </summary>
public class InvoiceDeleteWarningTests
{
    private static readonly DateTime Sent = new(2026, 10, 1, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void A_draft_is_not_issued()
    {
        InvoiceDeleteWarning.IsIssued("Draft").Should().BeFalse();
    }

    [Theory]
    [InlineData("Sent")]
    [InlineData("Paid")]
    [InlineData("Overdue")]
    [InlineData("Void")]
    public void Everything_that_is_not_a_draft_is_issued(string status)
    {
        InvoiceDeleteWarning.IsIssued(status).Should().BeTrue();
    }

    [Fact]
    public void A_draft_gets_no_severe_warning()
    {
        // Nobody outside the account has seen it, so there is no evidence to destroy
        // and nothing to be alarmed about beyond the ordinary.
        InvoiceDeleteWarning.Severe("Draft", "INV-00009", null).Should().BeNull();
    }

    [Fact]
    public void An_issued_invoice_is_told_the_evidence_goes_too()
    {
        var severe = InvoiceDeleteWarning.Severe("Sent", "INV-00006", Sent);

        severe.Should().NotBeNull();
        severe.Should().Contain("INV-00006");
        // The four things the invoice row carries that nothing else records.
        severe.Should().Contain("who it went to");
        severe.Should().Contain("fingerprint");
        severe.Should().Contain("opened it");
        // And the consequence that is easiest to miss: numbering continues from the
        // highest invoice that still exists, so the number is not retired.
        severe.Should().Contain("gap");
    }

    [Fact]
    public void An_issued_invoice_says_the_client_keeps_their_copy()
    {
        // The real hazard is asymmetry, not loss: deleting your side does not recall
        // theirs, it just leaves you unable to answer them.
        InvoiceDeleteWarning.Severe("Sent", "INV-00006", Sent)
            .Should().Contain("client keeps their copy");
    }

    [Theory]
    [InlineData("Sent")]
    [InlineData("Overdue")]
    public void Void_is_offered_where_it_is_still_the_right_answer(string status)
    {
        InvoiceDeleteWarning.Severe(status, "INV-00006", Sent)
            .Should().Contain("void it instead");
    }

    [Theory]
    [InlineData("Paid")]
    [InlineData("Void")]
    public void Void_is_not_offered_where_it_makes_no_sense(string status)
    {
        // Already void, or settled. Suggesting it reads as not having looked at the
        // invoice, and that costs the rest of the warning its credibility.
        InvoiceDeleteWarning.Severe(status, "INV-00006", Sent)
            .Should().NotContain("void it instead");
    }

    [Fact]
    public void Both_states_promise_that_billed_work_survives()
    {
        // Time entries and expenses are unlinked, not deleted. Without this the
        // warning reads as "you will lose the month's timesheets", which would push
        // people towards leaving wrong invoices in place.
        foreach (var status in new[] { "Draft", "Sent" })
        {
            InvoiceDeleteWarning.Baseline(status)
                .Should().Contain("back to unbilled");
        }
    }

    [Fact]
    public void An_issued_invoice_that_was_never_sent_is_not_told_it_loses_delivery_evidence()
    {
        // Void and Paid are both reachable straight from Draft, so "issued" does not
        // imply "emailed". Claiming a delete destroys delivery evidence that was never
        // recorded would be the warning lying to make itself sound graver.
        var severe = InvoiceDeleteWarning.Severe("Void", "INV-00005", sentAtUtc: null);

        severe.Should().NotBeNull();
        severe.Should().NotContain("who it went to");
        severe.Should().NotContain("every time they opened it");
        severe.Should().Contain("never emailed");
        // The number consequence applies either way.
        severe.Should().Contain("gap");
    }

    [Fact]
    public void The_panel_and_the_dialog_agree_about_delivery_evidence()
    {
        // The panel is read first and the dialog second. If the panel claims the delete
        // destroys a send record the dialog then says nothing about, the user learns the
        // panel overstates — and starts skimming both.
        InvoiceDeleteWarning.PanelDescription("Void", sentAtUtc: null)
            .Should().NotContain("record that it was sent");
        InvoiceDeleteWarning.PanelDescription("Sent", Sent)
            .Should().Contain("record that it was sent");
    }

    [Fact]
    public void The_panel_offers_void_on_the_same_terms_as_the_dialog()
    {
        InvoiceDeleteWarning.PanelDescription("Overdue", Sent).Should().Contain("Void it instead");
        InvoiceDeleteWarning.PanelDescription("Paid", Sent).Should().NotContain("Void it instead");
        InvoiceDeleteWarning.PanelDescription("Draft", null).Should().NotContain("Void it instead");
    }

    [Fact]
    public void The_baseline_names_what_it_is_deleting()
    {
        InvoiceDeleteWarning.Baseline("Draft").Should().Contain("the draft");
        InvoiceDeleteWarning.Baseline("Sent").Should().Contain("the invoice");
    }
}
