namespace Akinai.Shared.Dunning;

/// <summary>
/// The rungs of the ladder, in the order a client climbs them.
///
/// Four was chosen deliberately. Three leaves no room between "friendly" and
/// "this is now a problem", and anything past four starts to read as an
/// automated drip campaign — the moment a client decides your reminders are
/// machinery rather than a person waiting to be paid, they stop reading them.
///
/// The numeric values are the escalation order and are relied on for
/// comparison, so new stages must be inserted with care rather than appended:
/// a stage that sits between two existing ones needs renumbering, and renumbering
/// changes the meaning of values already stored against sent reminders.
/// </summary>
public enum DunningStage
{
    /// <summary>
    /// Sent <em>before</em> the due date. Not a chase at all — it exists because
    /// a large share of late payment is a client's accounts payable run simply
    /// not having seen the invoice yet, and a nudge that arrives while there is
    /// still time to act costs nothing and removes the need for every later stage.
    /// </summary>
    AdvanceNotice = 0,

    /// <summary>
    /// Days after the due date. Assumes oversight, not refusal, and says so —
    /// at this point that is usually true, and being wrong in the accusing
    /// direction is far more expensive than being wrong in the forgiving one.
    /// </summary>
    GentleReminder = 1,

    /// <summary>
    /// Weeks after the due date. Drops the assumption of oversight and asks for
    /// a commitment — either payment or a date — because by now silence is the
    /// actual problem to be solved.
    /// </summary>
    FirmReminder = 2,

    /// <summary>
    /// The last message this system sends. It names a deadline and says what
    /// happens after it, then stops: anything beyond here is a human decision
    /// (collections, legal, writing it off) and must not be automated.
    /// </summary>
    FinalNotice = 3,
}

/// <summary>
/// Why nothing is being sent right now. Carried on the decision rather than
/// collapsed into a bare "no", because the operational questions are all about
/// the reason: "why has this invoice never been chased" has very different
/// answers depending on whether the sequence is finished, the clock has not
/// come round yet, or the client's working day is over.
/// </summary>
public enum DunningSuppression
{
    /// <summary>A stage is due and sendable.</summary>
    None,

    /// <summary>No stage has come round yet — the next trigger date is in the future.</summary>
    NoStageDue,

    /// <summary>Every stage has been sent. The ladder ends here; a human takes it from here.</summary>
    SequenceComplete,

    /// <summary>
    /// Something already reached this client today, in their own calendar day.
    /// Two chasing emails on one day reads as a malfunction, and it undoes the
    /// escalation: the second one cannot be firmer than the first if they arrive
    /// together.
    /// </summary>
    AlreadyContactedToday,

    /// <summary>
    /// The client's local day is one we do not send on. A reminder sitting at the
    /// top of a Monday inbox gets read; one sent on Saturday gets buried under
    /// the weekend's backlog, and it tells the client no one was there to send it.
    /// </summary>
    NonSendingDay,

    /// <summary>
    /// Inside a sending day, but outside working hours in the client's timezone.
    /// This is the whole reason the client's offset is an input: a chase that
    /// lands at 03:00 announces itself as a cron job, and a cron job is much
    /// easier to ignore than a supplier.
    /// </summary>
    OutsideSendingHours,
}

/// <summary>
/// One rung: which stage, and when it comes round relative to the due date.
/// Negative is before the due date, positive after.
/// </summary>
public sealed record DunningStageRule(DunningStage Stage, int DaysRelativeToDue);

/// <summary>
/// A reminder that has already gone out for this invoice.
/// </summary>
/// <param name="Stage">Which rung was sent.</param>
/// <param name="SentAt">
/// When it was sent, as an absolute instant. Stored as an offset rather than a
/// local date because the "one per day" rule is evaluated in the <em>client's</em>
/// calendar day, which we cannot reconstruct from a bare local timestamp of ours.
/// </param>
public sealed record DunningSendRecord(DunningStage Stage, DateTimeOffset SentAt);

