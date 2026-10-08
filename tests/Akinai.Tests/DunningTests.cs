using FluentAssertions;
using Akinai.Shared.Dunning;
using Akinai.Shared.Enums;

namespace Akinai.Tests;

/// <summary>
/// The two promises the dunning ladder makes to a client — a stage is never
/// repeated, and nothing reaches them twice in one of their own days — plus the
/// courtesies that stop a chase reading as machinery: no weekends, no 3am.
///
/// Everything here is a pure function of its inputs, which is the point: the
/// awkward cases (Monday morning in Seoul, a Saturday final notice, a reminder
/// sent at 23:00 UTC that is tomorrow for the client) are ordinary tests rather
/// than something only a production inbox could tell us about.
/// </summary>
public class DunningTests
{
    // Monday. Chosen so that every default trigger lands on a weekday
    // (advance Fri 17 Jul, gentle Thu 23 Jul, firm Mon 3 Aug, final Wed 19 Aug),
    // which keeps the escalation tests about escalation and not about the calendar.
    private static readonly DateOnly Due = new(2026, 7, 20);

    private static readonly ClientTimeZone Utc = ClientTimeZone.Utc;
    private static readonly ClientTimeZone Seoul = ClientTimeZone.FromOffset(TimeSpan.FromHours(9));
    private static readonly ClientTimeZone LosAngeles = ClientTimeZone.FromOffset(TimeSpan.FromHours(-8));

    /// <summary>An instant whose <em>client-local</em> wall clock is the given date and time.</summary>
    private static DateTimeOffset LocalAt(ClientTimeZone zone, DateOnly date, int hour, int minute = 0)
        => new(date.ToDateTime(new TimeOnly(hour, minute)), zone.UtcOffset);

    /// <summary>Mid-morning in the client's day: safely inside any sane sending window.</summary>
    private static DateTimeOffset LocalMidMorning(ClientTimeZone zone, DateOnly date) => LocalAt(zone, date, 10);

    [Fact]
    public void The_ladder_is_climbed_in_order_one_rung_at_a_time()
    {
        // Walk a real calendar day by day, recording what actually goes out, the
        // way a nightly job would. Nothing is asserted about when each rung falls;
        // only that they arrive in escalating order, once each.
        var sent = new List<DunningSendRecord>();
        var log = new List<(DunningStage Stage, DateOnly On)>();

        for (var day = Due.AddDays(-10); day <= Due.AddDays(45); day = day.AddDays(1))
        {
            var now = LocalMidMorning(Utc, day);
            var decision = DunningSchedule.Evaluate(Due, now, Utc, sent);
            if (!decision.ShouldSend) continue;

            log.Add((decision.Stage!.Value, day));
            sent.Add(new DunningSendRecord(decision.Stage!.Value, now));
        }

        log.Select(x => x.Stage).Should().Equal(
            DunningStage.AdvanceNotice,
            DunningStage.GentleReminder,
            DunningStage.FirmReminder,
            DunningStage.FinalNotice);

        // One per day, with real distance between the rungs — four emails in four
        // days would technically satisfy "escalating order" and would still be the
        // worst version of this feature.
        log.Select(x => x.On).Should().OnlyHaveUniqueItems();
        log[^1].On.Should().BeOnOrAfter(log[0].On.AddDays(21));

        // And then it stops. Anything past the final notice is a human's decision.
        var after = DunningSchedule.Evaluate(Due, LocalMidMorning(Utc, Due.AddDays(60)), Utc, sent);
        after.ShouldSend.Should().BeFalse();
        after.Suppression.Should().Be(DunningSuppression.SequenceComplete);
    }

