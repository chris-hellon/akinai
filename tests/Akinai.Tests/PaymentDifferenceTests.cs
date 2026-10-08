using Akinai.Domain.Exceptions;
using Akinai.Domain.Services;
using FluentAssertions;

namespace Akinai.Tests;

/// <summary>
/// The rule that stops the app telling a user they have been short-paid when they have not.
///
/// These are not arithmetic tests. Subtracting two numbers is not the hard part; deciding
/// what the result <em>means</em> is, and getting it wrong has a cost in both directions —
/// chase a client who paid in full and withheld tax correctly, or quietly write off money
/// that really is missing. So nearly every test here pins a <em>classification</em> and
/// what the user is told to do about it, and several pin two inputs that differ only in
/// context to show that the context, not the number, decides.
/// </summary>
public class PaymentDifferenceTests
{
    private const PaymentDifference.ChargeConvention Sha = PaymentDifference.ChargeConvention.Sha;
    private const PaymentDifference.ChargeConvention Our = PaymentDifference.ChargeConvention.Our;
    private const PaymentDifference.ChargeConvention Ben = PaymentDifference.ChargeConvention.Ben;

    // ----------------------------------------------------------------------------------
    // Nothing is wrong
    // ----------------------------------------------------------------------------------

    [Fact]
    public void The_invoiced_amount_arriving_in_the_invoiced_currency_is_an_exact_match()
    {
        var result = PaymentDifference.Classify(1_000m, "GBP", 1_000m, "GBP");

        result.Cause.Should().Be(PaymentDifference.Cause.ExactMatch);
        result.DifferenceInInvoiceCurrency.Should().Be(0m);
        result.DifferenceInReceivedCurrency.Should().Be(0m);
        result.ImpliedRate.Should().BeNull("there was no conversion to imply a rate from");
        result.FxGainOrLoss.Should().BeNull();
        result.UnexplainedAmountInInvoiceCurrency.Should().Be(0m);
        result.ShouldChaseClient.Should().BeFalse();
        result.IsSettled.Should().BeTrue();
    }

    [Fact]
    public void A_single_penny_either_way_is_still_an_exact_match()
    {
        // Payers round. If a penny opened a "discrepancy" the feature would be noise.
        PaymentDifference.Classify(842.50m, "GBP", 842.49m, "GBP")
            .Cause.Should().Be(PaymentDifference.Cause.ExactMatch);

        PaymentDifference.Classify(842.50m, "GBP", 842.51m, "GBP")
            .Cause.Should().Be(PaymentDifference.Cause.ExactMatch);
    }

    [Fact]
    public void Currency_codes_are_compared_without_regard_to_case_or_padding()
    {
        PaymentDifference.Classify(500m, "eur", 500m, " EUR ")
            .Cause.Should().Be(PaymentDifference.Cause.ExactMatch);
    }

    [Fact]
    public void A_conversion_at_exactly_the_expected_rate_is_an_exact_match_not_a_discrepancy()
    {
        // EUR 1,000 at 0.8450 is GBP 845.00. The two numbers differ; nothing is missing.
        var result = PaymentDifference.Classify(1_000m, "EUR", 845m, "GBP", expectedRate: 0.8450m);

        result.Cause.Should().Be(PaymentDifference.Cause.ExactMatch);
        result.ImpliedRate.Should().Be(0.845m);
        result.FxGainOrLoss.Should().Be(0m);
        result.Explanation.Should().Contain("Paid in full");
    }

    // ----------------------------------------------------------------------------------
    // Cause 1: the money was converted
    // ----------------------------------------------------------------------------------