/// <summary>
/// The client's clock, reduced to the one thing the schedule actually needs: an
/// offset from UTC.
///
/// <para>
/// This takes an offset rather than a <see cref="TimeZoneInfo"/> so the schedule
/// stays a pure function of its inputs. Resolving an IANA id depends on the host's
/// timezone database, which differs between a Linux container and a browser running
/// WebAssembly — a schedule that behaves differently on the server and the client
/// would be untestable in exactly the cases that matter. <see cref="FromIanaId"/>
/// is offered for callers who hold an id, and it resolves the offset once, at the
/// edge, at a stated instant.
/// </para>
/// </summary>
public readonly record struct ClientTimeZone
{
    /// <summary>
    /// The offset. Always within ±14 hours and a whole number of minutes, because
    /// <see cref="DateTimeOffset.ToOffset"/> rejects anything else and this type
    /// exists to make that impossible rather than to throw at an awkward moment.
    /// </summary>
    public TimeSpan UtcOffset { get; }

    /// <summary>
    /// Out-of-range or sub-minute offsets are clamped to UTC rather than rejected.
    /// A nonsense value in a client record should cost us a slightly wrong send
    /// hour, not the whole chasing run for every other client in the batch.
    /// </summary>
    public ClientTimeZone(TimeSpan utcOffset)
    {
        var whole = TimeSpan.FromMinutes(Math.Truncate(utcOffset.TotalMinutes));
        UtcOffset = Math.Abs(whole.TotalHours) <= 14 ? whole : TimeSpan.Zero;
    }

    public static ClientTimeZone Utc => new(TimeSpan.Zero);

    public static ClientTimeZone FromOffset(TimeSpan offset) => new(offset);

    /// <summary>
    /// The offset an IANA zone ("Europe/Berlin", "Asia/Seoul") has <em>at a given
    /// instant</em>, so daylight saving is accounted for at the moment we are
    /// deciding about rather than whenever the client record was written.
    ///
    /// Unknown or corrupt ids fall back to UTC instead of throwing, for the same
    /// reason the invoice label lookups fall back to English: a stale string in the
    /// database must not be able to stop an invoice being chased.
    /// </summary>
    public static ClientTimeZone FromIanaId(string? ianaId, DateTimeOffset at)
    {
        if (string.IsNullOrWhiteSpace(ianaId)) return Utc;

        try
        {
            return new ClientTimeZone(TimeZoneInfo.FindSystemTimeZoneById(ianaId).GetUtcOffset(at));
        }
        catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException or ArgumentException)
        {
            return Utc;
        }
    }

    /// <summary>The same instant, expressed on the client's clock.</summary>
    public DateTimeOffset ToClientTime(DateTimeOffset instant) => instant.ToOffset(UtcOffset);

    /// <summary>The client's calendar date at a given instant.</summary>
    public DateOnly ClientDate(DateTimeOffset instant) => DateOnly.FromDateTime(ToClientTime(instant).DateTime);

    /// <summary>The client's wall-clock time at a given instant.</summary>
    public TimeOnly ClientTimeOfDay(DateTimeOffset instant) => TimeOnly.FromDateTime(ToClientTime(instant).DateTime);
}

/// <summary>
/// When the ladder's rungs fall, and the hours a client will tolerate being
/// chased in. Configurable because none of it is universal — but the defaults
/// are the ones most freelancers would pick if asked, so the common case needs
/// no decisions.
/// </summary>
public sealed record DunningPolicy
{
    /// <summary>
    /// The rungs. Escalation order is taken from <see cref="DunningStageRule.DaysRelativeToDue"/>
    /// rather than from the enum, so a user who moves a stage earlier gets the
    /// ordering they asked for instead of a sequence that silently contradicts
    /// its own dates.
    /// </summary>
    public IReadOnlyList<DunningStageRule> Stages { get; init; } =
    [
        new(DunningStage.AdvanceNotice, -3),
        new(DunningStage.GentleReminder, 3),
        new(DunningStage.FirmReminder, 14),
        new(DunningStage.FinalNotice, 30),
    ];

    /// <summary>
    /// Earliest local send time. Not 09:00 on the dot: a reminder timed to the
    /// top of the hour looks generated, and the first few minutes of the working
    /// day are when inboxes are triaged hardest.
    /// </summary>
    public TimeOnly EarliestSendTime { get; init; } = new(9, 30);

    /// <summary>
    /// Latest local send time, exclusive. Deliberately before the end of the
    /// working day — a chase arriving at 17:55 cannot be acted on until tomorrow,
    /// so it buys a night of being the unread thing at the top of the inbox and
    /// nothing else.
    /// </summary>
    public TimeOnly LatestSendTime { get; init; } = new(16, 30);