    [Fact]
    public void A_stage_that_has_been_sent_can_never_be_sent_again()
    {
        var sent = new List<DunningSendRecord>
        {
            new(DunningStage.GentleReminder, LocalMidMorning(Utc, Due.AddDays(3))),
        };

        // Every day between the gentle reminder and the firm one: the gentle rung
        // is gone, and the firm rung has not come round, so there is nothing to do.
        for (var day = Due.AddDays(4); day < Due.AddDays(14); day = day.AddDays(1))
        {
            var decision = DunningSchedule.Evaluate(Due, LocalMidMorning(Utc, day), Utc, sent);
            decision.ShouldSend.Should().BeFalse($"nothing is due on {day:O}");
            decision.Stage.Should().BeNull();
            decision.Suppression.Should().Be(DunningSuppression.NoStageDue);
        }
    }

    [Fact]
    public void Escalation_never_runs_backwards()
    {
        // A firm reminder has gone out, but the gentle one somehow never did —
        // a user who enabled chasing late, or a send that failed. The client must
        // not now receive the soft "this may simply have been missed" message:
        // after a firm reminder it reads as the supplier having lost their own file.
        var sent = new List<DunningSendRecord>
        {
            new(DunningStage.FirmReminder, LocalMidMorning(Utc, Due.AddDays(14))),
        };

        var decision = DunningSchedule.Evaluate(Due, LocalMidMorning(Utc, Due.AddDays(20)), Utc, sent);

        decision.Stage.Should().NotBe(DunningStage.GentleReminder);
        decision.Stage.Should().NotBe(DunningStage.AdvanceNotice);
        decision.ShouldSend.Should().BeFalse("the final notice is not due for another ten days");
    }

    [Fact]
    public void An_invoice_that_was_never_chased_starts_at_the_rung_the_calendar_justifies()
    {
        // Chasing switched on six weeks late (on a Monday, so the weekend rule is
        // not what is being tested). Sending "your invoice falls due in three days"
        // to someone six weeks overdue proves nobody is reading the account, which
        // is worse than having said nothing at all.
        var decision = DunningSchedule.Evaluate(Due, LocalMidMorning(Utc, Due.AddDays(42)), Utc);

        decision.ShouldSend.Should().BeTrue();
        decision.Stage.Should().Be(DunningStage.FinalNotice);
    }

    [Fact]
    public void Two_reminders_can_never_land_on_the_same_day()
    {
        var firmDay = Due.AddDays(14);

        // Something already reached this client this morning. The firm reminder is
        // genuinely due today, and is held anyway.
        var sent = new List<DunningSendRecord>
        {
            new(DunningStage.GentleReminder, LocalAt(Utc, firmDay, 9, 45)),
        };

        var decision = DunningSchedule.Evaluate(Due, LocalAt(Utc, firmDay, 12), Utc, sent);

        decision.ShouldSend.Should().BeFalse();
        decision.Suppression.Should().Be(DunningSuppression.AlreadyContactedToday);
        // The stage still comes back, so an operator can see what is being held.
        decision.Stage.Should().Be(DunningStage.FirmReminder);

        // Tomorrow it goes.
        var tomorrow = DunningSchedule.Evaluate(Due, LocalMidMorning(Utc, firmDay.AddDays(1)), Utc, sent);
        tomorrow.ShouldSend.Should().BeTrue();
        tomorrow.Stage.Should().Be(DunningStage.FirmReminder);
    }

    [Fact]
    public void A_reminder_sent_today_for_another_invoice_also_blocks_this_one()
    {
        // A client with four late invoices is one relationship, not four. Four
        // chasing emails in one morning ends it however carefully each was worded.
        var firmDay = Due.AddDays(14);
        var otherInvoiceChasedThisMorning = new[] { LocalAt(Utc, firmDay, 9, 40) };

        var decision = DunningSchedule.Evaluate(
            Due, LocalAt(Utc, firmDay, 12), Utc,
            alreadySent: null,
            otherContactWithClient: otherInvoiceChasedThisMorning);

        decision.ShouldSend.Should().BeFalse();
        decision.Suppression.Should().Be(DunningSuppression.AlreadyContactedToday);
    }

