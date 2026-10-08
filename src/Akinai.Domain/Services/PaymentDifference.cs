using System.Globalization;
using Akinai.Domain.Exceptions;

namespace Akinai.Domain.Services;

/// <summary>
/// Works out <em>why</em> the money that arrived is not the money that was invoiced.
///
/// Every invoicing tool can subtract. The damage is done by what they do next: they
/// render one number — "short by 102.47" — and leave the user to guess. Three
/// completely unrelated things produce that number, and the correct response to each is
/// different:
///
/// <list type="bullet">
/// <item>The receipt was converted, so the figures are in different currencies and
/// nothing is missing at all. Chasing here makes the user look as though they cannot
/// read a bank statement.</item>
/// <item>A correspondent bank took a fixed sum in transit. Whether that is the user's
/// cost or the client's depends entirely on the SHA/OUR/BEN convention the payment was
/// sent under — and under OUR it is recoverable from the sender.</item>
/// <item>The payer's country obliged them to withhold tax and remit it to their own
/// revenue authority. The invoice is <em>paid in full</em>. The user is owed a
/// withholding certificate, which is worth real money against their own tax bill, and
/// chasing the client for the balance is both wrong and insulting.</item>
/// </list>
///
/// So this classifies before it subtracts, and says which of those happened. Getting it
/// wrong in the safe direction matters more than getting it right often: an unexplained
/// gap is reported as unexplained rather than attributed to a plausible-looking cause,
/// because a wrongly-attributed gap is a gap nobody ever looks at again.
///
/// Pure arithmetic and a lookup table. No network, no database, no rate feed: it has to
/// run while rendering a payment row, and it has to be testable against the handful of
/// real statements that motivated it.
/// </summary>
public static class PaymentDifference
{
    /// <summary>
    /// Why the received amount differs from the invoiced amount. Deliberately not a
    /// severity: <see cref="WithholdingTax"/> and <see cref="UnexplainedShortfall"/> can
    /// be the same number of currency units and could not be more different in meaning.
    /// </summary>
    public enum Cause
    {
        /// <summary>The right money arrived. Nothing to do.</summary>
        ExactMatch,

        /// <summary>
        /// The receipt is in another currency and no rate was supplied, so nothing could be
        /// verified: a conversion margin, a transit fee and a tax withheld at source are
        /// indistinguishable without a rate to compare against.
        ///
        /// Deliberately NOT <see cref="FxSpread"/>. This case reports no difference at all,
        /// and sharing a cause with a verified conversion would let an unverified receipt
        /// present itself as a clean one — and count as settled.
        /// </summary>
        RateUnknown,

        /// <summary>
        /// The difference is conversion, not shortfall — the receipt landed in another
        /// currency, or at a rate other than the one the invoice was booked against.
        /// </summary>
        FxSpread,

        /// <summary>
        /// A fixed sum was taken in transit by a correspondent or intermediary bank.
        /// The invoice was paid; the wire was shaved.
        /// </summary>
        BankFee,

        /// <summary>
        /// The payer deducted tax at source and owes the user a certificate for it. This
        /// is a <em>complete</em> payment, and the only outstanding item is paperwork.
        /// </summary>
        WithholdingTax,

        /// <summary>
        /// Both: tax withheld at source, and a fee taken in transit on what was left.
        /// Common on an Indian or Philippine SHA wire, and the case that defeats any
        /// classifier that assumes one cause at a time.
        /// </summary>
        WithholdingTaxAndBankFee,

        /// <summary>More arrived than was invoiced — a duplicate, an advance, or a rounded-up wire.</summary>
        Overpayment,

        /// <summary>
        /// Money is missing and none of the known mechanisms accounts for it. The only
        /// classification that should lead to chasing the client.
        /// </summary>
        UnexplainedShortfall,
    }

    /// <summary>
    /// SWIFT field 71A: who bears the charges on the wire. It decides whether a fee-shaped
    /// deduction is the user's own cost of being paid or a sum the sender agreed to cover
    /// and should be asked to make good.
    /// </summary>
    public enum ChargeConvention
    {
        /// <summary>Not recorded. Fee-shaped deductions are still identified, but nothing is said about who owes them.</summary>
        Unknown = 0,

        /// <summary>Shared: the sender pays their own bank, the beneficiary bears intermediary charges. The usual default, and the usual reason money goes missing.</summary>
        Sha,

