using Akinai.Domain.Exceptions;
using Akinai.Domain.Services;
using FluentAssertions;
using static Akinai.Domain.Services.PaymentTiming;

namespace Akinai.Tests;

/// <summary>
/// The value of this prediction is that it comes from the user's own ledger, so the thing
/// worth pinning is not the arithmetic but the judgement around it: that the most specific
/// slice of history wins, that one absurd payment cannot move the answer, and that thin
/// history produces a refusal rather than a number that looks exactly like a confident one.
///
/// Every test fixes the clock and the dates. A forecast that changes depending on when the
/// suite runs is not a forecast anybody can reason about.
/// </summary>
public class PaymentTimingTests
{
    private static readonly DateOnly Today = new(2026, 10, 8);

    private static readonly Guid Acme = Guid.NewGuid();      // the client being asked about
    private static readonly Guid Hanoi = Guid.NewGuid();     // another client, same country
    private static readonly Guid Saigon = Guid.NewGuid();    // and another
    private static readonly Guid Berlin = Guid.NewGuid();     // a client somewhere else entirely

    private const int TermDays = 30;

    /// <summary>A settled invoice, described by the only thing that matters: how late it was.</summary>
    private static Observation Paid(Guid client, string? country, int daysAfterDue, decimal amount = 1_000m)
    {
        var issue = new DateOnly(2026, 1, 1);
        var due = issue.AddDays(TermDays);
        return new(client, country, issue, due, due.AddDays(daysAfterDue), amount);
    }

    /// <summary>The unpaid invoice, on the same net-30 terms as the history.</summary>
    private static Query Unpaid(
        Guid client,
        string? country,
        DateOnly due,
        decimal amount = 1_000m,
        DateOnly? viewedOn = null)
        => new(client, country, due.AddDays(-TermDays), due, amount, viewedOn);

    private static Observation[] PaidRepeatedly(Guid client, string? country, int count, int daysAfterDue)
        => Enumerable.Range(0, count).Select(_ => Paid(client, country, daysAfterDue)).ToArray();

    // -- Choosing a basis ---------------------------------------------------------------

    [Fact]
    public void This_clients_own_history_beats_the_country_and_the_global_pool()
    {
        // Acme pays in a week; everyone else in Vietnam takes a month. The whole feature is
        // worthless if Acme's invoice is predicted from everyone else's behaviour.
        var history = PaidRepeatedly(Acme, "VN", 4, daysAfterDue: 9)
            .Concat(PaidRepeatedly(Hanoi, "VN", 20, daysAfterDue: 34))
            .Concat(PaidRepeatedly(Berlin, "DE", 20, daysAfterDue: 1))
            .ToList();

        var due = Today.AddDays(14);
        var prediction = Predict(history, Unpaid(Acme, "VN", due), Today);

        prediction.Basis.Should().Be(PaymentTiming.Basis.Client);
        prediction.SampleSize.Should().Be(4);
        prediction.MedianDaysAfterDue.Should().Be(9);
        prediction.ExpectedPaymentDate.Should().Be(due.AddDays(9));
    }

    [Fact]
    public void A_new_client_is_predicted_from_the_rest_of_their_country()
    {
        // Nothing from Acme at all. Vietnam is the next most specific thing known.
        var history = new[]
        {
            Paid(Hanoi, "VN", 30),
            Paid(Hanoi, "VN", 32),
            Paid(Saigon, "VN", 34),
            Paid(Saigon, "VN", 36),
            Paid(Saigon, "VN", 38),
            Paid(Berlin, "DE", 1),
            Paid(Berlin, "DE", 2),
            Paid(Berlin, "DE", 2),
        };

        var due = Today.AddDays(14);
        var prediction = Predict(history, Unpaid(Acme, "VN", due), Today);

        prediction.Basis.Should().Be(PaymentTiming.Basis.Country);
        prediction.SampleSize.Should().Be(5);
        prediction.MedianDaysAfterDue.Should().Be(34);
        prediction.Explanation.Should().Contain("from the same country");
    }

    [Fact]
    public void A_clients_too_few_invoices_still_count_towards_their_country()
    {
        // Falling back widens the sample; it never discards what was already there.
        var history = new[]
        {
            Paid(Acme, "VN", 20),
            Paid(Acme, "VN", 22),
            Paid(Hanoi, "VN", 24),
            Paid(Saigon, "VN", 26),
        };

        var prediction = Predict(history, Unpaid(Acme, "VN", Today.AddDays(14)), Today);

        prediction.Basis.Should().Be(PaymentTiming.Basis.Country);
        prediction.SampleSize.Should().Be(4);
    }

