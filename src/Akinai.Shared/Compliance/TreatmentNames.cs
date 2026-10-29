namespace Akinai.Shared.Compliance;

/// <summary>
/// Plain names for <see cref="CorridorRules.Treatment"/>.
///
/// The treatment reaches the UI as a string — handlers call <c>.ToString()</c> on the enum
/// so the DTOs stay free of a domain type — and <c>EuReverseCharge</c> is not something to
/// show anybody, least of all on a record meant to be read by an accountant or produced in
/// a dispute.
/// </summary>
public static class TreatmentNames
{
    /// <summary>
    /// Takes the stored or serialised name rather than the enum, because that is what
    /// callers hold. An unrecognised value comes back as written: if the table gains a
    /// treatment this method has not been taught, showing the raw name is honest, where
    /// falling back to "Unknown" would assert something false — "Unknown" is itself a
    /// real treatment here, and means the corridor could not be determined.
    /// </summary>
    public static string Describe(string? treatment) => treatment switch
    {
        nameof(CorridorRules.Treatment.Unknown) => "Could not be determined",
        nameof(CorridorRules.Treatment.Domestic) => "Domestic",
        nameof(CorridorRules.Treatment.EuReverseCharge) => "EU reverse charge",
        nameof(CorridorRules.Treatment.EuConsumer) => "EU consumer sale",
        nameof(CorridorRules.Treatment.ExportFromEu) => "Export from the EU",
        nameof(CorridorRules.Treatment.CrossBorderNonEu) => "Cross-border, outside the EU",
        null or "" => "Not assessed",
        _ => treatment,
    };

    /// <summary>Overload for callers holding the enum itself.</summary>
    public static string Describe(CorridorRules.Treatment treatment)
        => Describe(treatment.ToString());
}