        /// <summary>The sender pays all charges. A deduction in transit means the instruction was not honoured, and it is recoverable from them.</summary>
        Our,

        /// <summary>The beneficiary pays all charges, including the sender's own bank's. Deductions are expected and can be larger.</summary>
        Ben,
    }

    /// <summary>
    /// The verdict. Carries the arithmetic in both currencies because the user reconciles
    /// against two documents at once — their invoice, in the invoice currency, and their
    /// bank statement, in the currency that actually landed.
    /// </summary>
    /// <param name="Cause">What happened.</param>
    /// <param name="DifferenceInInvoiceCurrency">Signed, negative when less arrived than was invoiced. Measured at <paramref name="ExpectedRate"/> where one was supplied.</param>
    /// <param name="DifferenceInReceivedCurrency">The same difference as it appears on the bank statement. Signed the same way.</param>
    /// <param name="ImpliedRate">Units of received currency per one unit of invoice currency, as the receipt actually worked out. Null when the currencies are the same. Note this is an <em>all-in</em> rate: if a fee or a withholding was also taken it is baked in, which is exactly why it must not be fed back in as an expected rate.</param>
    /// <param name="ExpectedRate">Echoed back. Null means none was supplied — see <see cref="Classify"/> for what that costs.</param>
    /// <param name="FxGainOrLoss">In the invoice currency: what the rate alone did, after the identified deduction is added back. Null when the currencies match or no expected rate was supplied. This is the figure that belongs in the accounts as a realised FX gain or loss.</param>
    /// <param name="WithholdingPercentage">The matched rate as a fraction (0.10 for 10%), or null.</param>
    /// <param name="WithholdingAmountInInvoiceCurrency">What the payer should be remitting on the user's behalf, which is the amount the certificate needs to show.</param>
    /// <param name="BankFeeInReceivedCurrency">The fixed sum taken in transit, in the currency it was taken from.</param>
    /// <param name="UnexplainedAmountInInvoiceCurrency">What is genuinely missing after every explanation is applied — the only figure it is ever correct to chase. Zero for everything but <see cref="Cause.UnexplainedShortfall"/>.</param>
    /// <param name="RequiresWithholdingCertificate">The user should be asking for a certificate, not for money.</param>
    /// <param name="ShouldChaseClient">A human needs to contact the client about money. <see cref="Explanation"/> says what to ask — which is not always "where is the rest".</param>
    /// <param name="Explanation">One or two sentences, written to be shown to the user as-is.</param>
    public sealed record Result(
        Cause Cause,
        decimal DifferenceInInvoiceCurrency,
        string InvoiceCurrency,
        decimal DifferenceInReceivedCurrency,
        string ReceivedCurrency,
        decimal? ImpliedRate,
        decimal? ExpectedRate,
        decimal? FxGainOrLoss,
        decimal? WithholdingPercentage,
        decimal? WithholdingAmountInInvoiceCurrency,
        decimal? BankFeeInReceivedCurrency,
        decimal UnexplainedAmountInInvoiceCurrency,
        bool RequiresWithholdingCertificate,
        bool ShouldChaseClient,
        string Explanation)
    {
        /// <summary>True when the invoice is settled in substance, whatever the two numbers look like.</summary>
        /// <remarks>
        /// <see cref="Cause.RateUnknown"/> is excluded on purpose: nothing was verified, so
        /// claiming the invoice is settled would be asserting an outcome on no evidence.
        /// </remarks>
        public bool IsSettled => Cause
            is Cause.ExactMatch
            or Cause.FxSpread
            or Cause.BankFee
            or Cause.WithholdingTax
            or Cause.WithholdingTaxAndBankFee
            or Cause.Overpayment;
    }

    // ---------------------------------------------------------------------------------
    // Thresholds. Each one is a judgement, so each one is named and argued for; a bare
    // 0.02m buried in a condition is how a classifier like this rots.
    // ---------------------------------------------------------------------------------

    /// <summary>
    /// Same-currency slack, in major units. Two cents absorbs the payer's own rounding
    /// and nothing else. Anything wider starts swallowing real deductions on small
    /// invoices, and the whole point is not to swallow deductions.
    /// </summary>
    public const decimal ExactMatchTolerance = 0.02m;