    [Fact]
    public void A_new_client_in_a_new_country_falls_all_the_way_back_to_everything_paid()
    {
        var history = new[]
        {
            Paid(Berlin, "DE", 5),
            Paid(Berlin, "DE", 6),
            Paid(Hanoi, "VN", 7),
            Paid(Saigon, "VN", 8),
        };

        var due = Today.AddDays(14);
        var prediction = Predict(history, Unpaid(Acme, "BR", due), Today);

        prediction.Basis.Should().Be(PaymentTiming.Basis.Global);
        prediction.SampleSize.Should().Be(4);
        // Even middles average to 6.5 and round towards later, never towards sooner.
        prediction.MedianDaysAfterDue.Should().Be(7);
        prediction.Confidence.Should().Be(PaymentTiming.Confidence.Low);
    }

    [Fact]
    public void A_missing_country_is_not_pooled_with_every_other_missing_country()
    {
        // "Unknown" is not a place. Matching unknowns to each other would manufacture a
        // cohort and then describe it confidently as the client's region.
        var history = new[]
        {
            Paid(Hanoi, null, 11),
            Paid(Saigon, null, 12),
            Paid(Berlin, null, 13),
        };

        var prediction = Predict(history, Unpaid(Acme, null, Today.AddDays(14)), Today);

        prediction.Basis.Should().Be(PaymentTiming.Basis.Global);
    }

    [Fact]
    public void Country_codes_match_whatever_case_and_spacing_they_arrive_in()
    {
        var history = PaidRepeatedly(Hanoi, " vn ", 4, daysAfterDue: 15);

        var prediction = Predict(history, Unpaid(Acme, "VN", Today.AddDays(14)), Today);

        prediction.Basis.Should().Be(PaymentTiming.Basis.Country);
        prediction.MedianDaysAfterDue.Should().Be(15);
    }

    // -- Refusing to answer -------------------------------------------------------------

    [Fact]
    public void Two_paid_invoices_are_a_pair_of_data_points_not_a_pattern()
    {
        var history = new[] { Paid(Acme, "VN", 9), Paid(Acme, "VN", 11) };

        var prediction = Predict(history, Unpaid(Acme, "VN", Today.AddDays(14)), Today);

        prediction.Basis.Should().Be(PaymentTiming.Basis.None);
        prediction.HasPrediction.Should().BeFalse();
        prediction.Confidence.Should().Be(PaymentTiming.Confidence.None);
        prediction.ExpectedPaymentDate.Should().BeNull();
        prediction.EarliestLikelyDate.Should().BeNull();
        prediction.LatestLikelyDate.Should().BeNull();
        prediction.MedianDaysAfterDue.Should().BeNull();
        prediction.Explanation.Should().Contain("not a pattern");
    }

    [Fact]
    public void An_empty_ledger_says_there_is_nothing_to_predict_from()
    {
        var prediction = Predict([], Unpaid(Acme, "VN", Today.AddDays(14)), Today);

        prediction.Basis.Should().Be(PaymentTiming.Basis.None);
        prediction.SampleSize.Should().Be(0);
        prediction.Explanation.Should().Contain("No invoices have been paid yet");
    }

    [Fact]
    public void Impossible_history_rows_are_dropped_rather_than_believed()
    {
        // A payment date before the issue date is a data fault, not a very fast client.
        // Dropping it keeps one bad row from both poisoning the median and throwing while
        // an invoice list is rendering.
        var issue = new DateOnly(2026, 1, 1);
        var history = new[]
        {
            Paid(Acme, "VN", 9),
            Paid(Acme, "VN", 10),
            Paid(Acme, "VN", 11),
            new Observation(Acme, "VN", issue, issue.AddDays(TermDays), issue.AddDays(-400), 1_000m),
        };

        var prediction = Predict(history, Unpaid(Acme, "VN", Today.AddDays(14)), Today);

        prediction.SampleSize.Should().Be(3);
        prediction.MedianDaysAfterDue.Should().Be(10);
    }

