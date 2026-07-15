using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using InvoiceSoftware.Domain.Enums;
using InvoiceSoftware.Infrastructure.Data;
using InvoiceSoftware.Shared.Api.BankImport;
using InvoiceSoftware.Shared.Dtos.BankImport;
using InvoiceSoftware.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace InvoiceSoftware.Infrastructure.Handlers.BankImport;

public class GetSuggestedMatchesHandler(IDbContextFactory<ApplicationDbContext> dbFactory, ICurrentUserService currentUserService)
    : IHandle<GetSuggestedMatches, List<TransactionMatchDto>>
{
    public async Task<HttpResult<List<TransactionMatchDto>>> Handle(
        GetSuggestedMatches request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
            return HttpResult<List<TransactionMatchDto>>.Unauthorized();

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var transaction = await db.BankTransactions
            .FirstOrDefaultAsync(bt => bt.Id == request.Id && bt.UserId == userId, cancellationToken);

        if (transaction == null)
            return HttpResult<List<TransactionMatchDto>>.NotFound();

        // Get unpaid invoices
        var invoices = await db.Invoices
            .Include(i => i.Client)
            .Include(i => i.TimeEntries)
                .ThenInclude(te => te.Job)
                    .ThenInclude(j => j.Project)
                        .ThenInclude(p => p.Client)
            .Where(i => i.Client.UserId == userId &&
                       (i.Status == InvoiceStatus.Sent || i.Status == InvoiceStatus.Overdue))
            .ToListAsync(cancellationToken);

        var suggestions = new List<TransactionMatchDto>();
        var transactionAmount = Math.Abs(transaction.Amount);

        foreach (var invoice in invoices)
        {
            // Calculate invoice total
            decimal invoiceTotal = 0;
            foreach (var te in invoice.TimeEntries)
            {
                var hourlyRate = te.Job.GetEffectiveHourlyRate();
                invoiceTotal += te.Hours.Value * hourlyRate;
            }
            invoiceTotal += invoiceTotal * invoice.TaxRate / 100;

            // Calculate match confidence
            var amountDiff = Math.Abs(invoiceTotal - transactionAmount);
            var amountMatchPercent = invoiceTotal > 0 ? 1 - (amountDiff / invoiceTotal) : 0;

            var confidence = MatchConfidence.None;
            var matchReason = "";

            // Exact amount match
            if (amountDiff < 0.01m)
            {
                confidence = MatchConfidence.High;
                matchReason = "Exact amount match";
            }
            // Close amount match
            else if (amountMatchPercent > 0.95m)
            {
                confidence = MatchConfidence.Medium;
                matchReason = $"Amount within 5% ({amountDiff:C2} difference)";
            }
            // Invoice number in description
            else if (transaction.Description.Contains(invoice.InvoiceNumber, StringComparison.OrdinalIgnoreCase))
            {
                confidence = MatchConfidence.High;
                matchReason = "Invoice number found in description";
            }
            // Client name in description
            else if (transaction.Description.Contains(invoice.Client.Name, StringComparison.OrdinalIgnoreCase))
            {
                confidence = MatchConfidence.Medium;
                matchReason = "Client name found in description";
            }
            // Reference match
            else if (!string.IsNullOrEmpty(transaction.Reference) &&
                    (transaction.Reference.Contains(invoice.InvoiceNumber, StringComparison.OrdinalIgnoreCase) ||
                     invoice.InvoiceNumber.Contains(transaction.Reference, StringComparison.OrdinalIgnoreCase)))
            {
                confidence = MatchConfidence.High;
                matchReason = "Reference matches invoice number";
            }

            if (confidence != MatchConfidence.None)
            {
                suggestions.Add(new TransactionMatchDto(
                    invoice.Id,
                    invoice.InvoiceNumber,
                    invoice.Client.Name,
                    invoiceTotal,
                    invoice.Currency,
                    invoice.DueDate,
                    invoice.Status.ToString(),
                    confidence.ToString(),
                    matchReason));
            }
        }

        // Sort by confidence (High first) then by amount difference
        var sortedSuggestions = suggestions
            .OrderByDescending(s => s.MatchConfidence == "High" ? 2 : s.MatchConfidence == "Medium" ? 1 : 0)
            .ThenBy(s => Math.Abs(s.InvoiceTotal - transactionAmount))
            .Take(5)
            .ToList();

        return HttpResult<List<TransactionMatchDto>>.Ok(sortedSuggestions);
    }
}