    /// <summary>
    /// Cross-currency slack, as a fraction of the expected receipt: 2 basis points. A rate
    /// quoted to four decimal places cannot reproduce a conversion more precisely than
    /// this, so insisting on two cents across a conversion would report every clean
    /// payment as an FX discrepancy.
    /// </summary>
    public const decimal ExactMatchRelativeTolerance = 0.0002m;

    /// <summary>
    /// How far from the expected rate still counts as the bank's spread rather than a
    /// shortfall: 3%. Sits above a typical 1–2.5% retail conversion margin plus a day's
    /// drift between booking the invoice and the value date, and comfortably below the
    /// smallest rate in <see cref="IndicativeWithholdingRates"/> (10%) — the two bands
    /// must not be able to collide, or a 10% tax deduction gets written off as FX noise.
    /// </summary>
    public const decimal FxSpreadToleranceFraction = 0.03m;

    /// <summary>
    /// How close a deduction must be to a published withholding rate to be called
    /// withholding: half a percentage point. Wide enough for the payer computing the tax
    /// on a slightly different base, rounding to whole units, and the rate wobble; narrow
    /// enough that no two rates in the table are ever both candidates.
    /// </summary>
    public const decimal WithholdingMatchTolerance = 0.005m;

    /// <summary>
    /// The plausible range for a correspondent bank's cut, in major units of the received
    /// currency. Quoted fees cluster at USD 15–40; the band is widened at both ends for
    /// EUR/GBP/CHF equivalents and for the two-hop wires that get shaved twice.
    ///
    /// Scale assumption, and the known limitation of this check: it reads as units of a
    /// major currency. A receipt in JPY, IDR or similar — quoted in thousands per USD —
    /// will not match, and the difference is reported as
    /// <see cref="Cause.UnexplainedShortfall"/> rather than guessed at. That is the
    /// correct failure direction, but it is a gap.
    /// </summary>
    public const decimal MinimumIntermediaryFee = 8m;

    /// <inheritdoc cref="MinimumIntermediaryFee"/>
    public const decimal MaximumIntermediaryFee = 60m;

    /// <summary>
    /// Under BEN the beneficiary also absorbs the sender's own bank's charge, so the
    /// plausible total is roughly double. Kept separate from
    /// <see cref="MaximumIntermediaryFee"/> so that a BEN-sized deduction on a SHA wire
    /// is still treated as suspicious.
    /// </summary>
    public const decimal MaximumBeneficiaryBorneCharge = 120m;

    /// <summary>
    /// A fee is a fixed sum, so on a small invoice it is indistinguishable from a real
    /// shortfall. Above a tenth of the invoice, refuse to call it a fee: "the bank took
    /// 40 of your 200" is a claim worth being sure about.
    /// </summary>
    public const decimal MaximumFeeFractionOfInvoice = 0.10m;