    [Fact]
    public void An_invoice_due_before_it_was_issued_is_rejected_outright()
    {
        var invoice = new Query(Acme, "VN", Today, Today.AddDays(-5), 1_000m);

        var predict = () => Predict(PaidRepeatedly(Acme, "VN", 4, 9), invoice, Today);

        predict.Should().Throw<DomainException>();
    }

    // -- The median doing its job -------------------------------------------------------

    [Fact]
    public void One_client_who_paid_after_three_hundred_days_barely_moves_the_estimate()
    {
        // 2, 3, 4, 5, 300. The mean is 62.8 days, which would tell the user to expect money
        // two months after this client has, in fact, always paid within a week.
        var history = new[]
        {
            Paid(Acme, "VN", 2),
            Paid(Acme, "VN", 3),
            Paid(Acme, "VN", 4),
            Paid(Acme, "VN", 5),
            Paid(Acme, "VN", 300),
        };

        var due = Today.AddDays(14);
        var prediction = Predict(history, Unpaid(Acme, "VN", due), Today);

        prediction.MedianDaysAfterDue.Should().Be(4);
        prediction.ExpectedPaymentDate.Should().Be(due.AddDays(4));

        // And the outlier does not get to define the far end of the window either — that is
        // taken from the upper quartile, so the 300-day row is one rank, not a boundary.
        prediction.LatestLikelyDate.Should().Be(due.AddDays(6));
        prediction.LatestLikelyDate.Should().BeBefore(due.AddDays(30));
    }

    [Fact]
    public void A_perfectly_consistent_client_is_still_given_a_window_rather_than_a_promise()
    {
        // Six payments on exactly day 9 produce a zero-width range, which renders as
        // certainty. Nothing this service can see justifies certainty.
        var history = PaidRepeatedly(Acme, "VN", 6, daysAfterDue: 9);

        var due = Today.AddDays(14);
        var prediction = Predict(history, Unpaid(Acme, "VN", due), Today);

        prediction.ExpectedPaymentDate.Should().Be(due.AddDays(9));
        prediction.EarliestLikelyDate.Should().Be(due.AddDays(7));
        prediction.LatestLikelyDate.Should().Be(due.AddDays(11));
    }

    // -- Confidence ---------------------------------------------------------------------

    [Fact]
    public void Six_consistent_payments_from_the_client_itself_earn_high_confidence()
    {
        var history = PaidRepeatedly(Acme, "VN", 6, daysAfterDue: 9);

        var prediction = Predict(history, Unpaid(Acme, "VN", Today.AddDays(14)), Today);

        prediction.Confidence.Should().Be(PaymentTiming.Confidence.High);
        prediction.Explanation.Should().Contain(
            "This client has paid 6 invoices, typically 9 days after the due date");
    }

    [Fact]
    public void A_client_who_pays_at_wildly_different_speeds_does_not_earn_high_confidence()
    {
        // Same count, same basis, same median-ish answer — but the middle half of the sample
        // spans six weeks, so there is no date here worth asserting.
        var history = new[]
        {
            Paid(Acme, "VN", 1),
            Paid(Acme, "VN", 4),
            Paid(Acme, "VN", 9),
            Paid(Acme, "VN", 14),
            Paid(Acme, "VN", 30),
            Paid(Acme, "VN", 45),
        };

        var prediction = Predict(history, Unpaid(Acme, "VN", Today.AddDays(14)), Today);

        prediction.Confidence.Should().Be(PaymentTiming.Confidence.Moderate);
    }

    [Fact]
    public void No_amount_of_country_history_earns_the_confidence_of_the_client_itself()
    {
        // Twenty invoices, all paid on exactly the same day relative to due — and still
        // capped, because aggregating strangers does not model this client.
        var history = PaidRepeatedly(Hanoi, "VN", 20, daysAfterDue: 34);

        var prediction = Predict(history, Unpaid(Acme, "VN", Today.AddDays(14)), Today);

        prediction.Basis.Should().Be(PaymentTiming.Basis.Country);
        prediction.Confidence.Should().Be(PaymentTiming.Confidence.Moderate);
    }

    [Fact]
    public void The_global_pool_stays_at_low_confidence_however_large_it_is()
    {
        var history = PaidRepeatedly(Berlin, "DE", 20, daysAfterDue: 4);

        var prediction = Predict(history, Unpaid(Acme, "BR", Today.AddDays(14)), Today);

        prediction.Basis.Should().Be(PaymentTiming.Basis.Global);
        prediction.Confidence.Should().Be(PaymentTiming.Confidence.Low);
    }

