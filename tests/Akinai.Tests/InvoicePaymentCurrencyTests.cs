using Akinai.Domain.Entities;
using Akinai.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Akinai.Tests;

/// <summary>
/// Which currencies an invoice records as accepted, and at what amount.
///
/// The amount is the whole risk here. It is a figure a client may pay against weeks
/// later, so once they have the invoice it must not move — the same rule that freezes a
/// time entry's billing rate the moment it is invoiced.
/// </summary>
public class InvoicePaymentCurrencyTests
{
    /// <summary>A rate that can be moved between saves, to prove what is frozen.</summary>
    private sealed class MovableRates : IExchangeRateService
    {
        public decimal Rate { get; set; } = 30_000m;
        public bool Available { get; set; } = true;

        public Task<IReadOnlyDictionary<string, decimal>> GetUsdRatesAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyDictionary<string, decimal>>(new Dictionary<string, decimal> { ["USD"] = 1m });

        public Task<decimal?> ConvertAsync(decimal amount, string from, string to, CancellationToken ct = default)
            => Task.FromResult(Available ? amount * Rate : (decimal?)null);
    }

    private static async Task AddAccountAsync(InvoiceTestContext ctx, string currency, string? accountNumber = "123456")
    {
        await using var db = ctx.NewContext();
        var settings = CurrencyPaymentSettings.Create(InvoiceTestContext.UserId, currency);
        settings.Update(
            bankName: "Timo", bankAccountName: "A Name", bankAccountNumber: accountNumber,
            bankSortCode: null, bankIban: null, bankSwift: null, vietQrBankCode: null);
        db.CurrencyPaymentSettings.Add(settings);
        await db.SaveChangesAsync();
    }

    private static async Task SetAsync(
        InvoiceTestContext ctx, IExchangeRateService rates, params string[] currencies)
    {
        await using var db = ctx.NewContext();
        await new InvoicePaymentCurrencyService(rates)
            .SetAsync(db, ctx.InvoiceId, InvoiceTestContext.UserId, currencies);
        await db.SaveChangesAsync();
    }

    private static async Task<Dictionary<string, decimal>> StoredAsync(InvoiceTestContext ctx)
    {
        await using var db = ctx.NewContext();
        return await db.InvoicePaymentCurrencies
            .Where(pc => pc.InvoiceId == ctx.InvoiceId)
            .ToDictionaryAsync(pc => pc.Currency, pc => pc.Amount);
    }

    private static async Task IssueAsync(InvoiceTestContext ctx)
    {
        await using var db = ctx.NewContext();
        var invoice = await db.Invoices.FirstAsync(i => i.Id == ctx.InvoiceId);
        invoice.Send();
        await db.SaveChangesAsync();
    }

    private static async Task<InvoiceTestContext> SeededAsync()
    {
        var ctx = new InvoiceTestContext();
        // £100 of work, no tax, so the converted figures are easy to read.
        await ctx.SeedAsync(clientRate: 100m, taxRate: 0m, entryDays: (1, 1m));
        await AddAccountAsync(ctx, "VND");
        return ctx;
    }

    [Fact]
    public async Task A_chosen_currency_is_converted_and_stored()
    {
        using var ctx = await SeededAsync();

        await SetAsync(ctx, new MovableRates { Rate = 30_000m }, "VND");

        var stored = await StoredAsync(ctx);
        stored.Should().ContainKey("VND");
        stored["VND"].Should().Be(3_000_000m);
    }

    [Fact]
    public async Task The_invoices_own_currency_is_never_stored()
    {
        using var ctx = await SeededAsync();
        await AddAccountAsync(ctx, "GBP");

        await SetAsync(ctx, new MovableRates(), "GBP", "VND");

        (await StoredAsync(ctx)).Keys.Should().BeEquivalentTo(["VND"]);
    }

