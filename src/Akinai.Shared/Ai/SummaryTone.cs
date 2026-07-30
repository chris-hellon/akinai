namespace Akinai.Shared.Ai;

/// <summary>
/// How a timesheet summary should read to the client.
///
/// The same grouping of work can be phrased for a corporate accounts department
/// or for a client you know well. Held as strings shared by both ends so the
/// picker, the request and the prompt cannot drift apart.
/// </summary>
public static class SummaryTone
{
    /// <summary>Clear and neutral. The default when none is chosen.</summary>
    public const string Professional = "Professional";

    /// <summary>Precise and impersonal, for a corporate client or their accountant.</summary>
    public const string Formal = "Formal";

    /// <summary>Warm and approachable, for a client you have a working relationship with.</summary>
    public const string Friendly = "Friendly";

    /// <summary>As short as the work allows — terse noun phrases, no filler.</summary>
    public const string Concise = "Concise";

    /// <summary>The options a picker offers, in display order.</summary>
    public static readonly IReadOnlyList<string> All =
        [Professional, Formal, Friendly, Concise];

    /// <summary>True when <paramref name="tone"/> is one we recognise.</summary>
    public static bool IsValid(string? tone)
        => tone is not null && All.Contains(tone, StringComparer.OrdinalIgnoreCase);

    /// <summary>Falls back to Professional for anything unrecognised.</summary>
    public static string Normalise(string? tone)
        => IsValid(tone)
            ? All.First(t => string.Equals(t, tone, StringComparison.OrdinalIgnoreCase))
            : Professional;

    /// <summary>
    /// The instruction appended to the model's system prompt for a tone.
    ///
    /// One sentence each — the base prompt already carries every rule about
    /// grouping, honesty and not stating numbers; this only shifts the voice.
    /// </summary>
    public static string Instruction(string tone) => Normalise(tone) switch
    {
        Formal => "Write each description in a formal, precise register suitable for a corporate client or their accounts department.",
        Friendly => "Write each description in a warm, approachable tone, as you would for a client you have a friendly working relationship with.",
        Concise => "Write each description as concisely as the work allows — short noun phrases, no filler words.",
        _ => "Write each description in a clear, professional tone.",
    };
}
