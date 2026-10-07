using Akinai.Infrastructure.Handlers.Invoices;
using Akinai.Shared.Api.Invoices;
using Akinai.Shared.Compliance;
using FluentAssertions;

namespace Akinai.Tests;

/// <summary>
/// The corridor rules are unit-tested on their own; these check that an invoice actually
/// carries the wording through the handler, because a statement the rules got right but
/// the document never printed is worth nothing.
/// </summary>
public class InvoiceComplianceHandlerTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static GetInvoiceHandler Handler(InvoiceTestContext ctx) =>
        new(ctx.Factory, ctx.CurrentUser, new InvoiceTestContext.PassThroughTranslator());

    [Fact]
    public async Task An_EU_cross_border_B2B_invoice_carries_the_reverse_charge_statement()
    {
        using var ctx = new InvoiceTestContext();
        await ctx.SeedAsync(100m, 0m, sellerCountry: "DE", buyerCountry: "FR",
            sellerTaxNumber: "DE123456789", buyerTaxNumber: "FR12345678901",
            entryDays: [(1, 2m)]);

        var detail = await Handler(ctx).Handle(new GetInvoice { Id = ctx.InvoiceId }, Ct);

        detail.Response!.TaxTreatment.Should().Be(nameof(CorridorRules.Treatment.EuReverseCharge));
        detail.Response.RequiredStatements.Should().ContainSingle()
            .Which.Should().Be(CorridorRules.ReverseChargeStatement);
    }

    [Fact]
    public async Task A_domestic_invoice_carries_no_extra_wording()
    {
        using var ctx = new InvoiceTestContext();
        await ctx.SeedAsync(100m, 20m, sellerCountry: "GB", buyerCountry: "GB",
            sellerTaxNumber: "GB123", buyerTaxNumber: null, entryDays: [(1, 2m)]);

        var detail = await Handler(ctx).Handle(new GetInvoice { Id = ctx.InvoiceId }, Ct);

        detail.Response!.TaxTreatment.Should().Be(nameof(CorridorRules.Treatment.Domestic));
        detail.Response.RequiredStatements.Should().BeEmpty();
    }

    [Fact]
    public async Task A_UK_seller_billing_the_EU_gets_no_reverse_charge_statement()
    {
        // Post-Brexit this is an export, not an intra-EU reverse charge. Printing the
        // statement here would be actively wrong.
        using var ctx = new InvoiceTestContext();
        await ctx.SeedAsync(100m, 0m, sellerCountry: "GB", buyerCountry: "DE",
            sellerTaxNumber: "GB123", buyerTaxNumber: "DE123", entryDays: [(1, 2m)]);

        var detail = await Handler(ctx).Handle(new GetInvoice { Id = ctx.InvoiceId }, Ct);

        detail.Response!.RequiredStatements.Should().BeEmpty();
        detail.Response.TaxTreatment.Should().Be(nameof(CorridorRules.Treatment.CrossBorderNonEu));
    }

    [Fact]
    public async Task With_no_countries_set_the_treatment_is_unknown_rather_than_guessed()
    {
        using var ctx = new InvoiceTestContext();
        await ctx.SeedAsync(100m, 20m, entryDays: [(1, 2m)]);

        var detail = await Handler(ctx).Handle(new GetInvoice { Id = ctx.InvoiceId }, Ct);

        detail.Response!.TaxTreatment.Should().Be(nameof(CorridorRules.Treatment.Unknown));
        detail.Response.RequiredStatements.Should().BeEmpty();
    }

    [Fact]
    public async Task The_client_copy_carries_the_same_wording_as_the_owners_view()
    {
        // The document the client holds and the one the sender sees must not disagree
        // about what the invoice says — the same rule as the totals.
        using var ctx = new InvoiceTestContext();
        await ctx.SeedAsync(100m, 0m, sellerCountry: "DE", buyerCountry: "FR",
            sellerTaxNumber: "DE1", buyerTaxNumber: "FR1", entryDays: [(1, 2m)]);

        var owner = await Handler(ctx).Handle(new GetInvoice { Id = ctx.InvoiceId }, Ct);

        var assessment = await InvoiceCompliance.AssessAsync(
            ctx.NewContext(), ctx.InvoiceId, InvoiceTestContext.UserId, Ct);

        assessment.RequiredStatements.Should().Equal(owner.Response!.RequiredStatements);
    }
}