    /// <summary>
    /// <strong>Indicative</strong> withholding rates on cross-border payments for
    /// services, as a fraction of the gross, where the payer is resident in the keyed
    /// country and the recipient is not.
    ///
    /// These are the statutory default rates, which is to say the worst case. A double
    /// tax treaty very commonly reduces them — often to zero where the service is
    /// performed entirely outside the payer's country, and often to a lower band for
    /// "technical services" or "professional fees" specifically. Several of them also move
    /// with surtaxes, documentation, and whether a tax residency certificate was lodged
    /// before payment.
    ///
    /// They exist here to let the app <em>recognise the shape</em> of a deduction and say
    /// "this looks like withholding, not a short payment" — a question of pattern, not of
    /// law. They are a starting point for the user to confirm with their accountant and
    /// against the certificate when it arrives. They are never an authority, must never be
    /// presented as the rate that applies, and must never be used to compute a figure that
    /// goes on a tax return.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, decimal> IndicativeWithholdingRates =
        new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            ["IN"] = 0.10m,   // India — fees for technical/professional services
            ["PH"] = 0.10m,   // Philippines
            ["MY"] = 0.10m,   // Malaysia — s.109B technical fees
            ["CN"] = 0.10m,   // China — enterprise income tax on service fees
            ["BR"] = 0.15m,   // Brazil
            ["TH"] = 0.15m,   // Thailand — service fees
            ["PK"] = 0.15m,   // Pakistan
            ["ID"] = 0.20m,   // Indonesia — art.26
            ["JP"] = 0.20m,   // Japan — 20.42% with the reconstruction surtax
            ["KR"] = 0.20m,   // South Korea
            ["TW"] = 0.20m,   // Taiwan
            ["TR"] = 0.20m,   // Türkiye
            ["CO"] = 0.20m,   // Colombia
            ["MX"] = 0.25m,   // Mexico — technical assistance and professional services
            ["AR"] = 0.315m,  // Argentina — 35% on a presumed 90% net base
        };

    /// <summary>
    /// The indicative rate for a payer country, or null when there is no entry — which
    /// means "we have no default to offer", never "no tax is withheld there".
    /// </summary>
    public static decimal? IndicativeWithholdingRate(string? payerCountryCode)
        => payerCountryCode is not null && IndicativeWithholdingRates.TryGetValue(payerCountryCode.Trim(), out var rate)
            ? rate
            : null;

    /// <summary>
    /// Classifies a received amount against an invoiced one.
    /// </summary>
    /// <param name="invoicedAmount">The invoice total. Must be positive — a zero invoice has nothing to reconcile against and would make every ratio meaningless.</param>
    /// <param name="invoiceCurrency">ISO-4217 code the invoice was raised in.</param>
    /// <param name="receivedAmount">What actually landed, before the user's own bank's charges but as the statement shows it.</param>
    /// <param name="receivedCurrency">ISO-4217 code of the receipt. Equal to <paramref name="invoiceCurrency"/> for a same-currency payment.</param>
    /// <param name="expectedRate">
    /// Units of received currency per one unit of invoice currency, as booked when the
    /// invoice was raised or as quoted on the value date.
    ///
    /// Supplying this is what makes a cross-currency receipt checkable at all. Without
    /// it, a conversion and a 10% tax deduction are the same event seen from outside:
    /// there is no way to tell how much of the gap is the rate. In that case nothing is
    /// claimed — the implied rate is reported for the user to check, and the difference is
    /// recorded as zero rather than invented.
    /// </param>
    /// <param name="payerCountryCode">
    /// ISO-3166 alpha-2 of where the <em>payer</em> is resident, which is what decides
    /// withholding — not where the user is, and not where the bank is. Null when unknown,
    /// in which case a withholding-shaped deduction is pointed out but not asserted.
    /// </param>
    /// <param name="convention">Who was meant to bear the wire charges.</param>
    public static Result Classify(
        decimal invoicedAmount,
        string invoiceCurrency,
        decimal receivedAmount,
        string receivedCurrency,
        decimal? expectedRate = null,
        string? payerCountryCode = null,
        ChargeConvention convention = ChargeConvention.Unknown)
    {
        if (invoicedAmount <= 0)
            throw new DomainException("Cannot classify a payment against a zero or negative invoice total");
        if (receivedAmount < 0)
            throw new DomainException("A received amount cannot be negative");
        if (string.IsNullOrWhiteSpace(invoiceCurrency) || string.IsNullOrWhiteSpace(receivedCurrency))
            throw new DomainException("Both the invoice currency and the received currency are required");
        if (expectedRate is <= 0)
            throw new DomainException("An expected exchange rate must be positive");

        var from = invoiceCurrency.Trim().ToUpperInvariant();
        var to = receivedCurrency.Trim().ToUpperInvariant();
        var sameCurrency = from == to;

        var impliedRate = sameCurrency ? (decimal?)null : Round6(receivedAmount / invoicedAmount);

        // The rate everything is measured against. Same currency: one, trivially.
        // Cross-currency without an expected rate: the implied rate, which makes the
        // difference identically zero — the honest answer, because at the rate it
        // actually converted at, nothing is missing and nothing can be proved.
        var rate = sameCurrency ? 1m : expectedRate ?? receivedAmount / invoicedAmount;

        if (!sameCurrency && expectedRate is null)
        {
            return new Result(
                Cause.RateUnknown,
                DifferenceInInvoiceCurrency: 0m,
                from,
                DifferenceInReceivedCurrency: 0m,
                to,
                impliedRate,
                ExpectedRate: null,
                FxGainOrLoss: null,
                WithholdingPercentage: null,
                WithholdingAmountInInvoiceCurrency: null,
                BankFeeInReceivedCurrency: null,
                UnexplainedAmountInInvoiceCurrency: 0m,
                RequiresWithholdingCertificate: false,
                ShouldChaseClient: false,
                Explanation:
                    $"{Format(from, invoicedAmount)} was invoiced and {Format(to, receivedAmount)} arrived, which works out "
                    + $"at {FormatRate(from, to, impliedRate!.Value)}. No expected rate was recorded for this invoice, so the "
                    + "receipt cannot be checked — a conversion, a bank fee and a tax deduction all look the same from here. "
                    + "Compare that rate against the rate on the value date, and if it is wide, record the expected rate so "
                    + "this can be classified.");
        }

        var expectedReceipt = invoicedAmount * rate;

        // Positive gap = money missing, measured on the bank statement. Everything below
        // works in this one direction so the sign never has to be re-derived.
        var gap = expectedReceipt - receivedAmount;

        var exactTolerance = sameCurrency
            ? ExactMatchTolerance
            : Math.Max(ExactMatchTolerance, expectedReceipt * ExactMatchRelativeTolerance);

        if (Math.Abs(gap) <= exactTolerance)
        {
            return Build(Cause.ExactMatch, gap, 0m, null, null,
                sameCurrency
                    ? $"{Format(to, receivedAmount)} received against {Format(from, invoicedAmount)} invoiced. Paid in full."
                    : $"{Format(to, receivedAmount)} received against {Format(from, invoicedAmount)} invoiced, at the expected "
                      + $"{FormatRate(from, to, rate)}. Paid in full — the two figures differ only because the currencies do.");
        }

        var fxBand = expectedReceipt * FxSpreadToleranceFraction;

        // ---- More arrived than expected -------------------------------------------
        if (gap < 0)
        {
            var surplus = -gap;

            if (!sameCurrency && surplus <= fxBand)
            {
                return Build(Cause.FxSpread, gap, 0m, null, null,
                    $"{Format(to, receivedAmount)} received against {Format(from, invoicedAmount)} invoiced — "
                    + $"{Format(to, surplus)} more than the expected rate of {FormatRate(from, to, rate)} implied, because it "
                    + $"converted at {FormatRate(from, to, impliedRate!.Value)}. The rate moved in your favour; the invoice is "
                    + "paid. Book the difference as a realised FX gain rather than as a credit to the client.");
            }

            return Build(Cause.Overpayment, gap, gap, null, null,
                sameCurrency
                    ? $"{Format(to, receivedAmount)} received against {Format(from, invoicedAmount)} invoiced — "
                      + $"{Format(to, surplus)} more than was asked for. Check for a duplicate payment or an advance against "
                      + "the next invoice before treating it as income."
                    : $"{Format(to, receivedAmount)} received against {Format(from, invoicedAmount)} invoiced — "
                      + $"{Format(to, surplus)} more than the expected {FormatRate(from, to, rate)} implied, which is too wide "
                      + "to be the conversion. Check for a duplicate or an advance, and confirm the rate on the value date.");
        }

        // ---- Withholding, checked before anything else ------------------------------
        // Deliberately first: the deduction is large and percentage-shaped, so it is the
        // cause most likely to be mistaken for a shortfall, and the one where being wrong
        // costs the user both a certificate and the client's goodwill.
        var declaredRate = IndicativeWithholdingRate(payerCountryCode);
        var observedFraction = gap / expectedReceipt;

        if (declaredRate is { } withholding)
        {
            var withholdingInReceived = invoicedAmount * withholding * rate;
            var residual = gap - withholdingInReceived;
            var country = payerCountryCode!.Trim().ToUpperInvariant();

            // The tax is remitted in the payer's own currency, so when the invoice is in
            // another one this figure is a conversion of it, not the sum on the certificate.
            // Saying so is the difference between a useful number and a wrong one.
            var withheldAmount = sameCurrency
                ? Format(from, invoicedAmount * withholding)
                : $"the equivalent of {Format(from, invoicedAmount * withholding)}";

            if (Math.Abs(observedFraction - withholding) <= WithholdingMatchTolerance)
            {
                return Build(Cause.WithholdingTax, gap, 0m, withholding, null,
                    $"{Format(to, receivedAmount)} received against {Format(from, invoicedAmount)} invoiced. The "
                    + $"{Percent(observedFraction)} deducted matches the indicative {Percent(withholding)} withholding rate on "
                    + $"cross-border service payments from {country}: this invoice is paid in full, and the payer is remitting "
                    + $"{withheldAmount} to their own tax authority on your behalf. Ask them for the "
                    + "withholding certificate — it is creditable against your own tax — and do not chase the balance. The rate "
                    + "is an indicative default; a treaty may reduce it, so confirm it against the certificate.",
                    requiresCertificate: true);
            }

            if (residual > 0 && IsFeeShaped(residual, expectedReceipt, convention))
            {
                return Build(Cause.WithholdingTaxAndBankFee, gap, residual, withholding, residual,
                    $"{Format(to, receivedAmount)} received against {Format(from, invoicedAmount)} invoiced, from two separate "
                    + $"deductions. The payer withheld the indicative {Percent(withholding)} for {country} "
                    + $"({withheldAmount}) — ask for the certificate — and a further "
                    + $"{Format(to, residual)} was taken in transit, which is a correspondent bank fee. {FeeLiability(convention)} "
                    + "Nothing here is unpaid invoice.",
                    requiresCertificate: true,
                    chase: convention == ChargeConvention.Our);
            }
        }

        // ---- The rate, where it can account for the whole gap -----------------------
        // Ahead of the fee check on purpose, and it is a real choice: on a smaller invoice
        // a 1–3% conversion margin and a fixed transit fee are the same number of units,
        // and only one of them can be reported. The rate wins because the two wrong answers
        // are not equally wrong — calling a spread "a bank fee" invents a third party and,
        // under OUR, a claim against the client, whereas calling a fee "a spread" still
        // tells the user the invoice is paid and this route costs them money.
        if (!sameCurrency && gap <= fxBand)
        {
            return Build(Cause.FxSpread, gap, 0m, null, null,
                $"{Format(to, receivedAmount)} received against {Format(from, invoicedAmount)} invoiced. It converted at "
                + $"{FormatRate(from, to, impliedRate!.Value)} rather than the expected {FormatRate(from, to, rate)}, which "
                + $"accounts for the whole {Format(to, gap)} — a {Percent(observedFraction)} spread, in the normal range for a "
                + "bank's inbound conversion. The invoice is paid; book the difference as a realised FX loss.");
        }

        // ---- A fixed sum taken in transit -------------------------------------------
        if (IsFeeShaped(gap, expectedReceipt, convention))
        {
            return Build(Cause.BankFee, gap, gap, null, gap,
                $"{Format(to, receivedAmount)} received against {Format(from, invoicedAmount)} invoiced. The missing "
                + $"{Format(to, gap)} is a fixed sum in the range correspondent banks take in transit, not a percentage, so the "
                + $"client almost certainly sent the full amount. {FeeLiability(convention)}",
                chase: convention == ChargeConvention.Our);
        }

        // ---- Nothing accounts for it ------------------------------------------------
        var lookalike = IndicativeWithholdingRates
            .Where(r => Math.Abs(observedFraction - r.Value) <= WithholdingMatchTolerance)
            .Select(r => r.Value)
            .FirstOrDefault();

        var shortfall = gap / rate;

        var explanation = lookalike != 0m && declaredRate is null
            // Worth saying, not worth asserting: the shape fits a tax deduction, but
            // without knowing where the payer is resident, claiming it would be a guess
            // dressed as a finding — and a wrong "paid in full" is never looked at again.
            ? $"{Format(to, receivedAmount)} received against {Format(from, invoicedAmount)} invoiced, leaving "
              + $"{Format(from, shortfall)} unaccounted for. That is {Percent(observedFraction)} of the invoice, which is the "
              + "shape of a tax withheld at source rather than a shortfall — but no payer country is recorded, so it cannot be "
              + "confirmed. Ask the client whether tax was withheld, and for the certificate if it was, before treating this as "
              + "unpaid."
            : $"{Format(to, receivedAmount)} received against {Format(from, invoicedAmount)} invoiced, leaving "
              + $"{Format(from, shortfall)} unaccounted for. It is neither a percentage matching a known withholding rate nor a "
              + $"fixed sum in the range banks take in transit{(sameCurrency ? "" : ", and the rate does not explain it")}. This "
              + "one is worth asking the client about.";

        // The whole gap is held back from the FX figure: the rate demonstrably cannot
        // produce a difference this wide, so attributing an unexplained shortfall to a
        // realised FX loss would quietly write it off in the accounts.
        return Build(Cause.UnexplainedShortfall, gap, gap, null, null, explanation,
            chase: true,
            unexplained: shortfall);

        // -----------------------------------------------------------------------------

        Result Build(
            Cause cause,
            decimal gapInReceived,
            decimal nonFxPortionInReceivedCurrency,
            decimal? withholdingFraction,
            decimal? bankFee,
            string explanation,
            bool requiresCertificate = false,
            bool chase = false,
            decimal unexplained = 0m)
        {
            // What the rate did, once every part of the difference this classification
            // can account for some other way is put back: for a clean withholding match it
            // lands near zero, which is the point — tax deducted at source is not an FX
            // loss and must not reach the accounts as one. A withholding deduction happens
            // in the invoice currency before conversion, so it is added back through the
            // rate; a transit fee is taken from the converted sum, so it is added back
            // as-is. The same arithmetic covers both.
            var grossBeforeDeduction = receivedAmount + nonFxPortionInReceivedCurrency;
            var withheld = withholdingFraction is { } f ? invoicedAmount * f : (decimal?)null;

            decimal? fxGainOrLoss = null;
            if (!sameCurrency && expectedRate is not null)
                fxGainOrLoss = Round2(grossBeforeDeduction / rate + (withheld ?? 0m) - invoicedAmount);

            return new Result(
                cause,
                Round2(-gapInReceived / rate),
                from,
                Round2(-gapInReceived),
                to,
                impliedRate,
                expectedRate,
                fxGainOrLoss,
                withholdingFraction,
                withheld is null ? null : Round2(withheld.Value),
                bankFee is null ? null : Round2(bankFee.Value),
                Round2(unexplained),
                requiresCertificate,
                chase,
                explanation);
        }
    }

    /// <summary>
    /// A deduction is fee-shaped when it is a plausible fixed sum <em>and</em> small
    /// relative to the invoice. Both halves matter: without the first, any shortfall under
    /// 60 is a "bank fee"; without the second, a bank is credited with taking a third of a
    /// small invoice.
    /// </summary>
    private static bool IsFeeShaped(decimal amount, decimal expectedReceipt, ChargeConvention convention)
    {
        var ceiling = convention == ChargeConvention.Ben
            ? MaximumBeneficiaryBorneCharge
            : MaximumIntermediaryFee;

        return amount >= MinimumIntermediaryFee
            && amount <= ceiling
            && amount <= expectedReceipt * MaximumFeeFractionOfInvoice;
    }

    /// <summary>
    /// Who is out of pocket, which is the only part of a transit fee the user can act on.
    /// Under OUR the sender undertook to cover every charge, so a deduction means the
    /// instruction was not honoured and the sum is recoverable from them — the one case
    /// where a fee is worth an email.
    /// </summary>
    private static string FeeLiability(ChargeConvention convention) => convention switch
    {
        ChargeConvention.Our =>
            "The payment was sent OUR, so the sender undertook to cover all charges — this deduction should not have reached "
            + "you, and is recoverable from them. Worth raising, with the statement line as evidence.",
        ChargeConvention.Sha =>
            "The payment was sent SHA, so intermediary charges are yours to bear by agreement. Nothing to recover, but it is a "
            + "real cost of this payment route — worth pricing in, or moving the client to a local rail.",
        ChargeConvention.Ben =>
            "The payment was sent BEN, so all charges are yours by agreement, including the sender's own bank's.",
        _ =>
            "The charge convention was not recorded for this payment, so whether it is recoverable depends on what was agreed — "
            + "under OUR it is, under SHA or BEN it is not.",
    };

    private static decimal Round2(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static decimal Round6(decimal value) => Math.Round(value, 6, MidpointRounding.AwayFromZero);

    private static string Format(string currency, decimal amount)
        => $"{currency} {amount.ToString("N2", CultureInfo.InvariantCulture)}";

    private static string FormatRate(string from, string to, decimal rate)
        => $"1 {from} = {Round6(rate).ToString("0.######", CultureInfo.InvariantCulture)} {to}";

    private static string Percent(decimal fraction)
        => $"{Math.Round(fraction * 100m, 2, MidpointRounding.AwayFromZero).ToString("0.##", CultureInfo.InvariantCulture)}%";
}