    [Fact]
    public void A_bank_conversion_margin_is_an_fx_spread_and_not_a_shortfall()
    {
        // EUR 1,000 expected at 0.8450 = GBP 845.00; GBP 832.10 landed, a 1.5% spread.
        var result = PaymentDifference.Classify(
            1_000m, "EUR", 832.10m, "GBP", expectedRate: 0.8450m, convention: Sha);

        result.Cause.Should().Be(PaymentDifference.Cause.FxSpread);
        result.IsSettled.Should().BeTrue();
        result.ShouldChaseClient.Should().BeFalse();
        result.UnexplainedAmountInInvoiceCurrency.Should().Be(0m);

        result.DifferenceInReceivedCurrency.Should().Be(-12.90m);
        result.DifferenceInInvoiceCurrency.Should().Be(-15.27m);   // 12.90 / 0.8450
        result.FxGainOrLoss.Should().Be(-15.27m, "the whole difference is the rate, and it belongs in the accounts as a realised FX loss");
        result.ImpliedRate.Should().Be(0.8321m);
    }

    [Fact]
    public void A_rate_that_moved_in_the_users_favour_is_an_fx_gain_not_an_overpayment()
    {
        // EUR 1,000 expected at 1.0800 = USD 1,080.00; USD 1,095.00 landed.
        var result = PaymentDifference.Classify(1_000m, "EUR", 1_095m, "USD", expectedRate: 1.08m);

        result.Cause.Should().Be(PaymentDifference.Cause.FxSpread);
        result.DifferenceInReceivedCurrency.Should().Be(15m);
        result.FxGainOrLoss.Should().Be(13.89m);
        result.Explanation.Should().Contain("realised FX gain");
    }

    [Fact]
    public void Without_an_expected_rate_a_cross_currency_receipt_is_reported_as_uncheckable_rather_than_guessed_at()
    {
        // The honest answer, and the reason the expected rate is worth capturing: a
        // conversion, a transit fee and a 10% tax deduction are indistinguishable from
        // outside unless the rate is known. Inventing a difference here would be worse
        // than admitting there is nothing to compare against.
        var result = PaymentDifference.Classify(1_000m, "EUR", 982.47m, "GBP");

        result.Cause.Should().Be(PaymentDifference.Cause.RateUnknown);
        result.IsSettled.Should().BeFalse("nothing was verified, so nothing may be asserted");
        result.ExpectedRate.Should().BeNull();
        result.ImpliedRate.Should().Be(0.98247m);
        result.DifferenceInInvoiceCurrency.Should().Be(0m);
        result.DifferenceInReceivedCurrency.Should().Be(0m);
        result.FxGainOrLoss.Should().BeNull("nothing can be attributed to the rate when the rate is unknown");
        result.ShouldChaseClient.Should().BeFalse();
        result.Explanation.Should().Contain("No expected rate was recorded");
    }

    // ----------------------------------------------------------------------------------
    // Cause 2: a correspondent bank took a fixed sum in transit
    // ----------------------------------------------------------------------------------

    [Fact]
    public void A_fixed_sum_missing_from_a_same_currency_wire_is_a_correspondent_bank_fee()
    {
        var result = PaymentDifference.Classify(4_800m, "USD", 4_775m, "USD", convention: Sha);

        result.Cause.Should().Be(PaymentDifference.Cause.BankFee);
        result.BankFeeInReceivedCurrency.Should().Be(25m);
        result.IsSettled.Should().BeTrue();
        result.ShouldChaseClient.Should().BeFalse("under SHA the beneficiary bears intermediary charges by agreement");
        result.UnexplainedAmountInInvoiceCurrency.Should().Be(0m);
        result.Explanation.Should().Contain("SHA");
    }

    [Fact]
    public void The_same_fee_on_an_OUR_payment_is_recoverable_from_the_sender()
    {
        // Same two numbers as the SHA case, opposite advice. Under OUR the sender
        // undertook to cover every charge, so the deduction should never have reached the
        // beneficiary — this is the one fee worth an email.
        var result = PaymentDifference.Classify(4_800m, "USD", 4_775m, "USD", convention: Our);

        result.Cause.Should().Be(PaymentDifference.Cause.BankFee);
        result.ShouldChaseClient.Should().BeTrue();
        result.Explanation.Should().Contain("recoverable from them");
    }

