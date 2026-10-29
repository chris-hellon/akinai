using Akinai.Shared.Dtos.CurrencyPaymentSettings;
using Akinai.Shared.Payments;
using FluentAssertions;

namespace Akinai.Tests;

/// <summary>
/// The render-time gate on an invoice's accepted currencies. The choice is made when the
/// invoice is written; this decides which of those choices still have an account behind
/// them on the day the document is drawn.
///
/// The two failure modes are opposite and both bad: hiding an account the client would
/// have used, or advertising a currency that lands nowhere.
/// </summary>
public class AlternatePaymentCurrencyTests
{
    private static CurrencyPaymentSettingsDto Account(string currency, string? accountNumber = "12345678")
        => new(Guid.NewGuid(), currency, "A Bank", "A Name", accountNumber, null, null, null, null);

    private static Dictionary<string, decimal> Totals(params (string Currency, decimal Amount)[] rows)
        => rows.ToDictionary(r => r.Currency, r => r.Amount, StringComparer.OrdinalIgnoreCase);

    [Fact]
    public void A_currency_with_both_a_total_and_an_account_is_offered()
    {
        var result = AlternatePaymentCurrencies.Select(
            "GBP",
            Totals(("GBP", 570m), ("VND", 19_605_118.40m)),
            [Account("GBP"), Account("VND")]);

        result.Should().ContainSingle();
        result[0].Currency.Should().Be("VND");
        result[0].Amount.Should().Be(19_605_118.40m);
    }

    [Fact]
    public void The_invoices_own_currency_is_never_an_alternate()
    {
        // It is rendered as the primary payment block. Listing it twice would read as
        // two different ways to pay and invite paying both.
        var result = AlternatePaymentCurrencies.Select(
            "GBP", Totals(("GBP", 570m)), [Account("GBP")]);

        result.Should().BeEmpty();
    }

    [Fact]
    public void An_accepted_currency_whose_account_has_gone_is_not_offered()
    {
        // The account was deleted after the invoice was issued. Printing "pay in USD"
        // with no USD details underneath sends the client hunting for an account number
        // that is no longer anywhere on the document.
        var result = AlternatePaymentCurrencies.Select(
            "GBP",
            Totals(("GBP", 570m), ("USD", 712m), ("VND", 19_605_118.40m)),
            [Account("GBP"), Account("VND")]);

        result.Select(r => r.Currency).Should().BeEquivalentTo(["VND"]);
    }

    [Fact]
    public void An_account_with_no_reachable_identifier_is_not_offered()
    {
        // A currency tab opened and abandoned leaves a row with a bank name and nothing
        // to pay into. Offering it sends the client looking for an account number that
        // is not on the invoice.
        var result = AlternatePaymentCurrencies.Select(
            "GBP",
            Totals(("GBP", 570m), ("EUR", 660m)),
            [Account("EUR", accountNumber: null)]);

        result.Should().BeEmpty();
    }

    [Fact]
    public void An_iban_only_account_is_reachable()
    {
        // SEPA needs no account number — the IBAN is the account, and it is what the
        // scan-to-pay QR encodes.
        var eur = Account("EUR", accountNumber: null) with { BankIban = "DE89370400440532013000" };

        var result = AlternatePaymentCurrencies.Select(
            "GBP", Totals(("GBP", 570m), ("EUR", 660m)), [eur]);

        result.Select(r => r.Currency).Should().BeEquivalentTo(["EUR"]);
    }

    [Theory]
    [InlineData("UpiVpa", "name@okbank")]
    [InlineData("PixKey", "a-pix-key")]
    [InlineData("PromptPayId", "0812345678")]
    public void A_local_rail_identifier_alone_is_reachable(string property, string value)
    {
        // These rails never have an IBAN or a sort code; the identifier IS the account.
        // Requiring a bank account number would hide exactly the markets this app exists
        // to serve.
        var inr = Account("INR", accountNumber: null);
        inr = property switch
        {
            "UpiVpa" => inr with { UpiVpa = value },
            "PixKey" => inr with { PixKey = value },
            _ => inr with { PromptPayId = value },
        };

        var result = AlternatePaymentCurrencies.Select(
            "GBP", Totals(("GBP", 570m), ("INR", 60_000m)), [inr]);

        result.Should().ContainSingle();
    }

    [Fact]
    public void Currency_matching_ignores_case()
    {
        var result = AlternatePaymentCurrencies.Select(
            "gbp",
            Totals(("GBP", 570m), ("vnd", 19_605_118.40m)),
            [Account("VND")]);

        result.Should().ContainSingle();
        result[0].Currency.Should().Be("VND");
    }

    [Fact]
    public void Options_come_back_in_a_stable_order()
    {
        // The view, the client's copy and the PDF are rendered separately. If the order
        // came from a dictionary they could disagree about which account is listed first.
        var result = AlternatePaymentCurrencies.Select(
            "GBP",
            Totals(("VND", 1m), ("EUR", 2m), ("USD", 3m), ("GBP", 4m)),
            [Account("VND"), Account("EUR"), Account("USD")]);

        result.Select(r => r.Currency).Should().ContainInOrder("EUR", "USD", "VND");
    }
}
