using System.Globalization;
using Akinai.Domain.Exceptions;

namespace Akinai.Domain.Services;

/// <summary>
/// Predicts when an invoice will actually be paid, from the user's own history of
/// invoices that were actually paid.
///
/// Every invoicing tool shows a due date. A due date is a request, not a forecast: the
/// useful question is not "when did I ask to be paid" but "when does <em>this client</em>
/// usually pay". For a freelancer billing across borders the answer varies enormously by
/// who and by where — a Vietnamese client settling 34 days after the due date and a Dutch
/// one settling 2 days before it are both behaving completely normally, and no published
/// benchmark can tell the user which of those their next invoice resembles. Their own
/// ledger can. That is the entire premise: the prediction is derived from data nobody else
/// holds, so nobody else can offer it.
///
/// Three rules shape everything below, and each of them exists because the obvious
/// implementation is wrong:
///
/// <list type="bullet">
/// <item><strong>Specific beats general.</strong> This client's own history first, then the
/// same country, then everything. The basis is reported alongside the number, because
/// "based on 6 invoices from this client" and "based on 3 invoices from anywhere" are not
/// the same claim and must not render as the same confident date.</item>
/// <item><strong>Median, never mean.</strong> One client who disappeared for 300 days and
/// then paid is the single most common shape in a freelancer's ledger, and it drags a mean
/// so far that the estimate becomes useless precisely for the users with the most
/// history. The median ignores it, which is the correct response: it happened once, it is
/// not the pattern, and the pattern is what was asked for.</item>
/// <item><strong>Silence over a confident-looking guess.</strong> Below
/// <see cref="MinimumSampleSize"/> observations this returns
/// <see cref="Basis.None"/> and says so in words. A date extrapolated from one past
/// invoice looks exactly like a date drawn from forty, and a user who plans rent around
/// the first one has been actively misled. Refusing to answer is a feature.</item>
/// </list>
///
/// Pure arithmetic over a collection the caller supplies. No EF, no clock, no network —
/// <paramref name="asOf"/> is passed in so that "is this already late" is reproducible,
/// and so this can be exercised against a real ledger in a test rather than only in
/// production.
/// </summary>
public static class PaymentTiming
{
    /// <summary>
    /// Which slice of history the prediction was drawn from. Reported rather than hidden,
    /// because it is the main thing that should decide how prominently the UI shows the
    /// date — and the thing a user will immediately want to argue with.
    /// </summary>
    public enum Basis
    {
        /// <summary>
        /// No slice had enough history. There is no date in the result, deliberately.
        /// </summary>
        None = 0,

        /// <summary>
        /// Everything paid, across every client and country. Weak — it mixes a fast
        /// domestic client with a slow overseas one — but it is still the user's own data,
        /// and for a first invoice to a new client in a new country it is all there is.
        /// </summary>
        Global = 1,

        /// <summary>
        /// Other clients in the same country. Captures the parts of payment speed that are
        /// genuinely regional: banking rails, payment-run conventions, approval culture,
        /// public holidays.
        /// </summary>
        Country = 2,

        /// <summary>
        /// This client's own paid invoices. The only basis that captures the one thing that
        /// predicts payment best — the habits of the specific person who signs it off.
        /// </summary>
        Client = 3,
    }

    /// <summary>
    /// How much weight the date deserves. Ordered, so it can be narrowed by taking the
    /// minimum — several independent reasons to distrust an estimate all apply at once.
    /// </summary>
    public enum Confidence
    {
        /// <summary>No prediction was made.</summary>
        None = 0,

        /// <summary>
        /// Worth showing as a hint, not worth planning around. Phrase it as a guess in the
        /// UI, because that is what it is.
        /// </summary>
        Low = 1,

        /// <summary>A real pattern, from a real sample, with real spread. The common case.</summary>
        Moderate = 2,