    [Fact]
    public void A_transit_fee_is_still_a_fee_when_the_rate_cannot_account_for_it()
    {
        // GBP 300 at 1.2700 = USD 381.00 expected; USD 356.00 landed. The USD 25 gap is
        // 6.6% — far outside any conversion margin, and exactly fee-shaped.
        var result = PaymentDifference.Classify(300m, "GBP", 356m, "USD", expectedRate: 1.27m, convention: Sha);

        result.Cause.Should().Be(PaymentDifference.Cause.BankFee);
        result.BankFeeInReceivedCurrency.Should().Be(25m);
        result.FxGainOrLoss.Should().Be(0m, "a fee is not an FX loss and must not be booked as one");
    }

    [Fact]
    public void A_charge_too_large_for_SHA_is_within_range_for_BEN()
    {
        // USD 90 off a USD 3,000 wire. Under BEN the beneficiary also absorbs the sender's
        // own bank's charge, so the plausible total is roughly double — under SHA the same
        // deduction is too big to wave through.
        PaymentDifference.Classify(3_000m, "USD", 2_910m, "USD", convention: Ben)
            .Cause.Should().Be(PaymentDifference.Cause.BankFee);

        PaymentDifference.Classify(3_000m, "USD", 2_910m, "USD", convention: Sha)
            .Cause.Should().Be(PaymentDifference.Cause.UnexplainedShortfall);
    }

    [Fact]
    public void A_fee_sized_gap_on_a_tiny_invoice_is_not_written_off_as_a_fee()
    {
        // USD 40 is a plausible fee in absolute terms, but it is a fifth of a USD 200
        // invoice. "The bank took 40 of your 200" is too strong a claim to make on shape
        // alone, so the gap stays open.
        var result = PaymentDifference.Classify(200m, "USD", 160m, "USD", convention: Sha);

        result.Cause.Should().Be(PaymentDifference.Cause.UnexplainedShortfall);
        result.UnexplainedAmountInInvoiceCurrency.Should().Be(40m);
        result.ShouldChaseClient.Should().BeTrue();
    }

    // ----------------------------------------------------------------------------------
    // Cause 3: the payer withheld tax at source
    // ----------------------------------------------------------------------------------

    [Fact]
    public void A_ten_percent_shortfall_from_India_is_withholding_not_a_short_payment()
    {
        var result = PaymentDifference.Classify(
            12_000m, "USD", 10_800m, "USD", payerCountryCode: "IN", convention: Sha);

        result.Cause.Should().Be(PaymentDifference.Cause.WithholdingTax);
        result.WithholdingPercentage.Should().Be(0.10m);
        result.WithholdingAmountInInvoiceCurrency.Should().Be(1_200m);

        result.IsSettled.Should().BeTrue("tax deducted at source is a complete payment");
        result.RequiresWithholdingCertificate.Should().BeTrue();
        result.ShouldChaseClient.Should().BeFalse("the client owes paperwork, not money");
        result.UnexplainedAmountInInvoiceCurrency.Should().Be(0m);

        result.Explanation.Should().Contain("withholding certificate");
        result.Explanation.Should().Contain("indicative");
    }

    [Theory]
    [InlineData("IN", 0.10)]
    [InlineData("PH", 0.10)]
    [InlineData("MY", 0.10)]
    [InlineData("TH", 0.15)]
    [InlineData("BR", 0.15)]
    [InlineData("ID", 0.20)]
    [InlineData("JP", 0.20)]
    [InlineData("MX", 0.25)]
    [InlineData("AR", 0.315)]
    public void Each_corridors_statutory_rate_is_recognised_as_withholding(string country, double rate)
    {
        var withheld = (decimal)rate;
        var invoiced = 8_000m;

        var result = PaymentDifference.Classify(
            invoiced, "USD", invoiced * (1 - withheld), "USD", payerCountryCode: country);

        result.Cause.Should().Be(PaymentDifference.Cause.WithholdingTax);
        result.WithholdingPercentage.Should().Be(withheld);
        result.WithholdingAmountInInvoiceCurrency.Should().Be(invoiced * withheld);
        result.RequiresWithholdingCertificate.Should().BeTrue();
        result.ShouldChaseClient.Should().BeFalse();
    }