    [Fact]
    public void One_per_day_is_measured_in_the_clients_day_not_ours()
    {
        // 23:00 UTC on the 2nd is already 08:00 on the 3rd in Seoul. For a Seoul
        // client that reminder arrived "today"; for a UTC client it arrived
        // yesterday. The invariant is about what the client experienced.
        var firmDay = Due.AddDays(14);                        // Monday 3 August
        var lateYesterdayUtc = new DateTimeOffset(2026, 8, 2, 23, 0, 0, TimeSpan.Zero);
        var sent = new[] { new DunningSendRecord(DunningStage.GentleReminder, lateYesterdayUtc) };

        var seoul = DunningSchedule.Evaluate(Due, LocalMidMorning(Seoul, firmDay), Seoul, sent);
        seoul.Suppression.Should().Be(DunningSuppression.AlreadyContactedToday);

        var utc = DunningSchedule.Evaluate(Due, LocalMidMorning(Utc, firmDay), Utc, sent);
        utc.ShouldSend.Should().BeTrue();
        utc.Stage.Should().Be(DunningStage.FirmReminder);
    }

    [Fact]
    public void Nothing_is_sent_at_the_weekend_it_waits_for_Monday()
    {
        // Due on Saturday 25 July, so the firm reminder falls on Saturday 8 August.
        var saturdayDue = new DateOnly(2026, 7, 25);
        var saturday = new DateOnly(2026, 8, 8);
        var sunday = saturday.AddDays(1);
        var monday = saturday.AddDays(2);

        foreach (var weekendDay in new[] { saturday, sunday })
        {
            var held = DunningSchedule.Evaluate(saturdayDue, LocalMidMorning(Utc, weekendDay), Utc);
            held.ShouldSend.Should().BeFalse($"{weekendDay.DayOfWeek} is not a sending day");
            held.Suppression.Should().Be(DunningSuppression.NonSendingDay);
            // Held, not lost.
            held.Stage.Should().Be(DunningStage.FirmReminder);
        }

        var sends = DunningSchedule.Evaluate(saturdayDue, LocalMidMorning(Utc, monday), Utc);
        sends.ShouldSend.Should().BeTrue();
        sends.Stage.Should().Be(DunningStage.FirmReminder);
    }

    [Theory]
    [InlineData(3, 0)]    // the middle of the night
    [InlineData(7, 30)]   // before anyone is at a desk
    [InlineData(16, 30)]  // the upper bound is exclusive
    [InlineData(22, 15)]  // the evening
    public void Nothing_is_sent_outside_the_clients_working_hours(int hour, int minute)
    {
        var firmDay = Due.AddDays(14);

        var decision = DunningSchedule.Evaluate(Due, LocalAt(Utc, firmDay, hour, minute), Utc);

        decision.ShouldSend.Should().BeFalse();
        decision.Suppression.Should().Be(DunningSuppression.OutsideSendingHours);
        decision.Stage.Should().Be(DunningStage.FirmReminder);
    }

    [Fact]
    public void Working_hours_are_the_clients_hours_not_the_servers()
    {
        // 11:00 UTC is a perfectly civil hour on our clock and 03:00 in Los
        // Angeles. This is the entire reason the client's offset is an input.
        var firmDay = Due.AddDays(14);
        var elevenUtc = new DateTimeOffset(2026, 8, 3, 11, 0, 0, TimeSpan.Zero);

        var fromOurChair = DunningSchedule.Evaluate(Due, elevenUtc, Utc);
        fromOurChair.ShouldSend.Should().BeTrue("11:00 is mid-morning for a UTC client");

        var inLosAngeles = DunningSchedule.Evaluate(Due, elevenUtc, LosAngeles);
        inLosAngeles.ShouldSend.Should().BeFalse();
        inLosAngeles.Suppression.Should().Be(DunningSuppression.OutsideSendingHours);

        firmDay.Should().Be(new DateOnly(2026, 8, 3), "the dates above are hand-written for readability");
    }

