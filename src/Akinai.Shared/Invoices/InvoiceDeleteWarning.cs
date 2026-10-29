namespace Akinai.Shared.Invoices;

/// <summary>
/// What the user is told before an invoice is deleted.
///
/// Deleting an invoice is a hard delete on purpose — one sent in error should be
/// removable, and Void is a cancellation, not an erasure. That makes the dialog
/// the only thing standing between a mis-click and a document that no longer
/// exists, so it has to carry the whole consequence rather than the generic
/// "this action cannot be undone".
///
/// Written once because the invoice list and the detail page both ask. A warning
/// that is severe on one screen and bland on the other teaches the user to trust
/// the bland one.
/// </summary>
public static class InvoiceDeleteWarning
{
    /// <summary>
    /// True once the invoice has left draft, whatever became of it afterwards.
    /// A draft is the only state nobody outside the account has seen.
    /// </summary>
    public static bool IsIssued(string? status)
        => !string.Equals(status, "Draft", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The sentence shown in every case: what the delete takes with it, and what it
    /// only releases. Billed work surviving is the one reassuring part, and saying so
    /// stops the warning reading as "you will lose the month's timesheets" — which
    /// would push people into leaving a wrong invoice in place instead.
    /// </summary>
    public static string Baseline(string? status)
    {
        var subject = IsIssued(status) ? "the invoice" : "the draft";
        return $"This permanently removes {subject}, its line items and the currencies it accepts. "
             + "Billed time and expenses are not deleted — they go back to unbilled and can be invoiced again.";
    }

    /// <summary>
    /// The line on the detail page's danger-zone panel, before anything is clicked.
    /// Shares <see cref="Severe"/>'s rule about delivery evidence, so the panel cannot
    /// promise a consequence the dialog then declines to mention.
    /// </summary>
    public static string PanelDescription(string? status, DateTime? sentAtUtc)
    {
        if (!IsIssued(status))
            return "Permanently remove this draft. This cannot be undone.";

        var lead = sentAtUtc.HasValue
            ? "Permanently remove this invoice and the record that it was sent."
            : "Permanently remove this invoice and everything recorded against it.";

        return status is "Sent" or "Overdue"
            ? lead + " Void it instead if you only need to cancel it."
            : lead;
    }

    /// <summary>
    /// The paragraph an issued invoice gets, shown in its own box. Null for a draft,
    /// where none of it is true.
    ///
    /// <paramref name="sentAtUtc"/> rather than the status decides whether this claims
    /// the invoice reached anyone, because the status cannot: both Void and Paid are
    /// reachable straight from Draft, so an invoice can be either without ever having
    /// been sent. Telling someone a delete destroys delivery evidence that was never
    /// recorded is the same class of mistake as not warning them at all.
    /// </summary>
    public static string? Severe(string? status, string invoiceNumber, DateTime? sentAtUtc)
    {
        if (!IsIssued(status)) return null;

        var text = sentAtUtc.HasValue
            ? $"{invoiceNumber} has already gone out. Deleting it erases the record that it was ever "
            + "sent — who it went to, when, the fingerprint of the document they received, and every "
            + "time they opened it. Your client keeps their copy; you will have nothing left to match "
            + "it against."
            : $"{invoiceNumber} has left draft, so it counts as issued even though it was never emailed "
            + "from here. Deleting it removes it from your records entirely — if it reached the client "
            + "by any other route, you will have nothing left to match their copy against.";

        // Numbering continues from the highest invoice that still exists, so a delete
        // either frees the number for reuse or leaves a hole. Easy to miss and awkward
        // to explain to an accountant afterwards, which is why it is spelled out.
        text += " The number is not retired either: it will either leave a gap in your sequence or be "
              + "issued again to someone else.";

        // Void is the non-destructive alternative, so it is worth offering — but only
        // where it is still a sensible thing to do. Suggesting it on an invoice that is
        // already void, or on one that has been paid, reads as not having looked.
        if (status is "Sent" or "Overdue")
        {
            text += $" To cancel {invoiceNumber} without erasing it, void it instead.";
        }

        return text;
    }
}