    [Fact]
    public void A_country_code_is_matched_regardless_of_case()
    {
        PaymentDifference.Classify(1_000m, "USD", 900m, "USD", payerCountryCode: "in")
            .Cause.Should().Be(PaymentDifference.Cause.WithholdingTax);
    }

    [Fact]
    public void Withholding_survives_the_payer_rounding_the_tax_to_whole_units()
    {
        // Indonesia at 20% of USD 7,350 is 1,470.00; the payer remitted 1,475 and sent the
        // rest. Half a percentage point of slack exists for exactly this.
        var result = PaymentDifference.Classify(
            7_350m, "USD", 5_875m, "USD", payerCountryCode: "ID");

        result.Cause.Should().Be(PaymentDifference.Cause.WithholdingTax);
        result.WithholdingPercentage.Should().Be(0.20m);
    }

    [Fact]
    public void Withholding_on_a_converted_receipt_is_not_reported_as_an_fx_loss()
    {
        // EUR 10,000 invoiced to a Japanese payer, expected at 162.50; 20% withheld, so
        // JPY 1,300,000 of an expected JPY 1,625,000 lands. The rate did nothing — if the
        // JPY 325,000 reached the accounts as a realised FX loss the user would both lose
        // the tax credit and misstate the year.
        var result = PaymentDifference.Classify(
            10_000m, "EUR", 1_300_000m, "JPY", expectedRate: 162.50m, payerCountryCode: "JP");

        result.Cause.Should().Be(PaymentDifference.Cause.WithholdingTax);
        result.WithholdingPercentage.Should().Be(0.20m);
        result.WithholdingAmountInInvoiceCurrency.Should().Be(2_000m);
        result.FxGainOrLoss.Should().Be(0m);
        result.DifferenceInInvoiceCurrency.Should().Be(-2_000m);
        result.DifferenceInReceivedCurrency.Should().Be(-325_000m);
    }

    [Fact]
    public void Tax_withheld_and_a_fee_taken_in_transit_are_reported_as_both()
    {
        // The case that defeats a one-cause classifier: India withheld 10% of USD 1,200,
        // and a correspondent took USD 25 from the USD 1,080 that was actually sent.
        var result = PaymentDifference.Classify(
            1_200m, "USD", 1_055m, "USD", payerCountryCode: "IN", convention: Sha);

        result.Cause.Should().Be(PaymentDifference.Cause.WithholdingTaxAndBankFee);
        result.WithholdingPercentage.Should().Be(0.10m);
        result.WithholdingAmountInInvoiceCurrency.Should().Be(120m);
        result.BankFeeInReceivedCurrency.Should().Be(25m);

        result.IsSettled.Should().BeTrue();
        result.RequiresWithholdingCertificate.Should().BeTrue();
        result.ShouldChaseClient.Should().BeFalse();
        result.UnexplainedAmountInInvoiceCurrency.Should().Be(0m);
    }

    [Fact]
    public void A_withheld_payment_whose_transit_fee_was_sent_OUR_is_worth_querying()
    {
        var result = PaymentDifference.Classify(
            1_200m, "USD", 1_055m, "USD", payerCountryCode: "IN", convention: Our);

        result.Cause.Should().Be(PaymentDifference.Cause.WithholdingTaxAndBankFee);
        result.RequiresWithholdingCertificate.Should().BeTrue();
        result.ShouldChaseClient.Should().BeTrue("the USD 25, not the tax, is recoverable under OUR");
    }

    [Fact]
    public void The_table_is_only_consulted_for_the_payers_own_country()
    {
        // 20% off a payment from Germany is not Indonesian withholding. Withholding is
        // decided by where the payer is resident, so a rate must never be borrowed from a
        // country that is not in the transaction.
        var result = PaymentDifference.Classify(
            5_000m, "EUR", 4_000m, "EUR", payerCountryCode: "DE");

        result.Cause.Should().Be(PaymentDifference.Cause.UnexplainedShortfall);
        result.WithholdingPercentage.Should().BeNull();
    }

