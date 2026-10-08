using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using Akinai.Domain.Services;
using Akinai.Infrastructure.Data;
using Akinai.Shared.Api.Invoices;
using Akinai.Shared.Compliance;
using Akinai.Shared.Dtos.Preflight;
using Akinai.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace Akinai.Infrastructure.Handlers.Invoices;

/// <summary>
/// Runs both pre-send checks over one invoice and returns what they found.
///
/// Read-only, and deliberately advisory: it never refuses to send. The rules here are
/// good but not infallible — a corridor this table does not model, or a bank that accepts
/// what the format check rejects, must not leave someone unable to invoice. The UI shows
/// the findings and the person decides.
/// </summary>
public class GetInvoicePreflightHandler(
    IDbContextFactory<ApplicationDbContext> dbFactory,
    ICurrentUserService currentUserService) : IHandle<GetInvoicePreflight, InvoicePreflightDto?>
{
    public async Task<HttpResult<InvoicePreflightDto?>> Handle(
        GetInvoicePreflight request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId)) return HttpResult<InvoicePreflightDto?>.Unauthorized();

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var invoice = await db.Invoices
            .AsNoTracking()
            .Where(i => i.Id == request.Id && i.Client.UserId == userId)
            .Select(i => new
            {
                i.Id,
                i.Currency,
                BuyerCountryCode = i.Client.BillingAddress != null ? i.Client.BillingAddress.CountryCode : null,
                BuyerTaxNumber = i.Client.TaxNumber,
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (invoice is null) return HttpResult<InvoicePreflightDto?>.NotFound();

        var findings = new List<PreflightFindingDto>();

        // --- Can it be paid? ----------------------------------------------
        // The settings for the invoice's own currency are the ones the client will be
        // given, so those are the ones worth checking.
        var payment = await db.CurrencyPaymentSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.UserId == userId && c.CurrencyCode == invoice.Currency, cancellationToken);

        if (payment is null)
        {
            findings.Add(new PreflightFindingDto(PreflightSeverity.Blocking, "Payment",
                $"No payment details are set up for {invoice.Currency}, so the invoice will go out with nothing to pay into."));
        }
        else
        {
            var details = new PayabilityCheck.Details(
                Iban: payment.BankIban,
                Swift: payment.BankSwift,
                AccountNumber: payment.BankAccountNumber,
                SortCode: payment.BankSortCode,
                PurposeCode: payment.PaymentPurposeCode,
                IntermediarySwift: payment.IntermediaryBankSwift);

            // Keyed on the client's country: a purpose-of-payment code is an outbound
            // exchange control, so it is the country the money leaves that demands it, and
            // the client who cannot send without one.
            foreach (var f in PayabilityCheck.Run(details, invoice.BuyerCountryCode))
            {
                findings.Add(new PreflightFindingDto(
                    f.Severity == PayabilityCheck.Severity.Blocking
                        ? PreflightSeverity.Blocking
                        : PreflightSeverity.Warning,
                    "Payment",
                    f.Message));
            }
        }

        // --- Is the client's VAT number even the right shape? --------------
        // Checked offline, by format and checksum. A number of the wrong shape is a
        // transcription slip or the wrong client's number pasted in, and either way the
        // invoice will be queried. Note IsRejected rather than !IsValid: a country with
        // no rule is unchecked, not wrong, and must not redden a perfectly good US client.
        if (!string.IsNullOrWhiteSpace(invoice.BuyerTaxNumber))
        {
            var format = TaxNumberFormat.Validate(invoice.BuyerTaxNumber, invoice.BuyerCountryCode);
            if (format.IsRejected)
            {
                findings.Add(new PreflightFindingDto(PreflightSeverity.Warning, "Tax", format.Message));
            }
        }

        // --- Does it say what it must? ------------------------------------
        var compliance = await InvoiceCompliance.AssessAsync(db, invoice.Id, userId, cancellationToken);

        foreach (var f in compliance.Findings)
        {
            findings.Add(new PreflightFindingDto(
                f.Severity switch
                {
                    CorridorRules.Severity.Blocking => PreflightSeverity.Blocking,
                    CorridorRules.Severity.Warning => PreflightSeverity.Warning,
                    _ => PreflightSeverity.Info,
                },
                "Tax",
                f.Message));
        }

        return HttpResult<InvoicePreflightDto?>.Ok(new InvoicePreflightDto(
            findings,
            compliance.Treatment.ToString(),
            compliance.RequiredStatements));
    }
}