    [Fact]
    public void An_invoice_far_larger_than_anything_in_the_history_is_trusted_less()
    {
        var history = PaidRepeatedly(Acme, "VN", 6, daysAfterDue: 9);
        var due = Today.AddDays(14);

        var typical = Predict(history, Unpaid(Acme, "VN", due, amount: 1_000m), Today);
        var huge = Predict(history, Unpaid(Acme, "VN", due, amount: 25_000m), Today);

        typical.Confidence.Should().Be(PaymentTiming.Confidence.High);
        huge.Confidence.Should().Be(PaymentTiming.Confidence.Moderate);

        // The date itself is untouched: the size of an invoice is a reason to trust the
        // estimate less, not a reason to invent a different one.
        huge.ExpectedPaymentDate.Should().Be(typical.ExpectedPaymentDate);
        huge.Explanation.Should().Contain("approval step");
    }

    // -- The view signal ----------------------------------------------------------------

    [Fact]
    public void An_unopened_invoice_days_from_being_due_is_trusted_less_and_says_why()
    {
        var history = PaidRepeatedly(Acme, "VN", 6, daysAfterDue: 9);
        var due = Today.AddDays(3);

        var unopened = Predict(history, Unpaid(Acme, "VN", due, viewedOn: null), Today);
        var opened = Predict(history, Unpaid(Acme, "VN", due, viewedOn: Today.AddDays(-20)), Today);

        opened.Confidence.Should().Be(PaymentTiming.Confidence.High);
        unopened.Confidence.Should().Be(PaymentTiming.Confidence.Moderate);
        unopened.Explanation.Should().Contain("has not opened this invoice");

        // Still not a different date: there is no view-to-payment interval in the input, so
        // there is nothing to shift the estimate by that would not be invented.
        unopened.ExpectedPaymentDate.Should().Be(opened.ExpectedPaymentDate);
    }

    [Fact]
    public void An_unopened_invoice_with_weeks_still_to_run_is_left_alone()
    {
        var history = PaidRepeatedly(Acme, "VN", 6, daysAfterDue: 9);

        var prediction = Predict(history, Unpaid(Acme, "VN", Today.AddDays(40), viewedOn: null), Today);

        prediction.Confidence.Should().Be(PaymentTiming.Confidence.High);
        prediction.Explanation.Should().NotContain("has not opened");
    }

    // -- Already overdue ----------------------------------------------------------------

    [Fact]
    public void An_invoice_past_its_predicted_date_is_flagged_rather_than_given_a_date_in_the_past()
    {
        var history = PaidRepeatedly(Acme, "VN", 6, daysAfterDue: 9);
        var due = Today.AddDays(-20);

        var prediction = Predict(history, Unpaid(Acme, "VN", due, viewedOn: Today.AddDays(-25)), Today);

        prediction.IsAlreadyLaterThanUsual.Should().BeTrue();
        prediction.DaysLaterThanUsual.Should().Be(11);

        // Money that has not arrived cannot be predicted to have arrived last week.
        prediction.ExpectedPaymentDate.Should().Be(Today);
        prediction.EarliestLikelyDate.Should().Be(Today);
        prediction.LatestLikelyDate.Should().Be(Today);

        // The pattern has already failed for this invoice, whatever the sample says.
        prediction.Confidence.Should().Be(PaymentTiming.Confidence.Low);
        prediction.Explanation.Should().Contain("worth chasing");
    }

    [Fact]
    public void An_invoice_overdue_but_within_its_clients_usual_lateness_is_not_flagged()
    {
        // Thirty days overdue and entirely normal — this client always pays around day 34.
        // Nagging here is how a good client is lost.
        var history = PaidRepeatedly(Acme, "VN", 6, daysAfterDue: 34);
        var due = Today.AddDays(-30);

        var prediction = Predict(history, Unpaid(Acme, "VN", due, viewedOn: Today.AddDays(-35)), Today);

        prediction.IsAlreadyLaterThanUsual.Should().BeFalse();
        prediction.DaysLaterThanUsual.Should().Be(0);
        prediction.ExpectedPaymentDate.Should().Be(due.AddDays(34));
        prediction.ExpectedPaymentDate.Should().BeOnOrAfter(Today);
    }