    // ----------------------------------------------------------------------------------
    // More arrived than was invoiced
    // ----------------------------------------------------------------------------------

    [Fact]
    public void More_money_than_was_invoiced_is_an_overpayment_to_look_into()
    {
        var result = PaymentDifference.Classify(1_000m, "USD", 1_050m, "USD");

        result.Cause.Should().Be(PaymentDifference.Cause.Overpayment);
        result.DifferenceInInvoiceCurrency.Should().Be(50m);
        result.DifferenceInReceivedCurrency.Should().Be(50m);
        result.IsSettled.Should().BeTrue();
        result.Explanation.Should().Contain("duplicate");
    }

    [Fact]
    public void A_surplus_too_large_to_be_the_rate_is_an_overpayment_not_an_fx_gain()
    {
        // EUR 1,000 expected at 1.0800 = USD 1,080; USD 1,200 landed. An 11% move is not
        // a conversion margin, and attributing it to one would bury a duplicate payment.
        var result = PaymentDifference.Classify(1_000m, "EUR", 1_200m, "USD", expectedRate: 1.08m);

        result.Cause.Should().Be(PaymentDifference.Cause.Overpayment);
        result.FxGainOrLoss.Should().Be(0m);
        result.DifferenceInReceivedCurrency.Should().Be(120m);
    }

    // ----------------------------------------------------------------------------------
    // The ambiguous cases — where the context, not the number, decides
    // ----------------------------------------------------------------------------------

    [Fact]
    public void The_same_ten_percent_gap_is_withholding_or_unexplained_depending_only_on_the_payers_country()
    {
        var fromIndia = PaymentDifference.Classify(
            3_000m, "USD", 2_700m, "USD", payerCountryCode: "IN");

        var fromNowhereRecorded = PaymentDifference.Classify(
            3_000m, "USD", 2_700m, "USD");

        fromIndia.Cause.Should().Be(PaymentDifference.Cause.WithholdingTax);
        fromIndia.ShouldChaseClient.Should().BeFalse();

        fromNowhereRecorded.Cause.Should().Be(PaymentDifference.Cause.UnexplainedShortfall);
        fromNowhereRecorded.UnexplainedAmountInInvoiceCurrency.Should().Be(300m);
        fromNowhereRecorded.WithholdingPercentage.Should().BeNull("a guess must not be dressed up as a finding");
        fromNowhereRecorded.RequiresWithholdingCertificate.Should().BeFalse();

        // Not asserted, but not hidden either: the first question to ask is whether tax
        // was withheld, which is cheaper than an argument about an unpaid invoice.
        fromNowhereRecorded.ShouldChaseClient.Should().BeTrue();
        fromNowhereRecorded.Explanation.Should().Contain("shape of a tax withheld at source");
        fromNowhereRecorded.Explanation.Should().Contain("no payer country is recorded");
    }

    [Fact]
    public void A_withholding_shaped_gap_from_a_country_with_no_entry_is_flagged_but_not_assumed()
    {
        // The United States does not withhold on a service invoice from a treaty-resident
        // contractor, and a missing entry never means "no tax is withheld there" anyway.
        var result = PaymentDifference.Classify(
            3_000m, "USD", 2_700m, "USD", payerCountryCode: "US");

        result.Cause.Should().Be(PaymentDifference.Cause.UnexplainedShortfall);
        result.Explanation.Should().Contain("shape of a tax withheld at source");
    }

    [Fact]
    public void A_gap_that_is_neither_a_known_rate_nor_a_plausible_fee_stays_open()
    {
        var result = PaymentDifference.Classify(
            9_400m, "GBP", 8_150m, "GBP", payerCountryCode: "IN", convention: Sha);

        result.Cause.Should().Be(PaymentDifference.Cause.UnexplainedShortfall);
        result.IsSettled.Should().BeFalse();
        result.ShouldChaseClient.Should().BeTrue();
        result.UnexplainedAmountInInvoiceCurrency.Should().Be(1_250m);
        result.WithholdingPercentage.Should().BeNull();
        result.BankFeeInReceivedCurrency.Should().BeNull();
    }