        /// <summary>
        /// Enough invoices from this client, paid consistently enough, that the date can be
        /// put in front of the user as an expectation. Reserved for
        /// <see cref="Basis.Client"/> — no amount of country or global history earns it,
        /// because the quantity being averaged over is not the thing being predicted.
        /// </summary>
        High = 3,
    }

    /// <summary>
    /// One past invoice that was actually paid: the raw material. Deliberately not the
    /// <c>Invoice</c> entity — this service must be callable from a projection, a cached
    /// summary or a test fixture, and must never tempt a caller into loading entities to
    /// get a forecast.
    /// </summary>
    /// <param name="ClientId">Who paid it. The key for the strongest basis.</param>
    /// <param name="ClientCountryCode">ISO-3166 alpha-2 of the client. Null or blank means the observation can still contribute to <see cref="Basis.Global"/> but can never match a country.</param>
    /// <param name="IssueDate">When it was raised.</param>
    /// <param name="DueDate">When payment was asked for. Carried separately from the issue date because payment terms differ per client and per invoice, and the interval that generalises is measured from here.</param>
    /// <param name="PaidDate">When the money actually arrived. The only date in this record that is an observation rather than an intention.</param>
    /// <param name="Amount">
    /// The invoice total. Used for one narrow purpose only — see
    /// <see cref="LargeInvoiceMultiple"/> — and never to adjust the predicted date.
    ///
    /// Assumed to be in a single currency across the collection the caller passes. A
    /// history mixing JPY and GBP totals makes the magnitude comparison meaningless, but
    /// its only effect is to lower confidence, so a mixed-currency ledger degrades this
    /// into excessive caution rather than into a wrong date.
    /// </param>
    public readonly record struct Observation(
        Guid ClientId,
        string? ClientCountryCode,
        DateOnly IssueDate,
        DateOnly DueDate,
        DateOnly PaidDate,
        decimal Amount);

    /// <summary>
    /// The invoice the question is about. Unpaid, by definition — there is nothing to
    /// predict about a settled one.
    /// </summary>
    /// <param name="ClientId">Used to find this client's own history.</param>
    /// <param name="ClientCountryCode">Used for the country fallback. Null or blank skips that tier rather than matching other unknowns to each other.</param>
    /// <param name="IssueDate">When it was raised.</param>
    /// <param name="DueDate">The date every interval below is measured against.</param>
    /// <param name="Amount">The total, compared against the history's typical size. See <see cref="LargeInvoiceMultiple"/>.</param>
    /// <param name="FirstViewedOn">
    /// When the client first opened it, if they have. Null means never opened.
    ///
    /// This is used, but only for what it can honestly support — see
    /// <see cref="UnviewedConcernWindowDays"/>. It does not move the date.
    /// </param>
    public readonly record struct Query(
        Guid ClientId,
        string? ClientCountryCode,
        DateOnly IssueDate,
        DateOnly DueDate,
        decimal Amount,
        DateOnly? FirstViewedOn = null);