    /// <summary>
    /// Days we do not send on, in the client's week.
    ///
    /// A set rather than a hardcoded Saturday/Sunday because this app bills across
    /// borders: the working week is Sunday–Thursday in much of the Gulf, and
    /// getting that wrong sends the firmest message of the sequence into a weekend.
    /// </summary>
    public IReadOnlySet<DayOfWeek> NonSendingDays { get; init; } =
        new HashSet<DayOfWeek> { DayOfWeek.Saturday, DayOfWeek.Sunday };

    /// <summary>The policy used when a caller does not supply one.</summary>
    public static DunningPolicy Default { get; } = new();

    /// <summary>Stages in escalation order: by trigger day, then by stage.</summary>
    public IReadOnlyList<DunningStageRule> InEscalationOrder() =>
        [.. Stages.OrderBy(s => s.DaysRelativeToDue).ThenBy(s => s.Stage)];
}

/// <summary>
/// What to do about one overdue invoice at one moment.
/// </summary>
/// <param name="Stage">
/// The stage in question. Populated even when suppressed — knowing that a final
/// notice is sitting held until Monday morning is worth far more in a log than
/// a bare "nothing to do".
/// </param>
public sealed record DunningDecision(DunningStage? Stage, DunningSuppression Suppression)
{
    /// <summary>
    /// The only thing a caller should branch on before sending. Both halves are
    /// checked: a stage with a suppression reason is a stage we are holding, and
    /// reading <see cref="Stage"/> alone would send it anyway.
    /// </summary>
    public bool ShouldSend => Stage is not null && Suppression == DunningSuppression.None;

    public static DunningDecision Send(DunningStage stage) => new(stage, DunningSuppression.None);

    public static DunningDecision Hold(DunningStage? stage, DunningSuppression why) => new(stage, why);
}