    [Fact]
    public void A_broken_timezone_id_costs_us_the_send_hour_and_nothing_else()
    {
        // A stale or corrupt zone string on a client record must not be able to
        // stop the chasing run for everyone else in the batch.
        var zone = ClientTimeZone.FromIanaId("Mars/Olympus_Mons", DateTimeOffset.UtcNow);
        zone.UtcOffset.Should().Be(TimeSpan.Zero);

        // Nor may an impossible offset: clamped rather than thrown.
        ClientTimeZone.FromOffset(TimeSpan.FromHours(37)).UtcOffset.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void A_held_reminder_can_say_when_it_will_actually_go()
    {
        // "Held" is an excuse; "waiting until Monday 09:30 their time" is an answer.
        var saturdayEvening = LocalAt(Utc, new DateOnly(2026, 8, 8), 19);
        var next = DunningSchedule.NextSendableMoment(saturdayEvening, Utc);

        next.Should().Be(LocalAt(Utc, new DateOnly(2026, 8, 10), 9, 30));

        // Too early in the day: later the same morning.
        var earlyMonday = LocalAt(Utc, new DateOnly(2026, 8, 10), 6);
        DunningSchedule.NextSendableMoment(earlyMonday, Utc)
            .Should().Be(LocalAt(Utc, new DateOnly(2026, 8, 10), 9, 30));

        // Too late in the day: tomorrow morning.
        var mondayNight = LocalAt(Utc, new DateOnly(2026, 8, 10), 23);
        DunningSchedule.NextSendableMoment(mondayNight, Utc)
            .Should().Be(LocalAt(Utc, new DateOnly(2026, 8, 11), 9, 30));
    }

    [Fact]
    public void A_client_whose_weekend_is_Friday_and_Saturday_is_left_alone_then()
    {
        // Billing across borders: the working week is Sunday to Thursday in much
        // of the Gulf, and a default Saturday/Sunday rule would push the firmest
        // message of the sequence into the client's weekend.
        var policy = DunningPolicy.Default with
        {
            NonSendingDays = new HashSet<DayOfWeek> { DayOfWeek.Friday, DayOfWeek.Saturday },
        };

        var friday = new DateOnly(2026, 8, 7);
        var sunday = new DateOnly(2026, 8, 9);
        var saturdayDue = new DateOnly(2026, 7, 24);          // firm reminder: Friday 7 August

        var onFriday = DunningSchedule.Evaluate(saturdayDue, LocalMidMorning(Utc, friday), Utc, policy: policy);
        onFriday.Suppression.Should().Be(DunningSuppression.NonSendingDay);

        var onSunday = DunningSchedule.Evaluate(saturdayDue, LocalMidMorning(Utc, sunday), Utc, policy: policy);
        onSunday.ShouldSend.Should().BeTrue("Sunday is a working day for this client");
    }

    // ---- Wording -----------------------------------------------------------

    private static IEnumerable<SupportedLanguage> AllLanguages => Enum.GetValues<SupportedLanguage>();

    private static IEnumerable<DunningStage> AllStages => Enum.GetValues<DunningStage>();

    [Fact]
    public void Every_supported_language_is_written_out_rather_than_falling_back_to_English()
    {
        // The English fallback is a safety net, and a safety net hides holes: a new
        // language added to the enum and forgotten in the table would quietly chase
        // that whole market in English, and nothing would fail.
        foreach (var language in AllLanguages)
            DunningTemplates.HasTemplates(language).Should().BeTrue($"{language} needs its own dunning wording");
    }

    [Fact]
    public void Every_language_has_every_stage_populated()
    {
        // A missing entry must never be able to reach a client as an empty email.
        foreach (var language in AllLanguages)
        {
            var set = DunningTemplates.For(language);

            foreach (var stage in AllStages)
            {
                var message = set.For(stage);

                message.Subject.Should().NotBeNullOrWhiteSpace($"{language}/{stage} subject");
                message.Body.Should().NotBeNullOrWhiteSpace($"{language}/{stage} body");
                // Length rather than emptiness, because a placeholder stub ("TODO",
                // a bare invoice number) is just as unsendable as a blank one.
                message.Body.Length.Should().BeGreaterThan(80, $"{language}/{stage} body looks like a stub");
            }

            // Four rungs that say the same thing are not an escalation.
            var bodies = AllStages.Select(s => set.For(s).Body).ToList();
            bodies.Should().OnlyHaveUniqueItems($"{language} must escalate, not repeat");
            AllStages.Select(s => set.For(s).Subject).Should().OnlyHaveUniqueItems($"{language} subjects");
        }
    }

    [Fact]
    public void Every_stage_can_name_the_invoice_and_the_money()
    {
        // The facts a client needs to act are the invoice number, the amount and
        // the due date. A reminder missing any of them makes the client come back
        // and ask, which costs another round trip at exactly the wrong moment.
        foreach (var language in AllLanguages)
        foreach (var stage in AllStages)
        {
            var message = DunningTemplates.For(language).For(stage);
            var where = $"{language}/{stage}";

            message.Body.Should().Contain("{0}", $"{where} must name the invoice");
            message.Body.Should().Contain("{1}", $"{where} must state the amount");
            message.Body.Should().Contain("{2}", $"{where} must state the due date");

            // Days overdue is the one that only makes sense once it is overdue.
            if (stage != DunningStage.AdvanceNotice)
                message.Body.Should().Contain("{3}", $"{where} must say how late it is");
        }
    }

    [Fact]
    public void Rendering_fills_every_placeholder_and_leaves_no_braces_behind()
    {
        foreach (var language in AllLanguages)
        foreach (var stage in AllStages)
        {
            var rendered = DunningTemplates.Render(
                language, stage, "INV-00042", "€1,250.00", "20 July 2026", daysPastDue: 14);

            rendered.Subject.Should().NotContain("{").And.NotContain("}");
            rendered.Body.Should().NotContain("{").And.NotContain("}");
            rendered.Body.Should().Contain("INV-00042").And.Contain("€1,250.00").And.Contain("20 July 2026");
        }
    }

    [Fact]
    public void The_advance_notice_can_never_tell_a_client_they_are_minus_three_days_overdue()
    {
        // It runs before the due date, so the day count arrives negative. Clamped,
        // because "-3 days overdue" in any language is the tell that nobody checked.
        foreach (var language in AllLanguages)
        {
            var rendered = DunningTemplates.Render(
                language, DunningStage.AdvanceNotice, "INV-1", "£100.00", "20 July 2026", daysPastDue: -3);

            rendered.Body.Should().NotContain("-3");
            rendered.Subject.Should().NotContain("-3");
        }
    }

    [Fact]
    public void An_unknown_language_name_falls_back_to_English_rather_than_throwing()
    {
        // The language is pinned to the invoice as a string, so a stale value must
        // chase the client in the wrong language rather than not at all.
        DunningTemplates.For("Klingon").Should().BeSameAs(DunningTemplates.For(SupportedLanguage.English));
        DunningTemplates.For((string?)null).Should().BeSameAs(DunningTemplates.For(SupportedLanguage.English));
        DunningTemplates.For("korean").Should().BeSameAs(DunningTemplates.For(SupportedLanguage.Korean));
    }

    [Fact]
    public void Each_language_formats_its_own_dates_and_numbers()
    {
        // The culture is what makes "1.250,00" and "1,250.00" come out the way the
        // reader expects; an unresolvable one falls back to invariant rather than
        // throwing, because WebAssembly may not have loaded its ICU data.
        foreach (var language in AllLanguages)
            DunningTemplates.For(language).Culture.Should().NotBeNull();
    }
}