    /// <summary>
    /// The forecast. Carries the structured dates <em>and</em> a sentence, because the two
    /// are needed in different places: a dashboard sorts and charts on the dates, and a row
    /// on the invoice list needs one line of plain English that is already correct about
    /// how much it knows.
    /// </summary>
    /// <param name="Basis">Which slice of history this came from. <see cref="Basis.None"/> means no prediction — every date below is null.</param>
    /// <param name="Confidence">How much weight it deserves.</param>
    /// <param name="SampleSize">How many paid invoices the basis slice contained. Shown to the user, because it is the single most informative number here.</param>
    /// <param name="ExpectedPaymentDate">The single best guess. Never in the past: for an unpaid invoice a predicted arrival date of last Tuesday is not a prediction, so it is clamped forward to <c>asOf</c> and <see cref="IsAlreadyLaterThanUsual"/> is raised instead.</param>
    /// <param name="EarliestLikelyDate">The optimistic end of the window, clamped forward the same way.</param>
    /// <param name="LatestLikelyDate">The pessimistic end — the figure worth planning cash around.</param>
    /// <param name="MedianDaysAfterDue">Signed: negative means this basis typically pays <em>before</em> the due date. The headline statistic.</param>
    /// <param name="MedianDaysFromIssue">Context for the above, and the more useful figure when payment terms have changed.</param>
    /// <param name="PaysBeforeDue">Even the slow end of this basis's range lands on or before the due date. A fact worth surfacing: these are the clients not to spend chasing.</param>
    /// <param name="IsAlreadyLaterThanUsual">This invoice has already outlasted its own prediction. The pattern has failed for this one, so the date is no longer informative — the flag is.</param>
    /// <param name="DaysLaterThanUsual">By how much. Zero unless <see cref="IsAlreadyLaterThanUsual"/>.</param>
    /// <param name="Explanation">One to three sentences, written to be shown to the user as-is.</param>
    public sealed record Prediction(
        Basis Basis,
        Confidence Confidence,
        int SampleSize,
        DateOnly? ExpectedPaymentDate,
        DateOnly? EarliestLikelyDate,
        DateOnly? LatestLikelyDate,
        int? MedianDaysAfterDue,
        int? MedianDaysFromIssue,
        bool PaysBeforeDue,
        bool IsAlreadyLaterThanUsual,
        int DaysLaterThanUsual,
        string Explanation)
    {
        /// <summary>True when there is a date to show at all.</summary>
        public bool HasPrediction => Basis != Basis.None;
    }

    // ---------------------------------------------------------------------------------
    // Thresholds. Every one of these is a judgement call rather than a fact, so every one
    // is named and argued for. A bare `>= 6` in a condition here would be indistinguishable
    // from a typo in six months' time.
    // ---------------------------------------------------------------------------------

    /// <summary>
    /// Fewest observations that will produce a prediction at all: three.
    ///
    /// Three is the smallest sample that has a middle. With two, the median is the average
    /// of both — a mean wearing a median's name, where one 300-day outlier moves the answer
    /// by 150 days. With one, there is no dispersion to inspect and no way to tell a
    /// habitual payer from a fluke. Three is still thin, which is why it yields
    /// <see cref="Confidence.Low"/> rather than a confident date, but it is the point at
    /// which a single aberration can no longer <em>be</em> the answer.
    /// </summary>
    public const int MinimumSampleSize = 3;

    /// <summary>
    /// Where <see cref="Confidence.Moderate"/> starts: four.
    ///
    /// At four, the quartile window has two observations on each side of the middle, so the
    /// reported range is measured rather than assumed, and no single observation can be
    /// both the median and an endpoint.
    /// </summary>
    public const int ModerateConfidenceSampleSize = 4;

    /// <summary>
    /// Where <see cref="Confidence.High"/> becomes available: six.
    ///
    /// Six invoices from one client is roughly half a year of monthly billing. That matters
    /// more than the count itself: it is long enough to have survived a quarter-end and a
    /// holiday period, the two things that make three consecutive fast payments look like a
    /// pattern when they are really just a quiet season. Fewer than six and a consistent
    /// spread is as likely to mean "not enough time has passed" as "this client is
    /// reliable".
    /// </summary>
    public const int HighConfidenceSampleSize = 6;

    /// <summary>
    /// How tight the middle of the sample has to be before the behaviour counts as
    /// consistent: a seven-day interquartile spread.
    ///
    /// A week, specifically, because clients pay out of periodic payment runs — commonly
    /// weekly or fortnightly — so the same habit lands on different days depending on where
    /// the due date fell relative to the run. Treating that as inconsistency would deny
    /// confidence to the most predictable clients there are. Beyond a week, the variation is
    /// no longer run alignment.
    /// </summary>
    public const int ConsistentSpreadDays = 7;

    /// <summary>
    /// The window the result is never narrower than: the median, plus or minus two days.
    ///
    /// A sample where every invoice was paid on exactly day 9 produces a zero-width range,
    /// which renders as certainty. No sample of the sizes involved here supports certainty,
    /// and a range is read as a planning window — so the floor is a statement about how
    /// precisely this can be known, not about the data.
    /// </summary>
    public const int MinimumHalfWindowDays = 2;