/// <summary>
/// Decides whether an overdue invoice should be chased right now, and if so with
/// which stage of the escalation.
///
/// <para>
/// It knows nothing about email, invoices, the database or the clock. Everything
/// it needs — the due date, "now", the client's offset, and what has already been
/// sent — is passed in, so the awkward cases (a Sunday at 03:00 in Seoul for an
/// invoice whose firm reminder already went out) are ordinary unit tests rather
/// than something only production can tell you about.
/// </para>
///
/// <para>
/// <b>Two invariants are the point of this type.</b> A stage can never be sent
/// twice, and a client can never be contacted twice in one of their calendar
/// days. Both are enforced here against the history the caller supplies, which
/// means the caller's side of the bargain is to record a send <em>before</em> or
/// atomically with sending it: this is a pure function, and it cannot protect a
/// client from a reminder it was never told about.
/// </para>
/// </summary>
public static class DunningSchedule
{
    /// <summary>
    /// The stage due now, or nothing, with the reason.
    /// </summary>
    /// <param name="dueDate">The invoice's due date, in the terms the client agreed.</param>
    /// <param name="now">The moment being decided about.</param>
    /// <param name="clientTimeZone">
    /// The <em>client's</em> offset — not ours. The whole value of the courtesy is
    /// that it is measured in their working day.
    /// </param>
    /// <param name="alreadySent">
    /// Every stage already sent for this invoice. Order does not matter; gaps do
    /// not matter. Anything in here can never be sent again.
    /// </param>
    /// <param name="otherContactWithClient">
    /// Reminders already sent to the same client today for <em>other</em> invoices.
    /// A client with four late invoices is one relationship, not four, and four
    /// chasing emails in one morning ends it regardless of how carefully each one
    /// was worded. Callers that chase a single invoice in isolation can leave this
    /// empty and lose only this protection.
    /// </param>
    /// <param name="policy">Timing and working hours. Defaults to <see cref="DunningPolicy.Default"/>.</param>
    public static DunningDecision Evaluate(
        DateOnly dueDate,
        DateTimeOffset now,
        ClientTimeZone clientTimeZone,
        IReadOnlyList<DunningSendRecord>? alreadySent = null,
        IReadOnlyList<DateTimeOffset>? otherContactWithClient = null,
        DunningPolicy? policy = null)
    {
        policy ??= DunningPolicy.Default;
        alreadySent ??= [];
        otherContactWithClient ??= [];

        var today = clientTimeZone.ClientDate(now);
        var timeOfDay = clientTimeZone.ClientTimeOfDay(now);

        var ladder = policy.InEscalationOrder();
        if (ladder.Count == 0) return DunningDecision.Hold(null, DunningSuppression.SequenceComplete);

        var sentStages = alreadySent.Select(s => s.Stage).ToHashSet();

        // Escalation is one-way. The highest rung already sent is the floor: a
        // client who has had a firm reminder must never then receive the gentle
        // one, which would read as the supplier losing track of their own file.
        var floor = ladder
            .Select((rule, index) => (rule, index))
            .Where(x => sentStages.Contains(x.rule.Stage))
            .Select(x => (int?)x.index)
            .DefaultIfEmpty(null)
            .Max();

        // The most escalated rung whose date has arrived — not the earliest.
        //
        // This matters for an invoice that was never chased at all: picking the
        // earliest unsent rung would send "your invoice falls due in three days"
        // to someone six weeks overdue, which is worse than silence because it
        // proves no one is reading the account. Jumping to the rung the calendar
        // actually justifies keeps the wording true, at the price of skipping
        // rungs the client never needed to see.
        DunningStageRule? candidate = null;
        for (var i = 0; i < ladder.Count; i++)
        {
            if (floor.HasValue && i <= floor.Value) continue;
            if (sentStages.Contains(ladder[i].Stage)) continue;
            if (dueDate.AddDays(ladder[i].DaysRelativeToDue) > today) break;

            candidate = ladder[i];
        }

        if (candidate is null)
        {
            // Nothing left to send is a different situation from nothing due yet,
            // and only the first of the two ever needs a human to look at it.
            var exhausted = floor == ladder.Count - 1 || sentStages.Count >= ladder.Count;
            return DunningDecision.Hold(null, exhausted
                ? DunningSuppression.SequenceComplete
                : DunningSuppression.NoStageDue);
        }

        var stage = candidate.Stage;

        // One contact per client per client-day, counting this invoice's own
        // history and anything else that reached them today. Checked first
        // because it is the invariant whose breach the client actually notices.
        var contactedToday =
            alreadySent.Any(s => clientTimeZone.ClientDate(s.SentAt) == today) ||
            otherContactWithClient.Any(at => clientTimeZone.ClientDate(at) == today);

        if (contactedToday) return DunningDecision.Hold(stage, DunningSuppression.AlreadyContactedToday);

        if (policy.NonSendingDays.Contains(today.DayOfWeek))
            return DunningDecision.Hold(stage, DunningSuppression.NonSendingDay);

        // Upper bound exclusive: LatestSendTime is the point past which we would
        // rather wait for tomorrow morning than catch the end of today.
        if (timeOfDay < policy.EarliestSendTime || timeOfDay >= policy.LatestSendTime)
            return DunningDecision.Hold(stage, DunningSuppression.OutsideSendingHours);

        return DunningDecision.Send(stage);
    }

    /// <summary>
    /// The first instant at or after <paramref name="notBefore"/> that falls inside
    /// the client's sending window.
    ///
    /// For schedulers and for telling the user when a held reminder will actually
    /// go — "waiting until Monday 09:30 their time" is an answer, where "held" is
    /// only an excuse. It deliberately ignores send history: this answers "when is
    /// this client reachable", not "what will be sent".
    /// </summary>
    public static DateTimeOffset NextSendableMoment(
        DateTimeOffset notBefore,
        ClientTimeZone clientTimeZone,
        DunningPolicy? policy = null)
    {
        policy ??= DunningPolicy.Default;

        var local = clientTimeZone.ToClientTime(notBefore);
        var date = DateOnly.FromDateTime(local.DateTime);
        var time = TimeOnly.FromDateTime(local.DateTime);

        if (time >= policy.LatestSendTime)
        {
            date = date.AddDays(1);
            time = policy.EarliestSendTime;
        }
        else if (time < policy.EarliestSendTime)
        {
            time = policy.EarliestSendTime;
        }

        // Bounded rather than a while(true): a policy that marks every day as
        // non-sending is a configuration mistake, and the right response to it is
        // a slightly wrong answer, not a hung batch job.
        for (var i = 0; i < 14 && policy.NonSendingDays.Contains(date.DayOfWeek); i++)
        {
            date = date.AddDays(1);
            time = policy.EarliestSendTime;
        }

        return new DateTimeOffset(date.ToDateTime(time), clientTimeZone.UtcOffset);
    }
}
