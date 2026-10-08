namespace Akinai.Shared.Dtos.Preflight;

/// <summary>
/// Everything worth knowing about an invoice before it goes out: whether the payment
/// details could actually be paid, and whether the document says what the corridor
/// requires it to say.
///
/// Both checks exist because their failures are silent and expensive. A mistyped IBAN is
/// discovered a fortnight later when the wire comes back with fees deducted; a missing
/// reverse-charge statement is discovered when the client's accountant rejects the invoice.
/// Neither shows up at the moment of sending unless something looks for it.
/// </summary>
public record InvoicePreflightDto(
    IReadOnlyList<PreflightFindingDto> Findings,
    /// <summary>The tax treatment the corridor rules believe applies.</summary>
    string TaxTreatment,
    /// <summary>Wording the document is required to carry, for display as confirmation.</summary>
    IReadOnlyList<string> RequiredStatements)
{
    public bool HasBlocking => Findings.Any(f => f.Severity == PreflightSeverity.Blocking);
    public bool IsClear => Findings.Count == 0;
}

public enum PreflightSeverity
{
    /// <summary>Will very likely fail, be returned, or be rejected by the client's accountant.</summary>
    Blocking,
    /// <summary>May be delayed or queried, but can succeed.</summary>
    Warning,
    /// <summary>Worth knowing; no action implied.</summary>
    Info,
}

/// <param name="Area">"Payment" or "Tax", so the UI can group them.</param>
public record PreflightFindingDto(PreflightSeverity Severity, string Area, string Message);
