using Akinai.Infrastructure.Handlers.Invoices;
using Akinai.Shared.Api.Invoices;
using Akinai.Shared.Dtos.Preflight;
using FluentAssertions;

namespace Akinai.Tests;

public class InvoicePreflightHandlerTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static GetInvoicePreflightHandler Handler(InvoiceTestContext ctx)
        => new(ctx.Factory, ctx.CurrentUser);

    [Fact]
    public async Task With_no_payment_details_at_all_the_invoice_is_blocked()
    {
        using var ctx = new InvoiceTestContext();
        await ctx.SeedAsync(entryDays: [(1, 2m)]);

        var result = await Handler(ctx).Handle(new GetInvoicePreflight { Id = ctx.InvoiceId }, Ct);

        result.Response!.HasBlocking.Should().BeTrue();
        result.Response.Findings.Should().Contain(f =>
            f.Area == "Payment" && f.Severity == PreflightSeverity.Blocking);
    }

    [Fact]
    public async Task An_EU_reverse_charge_invoice_missing_the_clients_VAT_number_is_blocked_on_tax()
    {
        using var ctx = new InvoiceTestContext();
        await ctx.SeedAsync(100m, 0m, sellerCountry: "DE", buyerCountry: "FR",
            sellerTaxNumber: "DE123456789", buyerTaxNumber: null, entryDays: [(1, 2m)]);

        var result = await Handler(ctx).Handle(new GetInvoicePreflight { Id = ctx.InvoiceId }, Ct);

        result.Response!.Findings.Should().Contain(f =>
            f.Area == "Tax" && f.Severity == PreflightSeverity.Blocking
            && f.Message.Contains("client's VAT number"));
    }

    [Fact]
    public async Task A_complete_EU_reverse_charge_invoice_reports_its_required_wording()
    {
        using var ctx = new InvoiceTestContext();
        await ctx.SeedAsync(100m, 0m, sellerCountry: "DE", buyerCountry: "FR",
            sellerTaxNumber: "DE123456789", buyerTaxNumber: "FR12345678901", entryDays: [(1, 2m)]);

        var result = await Handler(ctx).Handle(new GetInvoicePreflight { Id = ctx.InvoiceId }, Ct);

        result.Response!.Findings.Should().NotContain(f => f.Area == "Tax");
        result.Response.RequiredStatements.Should().ContainSingle();
        result.Response.TaxTreatment.Should().Be("EuReverseCharge");
    }

    [Fact]
    public async Task An_invoice_for_somebody_elses_client_is_not_found()
    {
        using var ctx = new InvoiceTestContext();
        await ctx.SeedAsync(entryDays: [(1, 2m)]);

        var stranger = new GetInvoicePreflightHandler(ctx.Factory, new InvoiceTestContext.OtherUser());
        var result = await stranger.Handle(new GetInvoicePreflight { Id = ctx.InvoiceId }, Ct);

        result.Response.Should().BeNull();
    }

    [Fact]
    public async Task A_malformed_client_VAT_number_is_flagged()
    {
        // "DE12345" is the right country, wrong shape — the commonest form of this slip.
        using var ctx = new InvoiceTestContext();
        await ctx.SeedAsync(100m, 0m, sellerCountry: "DE", buyerCountry: "FR",
            sellerTaxNumber: "DE136695976", buyerTaxNumber: "FR123", entryDays: [(1, 2m)]);

        var result = await Handler(ctx).Handle(new GetInvoicePreflight { Id = ctx.InvoiceId }, Ct);

        result.Response!.Findings.Should().Contain(f => f.Area == "Tax" && f.Message.Length > 0);
    }

    [Fact]
    public async Task A_client_in_a_country_with_no_VAT_rule_is_not_flagged()
    {
        // A US client has no VAT number scheme to check against. Treating "unchecked" as
        // "invalid" would redden every non-EU client on the system.
        using var ctx = new InvoiceTestContext();
        await ctx.SeedAsync(100m, 0m, sellerCountry: "GB", buyerCountry: "US",
            sellerTaxNumber: "GB980780684", buyerTaxNumber: "12-3456789", entryDays: [(1, 2m)]);

        var result = await Handler(ctx).Handle(new GetInvoicePreflight { Id = ctx.InvoiceId }, Ct);

        result.Response!.Findings.Should().NotContain(f =>
            f.Area == "Tax" && f.Severity == PreflightSeverity.Warning);
    }

    [Fact]
    public async Task A_client_paying_from_Vietnam_is_warned_about_the_purpose_code()
    {
        // The rule is an outbound exchange control: it is the client's country that
        // demands the code, and the client who cannot send the money without it.
        using var ctx = new InvoiceTestContext();
        await ctx.SeedAsync(100m, 0m, sellerCountry: "GB", buyerCountry: "VN",
            sellerTaxNumber: "GB980780684", buyerTaxNumber: null, entryDays: [(1, 2m)]);
        await ctx.AddPaymentDetailsAsync(iban: "GB82 WEST 1234 5698 7654 32", swift: "DEUTDEFF");

        var result = await Handler(ctx).Handle(new GetInvoicePreflight { Id = ctx.InvoiceId }, Ct);

        result.Response!.Findings.Should().Contain(f =>
            f.Area == "Payment" && f.Message.Contains("purpose of payment"));
    }
}
