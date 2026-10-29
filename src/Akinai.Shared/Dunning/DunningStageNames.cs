namespace Akinai.Shared.Dunning;

/// <summary>
/// Plain-English names for the rungs of the ladder.
///
/// Reminders store their stage as text rather than as an enum value, so that a renumbered
/// enum cannot re-point historical rows at a different rung. The trade is that anything
/// displaying a stored stage is holding a string, and <c>FirmReminder</c> is not something
/// to show a user — least of all on a record meant to be handed to an accountant.
/// </summary>
public static class DunningStageNames
{
    /// <summary>
    /// What to call the rung. Takes the stored string rather than the enum, because the
    /// stored value is what callers have, and an unrecognised one is returned as written:
    /// a row from an older or newer ladder is still evidence, and silently relabelling it
    /// "Unknown" would hide the one thing the reader needs.
    /// </summary>
    public static string Describe(string? stage) => stage switch
    {
        nameof(DunningStage.AdvanceNotice) => "Advance notice",
        nameof(DunningStage.GentleReminder) => "Gentle reminder",
        nameof(DunningStage.FirmReminder) => "Firm reminder",
        nameof(DunningStage.FinalNotice) => "Final notice",
        null or "" => "Reminder",
        _ => stage,
    };

    /// <summary>
    /// Whether this rung went out before the due date. Worth distinguishing on a record,
    /// because an advance notice is not a chase and counting it as one overstates how
    /// hard a client was pursued.
    /// </summary>
    public static bool IsBeforeDue(string? stage)
        => stage == nameof(DunningStage.AdvanceNotice);
}