    /// <summary>
    /// How much bigger than the basis's typical invoice this one has to be before
    /// confidence is stepped down: four times.
    ///
    /// Not a claim that large invoices are paid more slowly — the direction of that is not
    /// reliable, since bigger invoices attract longer approval chains but also harder
    /// chasing. The claim is narrower and defensible: an invoice several times larger than
    /// anything in the sample has probably crossed an authorisation threshold the sample
    /// never tested, so it is being predicted by history that does not cover it. Unknown
    /// direction, unknown size — so this lowers confidence and never moves the date.
    /// </summary>
    public const decimal LargeInvoiceMultiple = 4m;

    /// <summary>
    /// How close to the due date an unopened invoice stops being a non-event: seven days.
    ///
    /// This is the only use made of the view signal, and it is mechanical rather than
    /// statistical. Payment requires the client to have the invoice in hand; an unopened
    /// invoice with under a week to go has already missed the weekly payment run that would
    /// have settled it on time, whatever this client's habits are. So the history is no
    /// longer describing the same situation.
    ///
    /// Note what is deliberately <em>not</em> done: there is no learned "viewed invoices get
    /// paid N days sooner" adjustment. <see cref="Observation"/> carries no view dates, so
    /// there is nothing in this service's input from which such a relationship could be
    /// measured — and asserting one from intuition would be inventing a correlation and
    /// dressing it as the user's own data, which is the one thing this service exists not to
    /// do. If view timestamps are ever added to the observations, that becomes a real
    /// question worth answering with real numbers.
    /// </summary>
    public const int UnviewedConcernWindowDays = 7;