    [Fact]
    public async Task A_currency_with_no_account_is_refused()
    {
        using var ctx = await SeededAsync();

        // USD arrived from a stale page or by hand. Honouring it would print a heading
        // with no account number underneath it.
        await SetAsync(ctx, new MovableRates(), "USD");

        (await StoredAsync(ctx)).Should().BeEmpty();
    }

    [Fact]
    public async Task An_account_with_nothing_to_pay_into_is_refused()
    {
        using var ctx = await SeededAsync();
        await AddAccountAsync(ctx, "EUR", accountNumber: null);

        await SetAsync(ctx, new MovableRates(), "EUR");

        (await StoredAsync(ctx)).Should().BeEmpty();
    }

    [Fact]
    public async Task A_currency_with_no_rate_available_is_not_offered()
    {
        using var ctx = await SeededAsync();

        // Better to offer nothing than a guessed amount: the client would pay a figure
        // nobody agreed to, and it would read as a shortfall when it arrived.
        await SetAsync(ctx, new MovableRates { Available = false }, "VND");

        (await StoredAsync(ctx)).Should().BeEmpty();
    }

    [Fact]
    public async Task Null_leaves_the_existing_selection_alone()
    {
        using var ctx = await SeededAsync();
        await SetAsync(ctx, new MovableRates(), "VND");

        // An update that does not mention payment currencies must not silently clear
        // them — most callers have no opinion on the subject.
        await using (var db = ctx.NewContext())
        {
            await new InvoicePaymentCurrencyService(new MovableRates())
                .SetAsync(db, ctx.InvoiceId, InvoiceTestContext.UserId, null);
            await db.SaveChangesAsync();
        }

        (await StoredAsync(ctx)).Keys.Should().BeEquivalentTo(["VND"]);
    }

    [Fact]
    public async Task An_empty_list_clears_the_selection()
    {
        using var ctx = await SeededAsync();
        await SetAsync(ctx, new MovableRates(), "VND");

        await SetAsync(ctx, new MovableRates());

        (await StoredAsync(ctx)).Should().BeEmpty();
    }

    [Fact]
    public async Task A_draft_is_reconverted_when_the_rate_moves()
    {
        using var ctx = await SeededAsync();
        var rates = new MovableRates { Rate = 30_000m };
        await SetAsync(ctx, rates, "VND");

        // Nobody has this invoice yet, and its total can still change, so the figure it
        // quotes should be today's.
        rates.Rate = 26_000m;
        await SetAsync(ctx, rates, "VND");

        (await StoredAsync(ctx))["VND"].Should().Be(2_600_000m);
    }

    [Fact]
    public async Task An_issued_invoice_keeps_the_amount_the_client_was_given()
    {
        using var ctx = await SeededAsync();
        var rates = new MovableRates { Rate = 30_000m };
        await SetAsync(ctx, rates, "VND");
        await IssueAsync(ctx);

        // The client is holding a document that says 3,000,000 VND. Editing the invoice
        // afterwards — a note, a due date — must not restate what they owe.
        rates.Rate = 26_000m;
        await SetAsync(ctx, rates, "VND");

        (await StoredAsync(ctx))["VND"].Should().Be(3_000_000m);
    }

    [Fact]
    public async Task A_currency_added_after_issue_is_converted_at_todays_rate()
    {
        using var ctx = await SeededAsync();
        await AddAccountAsync(ctx, "EUR");
        var rates = new MovableRates { Rate = 30_000m };
        await SetAsync(ctx, rates, "VND");
        await IssueAsync(ctx);

        // EUR is an offer being made now, so it has no earlier rate to honour — while
        // VND, which the client already has, keeps the figure they were given.
        rates.Rate = 26_000m;
        await SetAsync(ctx, rates, "VND", "EUR");

        var stored = await StoredAsync(ctx);
        stored["VND"].Should().Be(3_000_000m);
        stored["EUR"].Should().Be(2_600_000m);
    }
}