    // -- Clients who pay early ----------------------------------------------------------

    [Fact]
    public void A_client_whose_slowest_payment_still_beats_the_due_date_is_reported_as_early()
    {
        var history = new[]
        {
            Paid(Acme, "VN", -10),
            Paid(Acme, "VN", -9),
            Paid(Acme, "VN", -8),
            Paid(Acme, "VN", -7),
            Paid(Acme, "VN", -6),
            Paid(Acme, "VN", -5),
        };

        var due = Today.AddDays(40);
        var prediction = Predict(history, Unpaid(Acme, "VN", due, viewedOn: Today), Today);

        prediction.PaysBeforeDue.Should().BeTrue();
        prediction.MedianDaysAfterDue.Should().Be(-7);
        prediction.ExpectedPaymentDate.Should().Be(due.AddDays(-7));
        prediction.LatestLikelyDate.Should().BeBefore(due);
        prediction.Confidence.Should().Be(PaymentTiming.Confidence.High);
        prediction.Explanation.Should().Contain("7 days before the due date");
        prediction.Explanation.Should().Contain("even their slowest");
    }

    [Fact]
    public void A_client_who_is_usually_early_but_sometimes_very_late_is_not_called_early()
    {
        // The median is on the right side of the due date, and that is not enough: a quarter
        // of these arrived a fortnight late, and telling the user not to watch this one
        // because they "pay early" is how that fortnight goes unnoticed.
        var history = new[]
        {
            Paid(Acme, "VN", -5),
            Paid(Acme, "VN", -3),
            Paid(Acme, "VN", -1),
            Paid(Acme, "VN", 2),
            Paid(Acme, "VN", 12),
            Paid(Acme, "VN", 14),
        };

        var prediction = Predict(history, Unpaid(Acme, "VN", Today.AddDays(40), viewedOn: Today), Today);

        prediction.MedianDaysAfterDue.Should().Be(1);
        prediction.PaysBeforeDue.Should().BeFalse();
        prediction.Explanation.Should().NotContain("even their slowest");
    }

    [Fact]
    public void A_client_who_pays_on_the_due_date_is_described_that_way_but_not_called_early()
    {
        // Exactly on time, every time — and still not "early", because the window is never
        // narrower than a couple of days and part of it therefore lands after the due date.
        // Saying "they always pay before it is due" of a to-the-day payer would be flattery,
        // not information.
        var history = PaidRepeatedly(Acme, "VN", 6, daysAfterDue: 0);

        var due = Today.AddDays(40);
        var prediction = Predict(history, Unpaid(Acme, "VN", due, viewedOn: Today), Today);

        prediction.MedianDaysAfterDue.Should().Be(0);
        prediction.ExpectedPaymentDate.Should().Be(due);
        prediction.PaysBeforeDue.Should().BeFalse();
        prediction.Explanation.Should().Contain("typically on the due date");
    }

    // -- Reporting ----------------------------------------------------------------------

    [Fact]
    public void The_interval_from_issue_is_reported_alongside_the_interval_from_due()
    {
        // Net-30 terms, paid nine days late: the user may think in either frame, and the
        // issue-relative figure is the one that survives a change of payment terms.
        var history = PaidRepeatedly(Acme, "VN", 6, daysAfterDue: 9);

        var prediction = Predict(history, Unpaid(Acme, "VN", Today.AddDays(14)), Today);

        prediction.MedianDaysAfterDue.Should().Be(9);
        prediction.MedianDaysFromIssue.Should().Be(TermDays + 9);
    }

    [Fact]
    public void The_window_is_ordered_and_contains_the_expected_date()
    {
        var history = new[]
        {
            Paid(Acme, "VN", 3),
            Paid(Acme, "VN", 8),
            Paid(Acme, "VN", 10),
            Paid(Acme, "VN", 21),
            Paid(Acme, "VN", 40),
        };

        var prediction = Predict(history, Unpaid(Acme, "VN", Today.AddDays(14)), Today);

        prediction.EarliestLikelyDate.Should().BeOnOrBefore(prediction.ExpectedPaymentDate!.Value);
        prediction.LatestLikelyDate.Should().BeOnOrAfter(prediction.ExpectedPaymentDate!.Value);
    }
}