    /// <summary>
    /// Predicts when <paramref name="invoice"/> will be paid.
    /// </summary>
    /// <param name="history">
    /// Paid invoices to learn from — the user's whole ledger is fine, filtering happens
    /// here. Observations that cannot be true (paid before issued, due before issued) are
    /// dropped rather than thrown on: this runs while rendering an invoice list, and one
    /// bad historical row must not take the page down.
    /// </param>
    /// <param name="invoice">The unpaid invoice being asked about.</param>
    /// <param name="asOf">
    /// Today, from the caller's clock. Passed in rather than read here so that "already
    /// later than usual" is deterministic, and so a forecast can be replayed as of any date.
    /// </param>
    public static Prediction Predict(IEnumerable<Observation> history, Query invoice, DateOnly asOf)
    {
        ArgumentNullException.ThrowIfNull(history);

        if (invoice.DueDate < invoice.IssueDate)
            throw new DomainException("An invoice cannot be due before it was issued");

        // Impossible rows are dropped, not repaired: a negative payment interval is a data
        // fault, and guessing what it was meant to be would put fiction into the median.
        var usable = history
            .Where(o => o.PaidDate >= o.IssueDate && o.DueDate >= o.IssueDate)
            .ToList();

        var country = Normalise(invoice.ClientCountryCode);

        // Most specific slice that clears the minimum wins. Note each tier is a superset of
        // the one above it, so a client with two paid invoices still contributes to its own
        // country's sample — nothing is discarded by falling back, only widened.
        var (basis, sample) = SelectBasis(usable, invoice.ClientId, country);

        if (basis == Basis.None)
            return NotEnoughHistory(usable.Count);

        var daysAfterDue = sample.Select(o => o.PaidDate.DayNumber - o.DueDate.DayNumber).Order().ToList();
        var daysFromIssue = sample.Select(o => o.PaidDate.DayNumber - o.IssueDate.DayNumber).Order().ToList();

        var medianAfterDue = Median(daysAfterDue);
        var medianFromIssue = Median(daysFromIssue);

        // The quartiles, not the extremes. The whole point of resisting the 300-day outlier
        // in the middle is lost if it is then allowed to define the edge of the window.
        var lower = Math.Min(Percentile(daysAfterDue, 0.25), medianAfterDue - MinimumHalfWindowDays);
        var upper = Math.Max(Percentile(daysAfterDue, 0.75), medianAfterDue + MinimumHalfWindowDays);

        var expectedRaw = invoice.DueDate.AddDays(medianAfterDue);
        var earliestRaw = invoice.DueDate.AddDays(lower);
        var latestRaw = invoice.DueDate.AddDays(upper);

        var alreadyLate = expectedRaw < asOf;
        var daysLate = alreadyLate ? asOf.DayNumber - expectedRaw.DayNumber : 0;

        var expected = Later(expectedRaw, asOf);
        var earliest = Later(earliestRaw, asOf);
        var latest = Later(latestRaw, expected);

        // "Pays early" is deliberately the strong form: not "the median is negative" but
        // "even the slow end of the range is not late". A client whose middle is day -1 and
        // whose upper quartile is day +12 is not an early payer, and telling the user they
        // are is how a chase gets skipped.
        var paysBeforeDue = upper <= 0;

        var confidence = BaseConfidence(basis, sample.Count, upper - lower);

        var unviewedAndNearlyDue = invoice.FirstViewedOn is null
            && asOf >= invoice.DueDate.AddDays(-UnviewedConcernWindowDays);

        if (unviewedAndNearlyDue)
            confidence = StepDown(confidence);

        if (IsUnusuallyLarge(invoice.Amount, sample))
            confidence = StepDown(confidence);

        // An invoice that has outlasted its own prediction has already demonstrated that the
        // pattern does not hold for it, whatever the sample says.
        if (alreadyLate)
            confidence = Narrow(confidence, Confidence.Low);

        return new Prediction(
            basis,
            confidence,
            sample.Count,
            expected,
            earliest,
            latest,
            medianAfterDue,
            medianFromIssue,
            paysBeforeDue,
            alreadyLate,
            daysLate,
            Explain(
                basis,
                sample.Count,
                medianAfterDue,
                medianFromIssue,
                expected,
                earliest,
                latest,
                paysBeforeDue,
                alreadyLate,
                daysLate,
                unviewedAndNearlyDue,
                IsUnusuallyLarge(invoice.Amount, sample)));
    }

    /// <summary>
    /// Client, then country, then everything — stopping at the first slice with enough in
    /// it. Specificity is preferred over sample size on purpose: three invoices from the
    /// person who will actually pay this one beats forty from strangers, because the
    /// quantity being predicted is that person's behaviour.
    /// </summary>
    private static (Basis Basis, List<Observation> Sample) SelectBasis(
        List<Observation> usable,
        Guid clientId,
        string? country)
    {
        var ofClient = usable.Where(o => o.ClientId == clientId).ToList();
        if (ofClient.Count >= MinimumSampleSize)
            return (Basis.Client, ofClient);

        // A blank country is skipped rather than matched: "unknown" is not a place, and
        // pooling every client whose country was never recorded would invent a cohort.
        if (country is not null)
        {
            var ofCountry = usable.Where(o => Normalise(o.ClientCountryCode) == country).ToList();
            if (ofCountry.Count >= MinimumSampleSize)
                return (Basis.Country, ofCountry);
        }

        return usable.Count >= MinimumSampleSize
            ? (Basis.Global, usable)
            : (Basis.None, []);
    }

    /// <summary>
    /// Sample size sets the ceiling, consistency decides whether it is reached. Country and
    /// global bases are capped below <see cref="Confidence.High"/> however much history they
    /// have: aggregating more strangers does not make a stranger's behaviour a better model
    /// of this client's.
    /// </summary>
    private static Confidence BaseConfidence(Basis basis, int sampleSize, int spreadDays)
    {
        var consistent = spreadDays <= ConsistentSpreadDays;

        var earned = (sampleSize, consistent) switch
        {
            ( >= HighConfidenceSampleSize, true) => Confidence.High,
            ( >= ModerateConfidenceSampleSize, _) => Confidence.Moderate,
            _ => Confidence.Low,
        };

        var ceiling = basis switch
        {
            Basis.Client => Confidence.High,
            Basis.Country => Confidence.Moderate,
            _ => Confidence.Low,
        };

        return Narrow(earned, ceiling);
    }