    [Fact]
    public void A_payment_that_never_arrived_is_not_explained_away()
    {
        var result = PaymentDifference.Classify(2_500m, "USD", 0m, "USD", payerCountryCode: "IN");

        result.Cause.Should().Be(PaymentDifference.Cause.UnexplainedShortfall);
        result.UnexplainedAmountInInvoiceCurrency.Should().Be(2_500m);
    }

    // ----------------------------------------------------------------------------------
    // Invariants the thresholds depend on
    // ----------------------------------------------------------------------------------

    [Fact]
    public void Every_indicative_withholding_rate_sits_clear_of_the_fx_spread_band()
    {
        // The two bands must not be able to overlap. If a withholding rate ever drops into
        // the FX band, a real tax deduction starts being written off as conversion noise —
        // silently, and in the direction that loses the user a tax credit. Any new row in
        // the table below 3% has to come with a rethink of the ordering, so this fails
        // loudly rather than letting one in.
        PaymentDifference.IndicativeWithholdingRates.Should().NotBeEmpty();

        PaymentDifference.IndicativeWithholdingRates.Values.Should().OnlyContain(
            rate => rate > PaymentDifference.FxSpreadToleranceFraction + PaymentDifference.WithholdingMatchTolerance);
    }

    [Fact]
    public void No_two_withholding_rates_are_close_enough_to_both_match_one_deduction()
    {
        var rates = PaymentDifference.IndicativeWithholdingRates.Values.Distinct().Order().ToList();

        foreach (var (lower, upper) in rates.Zip(rates.Skip(1)))
        {
            (upper - lower).Should().BeGreaterThan(
                PaymentDifference.WithholdingMatchTolerance * 2,
                "a deduction must never be a candidate for two different rates at once");
        }
    }

    [Fact]
    public void An_unknown_country_has_no_indicative_rate_offered_for_it()
    {
        PaymentDifference.IndicativeWithholdingRate("DE").Should().BeNull();
        PaymentDifference.IndicativeWithholdingRate(null).Should().BeNull();
        PaymentDifference.IndicativeWithholdingRate("IN").Should().Be(0.10m);
    }

    // ----------------------------------------------------------------------------------
    // Inputs that cannot be reconciled at all
    // ----------------------------------------------------------------------------------

    [Fact]
    public void A_zero_invoice_cannot_be_reconciled_against()
    {
        // Every ratio in the classifier divides by the invoiced amount. Failing here is
        // better than returning a confident classification built on a division by zero.
        var act = () => PaymentDifference.Classify(0m, "USD", 100m, "USD");

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void A_negative_receipt_or_a_non_positive_rate_is_rejected()
    {
        var negativeReceipt = () => PaymentDifference.Classify(100m, "USD", -1m, "USD");
        negativeReceipt.Should().Throw<DomainException>();

        var zeroRate = () => PaymentDifference.Classify(100m, "USD", 90m, "GBP", expectedRate: 0m);
        zeroRate.Should().Throw<DomainException>();
    }

    [Fact]
    public void A_missing_currency_code_is_rejected()
    {
        var act = () => PaymentDifference.Classify(100m, "USD", 100m, "   ");

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void A_cross_currency_receipt_with_a_rate_is_settled_as_an_FX_spread()
    {
        // The same receipt, now checkable, is a conversion and is settled.
        var result = PaymentDifference.Classify(
            invoicedAmount: 1000m, invoiceCurrency: "EUR",
            receivedAmount: 845m, receivedCurrency: "GBP",
            expectedRate: 0.85m);

        result.Cause.Should().Be(PaymentDifference.Cause.FxSpread);
        result.IsSettled.Should().BeTrue();
    }
}