    /// <summary>
    /// Whether this invoice is far enough outside the size range the sample covers that the
    /// sample is arguably not evidence about it. Compared against the sample's own median
    /// amount, for the same outlier reason the dates use one.
    /// </summary>
    private static bool IsUnusuallyLarge(decimal amount, List<Observation> sample)
    {
        if (amount <= 0)
            return false;

        var amounts = sample.Select(o => o.Amount).Where(a => a > 0).Order().ToList();
        if (amounts.Count == 0)
            return false;

        var typical = amounts.Count % 2 == 1
            ? amounts[amounts.Count / 2]
            : (amounts[amounts.Count / 2 - 1] + amounts[amounts.Count / 2]) / 2m;

        return typical > 0 && amount >= typical * LargeInvoiceMultiple;
    }

    /// <summary>
    /// The honest answer when there is nothing to go on. Phrased to tell the user what would
    /// make it work rather than just failing, because the fix is in their hands: raise and
    /// settle a few more invoices.
    /// </summary>
    private static Prediction NotEnoughHistory(int paidInvoices) => new(
        Basis.None,
        Confidence.None,
        paidInvoices,
        ExpectedPaymentDate: null,
        EarliestLikelyDate: null,
        LatestLikelyDate: null,
        MedianDaysAfterDue: null,
        MedianDaysFromIssue: null,
        PaysBeforeDue: false,
        IsAlreadyLaterThanUsual: false,
        DaysLaterThanUsual: 0,
        Explanation: paidInvoices switch
        {
            0 => "No invoices have been paid yet, so there is no history to predict from. "
                 + $"Once {MinimumSampleSize} have been settled, payment dates start being predicted from your own records.",
            _ => $"Only {Plural(paidInvoices, "paid invoice")} to go on, which is not a pattern — one or two payments "
                 + $"cannot be told apart from luck. At {MinimumSampleSize} this starts predicting, and it gets sharper "
                 + "as the same client pays you again.",
        });

    /// <summary>
    /// The sentence the user reads. Assembled rather than templated because the interesting
    /// cases are combinations: an early-paying client whose latest invoice has gone quiet,
    /// or a global-basis guess about an unopened invoice, need to read as one coherent
    /// thought rather than three stacked disclaimers.
    /// </summary>
    private static string Explain(
        Basis basis,
        int sampleSize,
        int medianAfterDue,
        int medianFromIssue,
        DateOnly expected,
        DateOnly earliest,
        DateOnly latest,
        bool paysBeforeDue,
        bool alreadyLate,
        int daysLate,
        bool unviewedAndNearlyDue,
        bool unusuallyLarge)
    {
        var timing = medianAfterDue switch
        {
            0 => "on the due date",
            > 0 => $"{Plural(medianAfterDue, "day")} after the due date",
            _ => $"{Plural(-medianAfterDue, "day")} before the due date",
        };

        var opening = basis switch
        {
            Basis.Client when paysBeforeDue =>
                $"This client has paid {Plural(sampleSize, "invoice")}, typically {timing} — and even their slowest "
                + "still arrived before it was due.",
            Basis.Client =>
                $"This client has paid {Plural(sampleSize, "invoice")}, typically {timing} "
                + $"({Plural(medianFromIssue, "day")} from issue).",
            Basis.Country =>
                $"No settled invoices from this client yet, but {Plural(sampleSize, "invoice")} from the same country "
                + $"were paid a median of {timing}.",
            _ =>
                $"Nothing from this client or country yet. Across all {Plural(sampleSize, "invoice")} you have been "
                + $"paid for, the median was {timing} — a rough guide at best.",
        };

        var window = alreadyLate
            ? $" That date has passed: it is already {Plural(daysLate, "day")} later than this history predicted, so "
              + "the pattern no longer accounts for it. This one is worth chasing."
            : $" Expect payment around {Format(expected)}, most likely between {Format(earliest)} and {Format(latest)}.";

        var caveats = string.Empty;

        if (unviewedAndNearlyDue)
            caveats += " The client has not opened this invoice yet, and payment needs them to have it in hand — "
                       + "treat the dates above as the earliest plausible rather than the likely ones, and consider "
                       + "resending it.";

        if (unusuallyLarge)
            caveats += $" It is also more than {LargeInvoiceMultiple:0.#}x the size of a typical invoice in that history, "
                       + "which may well put it through an approval step the others never went near.";

        return opening + window + caveats;
    }

    /// <summary>
    /// The middle value, which is the whole statistical argument of this service.
    ///
    /// A freelancer's ledger reliably contains one client who vanished for most of a year
    /// and then paid. In a mean that single row shifts the answer by tens of days and goes
    /// on doing so for every future prediction, so the users with the most history get the
    /// worst estimates. The median does not move: the outlier is one rank among many, which
    /// is exactly what it is in reality — something that happened once, not the pattern that
    /// was asked about.
    ///
    /// On an even sample the two middles are averaged and rounded towards <em>later</em>.
    /// The two errors are not equal: predicting money a day early invites spending it before
    /// it arrives, predicting it a day late costs nothing.
    /// </summary>
    private static int Median(IReadOnlyList<int> sortedAscending)
    {
        var count = sortedAscending.Count;
        var middle = count / 2;

        return count % 2 == 1
            ? sortedAscending[middle]
            : (int)Math.Ceiling((sortedAscending[middle - 1] + sortedAscending[middle]) / 2.0);
    }

    /// <summary>
    /// Nearest-rank percentile: returns an observation that actually happened rather than an
    /// interpolation between two that did. On the sample sizes here — three to a few dozen —
    /// interpolating implies a precision the data has not got, and an invented endpoint is
    /// harder to explain to a user looking at their own invoice list.
    /// </summary>
    private static int Percentile(IReadOnlyList<int> sortedAscending, double fraction)
    {
        var rank = (int)Math.Ceiling(fraction * sortedAscending.Count) - 1;
        return sortedAscending[Math.Clamp(rank, 0, sortedAscending.Count - 1)];
    }

    /// <summary>Takes the more cautious of two confidence levels.</summary>
    private static Confidence Narrow(Confidence a, Confidence b)
        => (Confidence)Math.Min((int)a, (int)b);

    /// <summary>
    /// One band down, floored at <see cref="Confidence.Low"/>. Used for caveats that weaken
    /// an estimate without invalidating it — several can apply, and each should cost
    /// something without any one of them collapsing the result to nothing.
    /// </summary>
    private static Confidence StepDown(Confidence confidence) => confidence switch
    {
        Confidence.High => Confidence.Moderate,
        Confidence.Moderate => Confidence.Low,
        _ => confidence,
    };

    private static DateOnly Later(DateOnly a, DateOnly b) => a > b ? a : b;

    private static string? Normalise(string? countryCode)
        => string.IsNullOrWhiteSpace(countryCode) ? null : countryCode.Trim().ToUpperInvariant();

    /// <summary>
    /// Dates are rendered long and unambiguously — "9 October 2026", never "09/10/2026",
    /// which means two different days depending on the reader. Invariant on purpose: the
    /// explanation is a convenience for the common case, and a caller that needs a localised
    /// date should build its own sentence from the structured fields.
    /// </summary>
    private static string Format(DateOnly date)
        => date.ToString("d MMMM yyyy", CultureInfo.InvariantCulture);

    private static string Plural(int count, string noun)
        => $"{count} {noun}{(count == 1 ? "" : "s")}";
}
