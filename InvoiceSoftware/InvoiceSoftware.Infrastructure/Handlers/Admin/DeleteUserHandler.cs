using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using InvoiceSoftware.Infrastructure.Data;
using InvoiceSoftware.Shared.Api.Admin;
using InvoiceSoftware.Shared.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace InvoiceSoftware.Infrastructure.Handlers.Admin;

/// <summary>
/// Permanently erases an account and everything it owns (GDPR erasure).
///
/// The ownership FKs are Restrict, so nothing can be deleted out of order —
/// which is deliberate. Rows are removed children-first inside a single
/// transaction, so a failure part-way leaves the account fully intact rather
/// than half-erased.
/// </summary>
public class DeleteUserHandler(
    IDbContextFactory<ApplicationDbContext> dbFactory,
    UserManager<ApplicationUser> userManager,
    ICurrentUserService currentUserService,
    ILogger<DeleteUserHandler> logger)
    : IHandle<DeleteUser>
{
    public async Task<HttpResult> Handle(DeleteUser request, CancellationToken cancellationToken)
    {
        if (!currentUserService.IsInRole(AdminRoles.Admin)) return HttpResult.Forbidden();

        // You cannot erase yourself — there would be no way back.
        if (request.UserId == currentUserService.UserId)
        {
            logger.LogWarning("Delete refused: cannot delete your own account.");
            return HttpResult.BadRequest();
        }

        var user = await userManager.FindByIdAsync(request.UserId);
        if (user is null) return HttpResult.NotFound();

        // Typing the address is a deliberate speed bump against a stray click.
        if (!string.Equals(request.ConfirmEmail?.Trim(), user.Email, StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning("Delete refused: confirmation '{Confirm}' does not match '{Email}'.",
                request.ConfirmEmail, user.Email);
            return HttpResult.BadRequest();
        }

        // Never erase the last admin.
        if (await userManager.IsInRoleAsync(user, AdminRoles.Admin))
        {
            var admins = await userManager.GetUsersInRoleAsync(AdminRoles.Admin);
            if (admins.Count <= 1)
            {
                logger.LogWarning("Delete refused: would remove the last admin.");
                return HttpResult.BadRequest();
            }
        }

        var uid = user.Id;
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        // The context is configured with EnableRetryOnFailure, so a manual
        // transaction must be wrapped in the execution strategy — otherwise EF
        // throws "does not support user-initiated transactions" and nothing is
        // deleted.
        var strategy = db.Database.CreateExecutionStrategy();
        var ok = false;

        try
        {
            await strategy.ExecuteAsync(async () =>
            {
            await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
            // Signatures reference invoices/estimates/clients/projects by id with
            // no FK, so they must go before the rows they point at — otherwise
            // they'd survive as dangling records after erasure.
            var invoiceIds  = await db.Invoices.Where(i => i.Client.UserId == uid).Select(i => i.Id).ToListAsync(cancellationToken);
            var estimateIds = await db.Estimates.Where(e => e.UserId == uid).Select(e => e.Id).ToListAsync(cancellationToken);
            var clientIds   = await db.Clients.Where(c => c.UserId == uid).Select(c => c.Id).ToListAsync(cancellationToken);
            var projectIds  = await db.Projects.Where(p => p.Client.UserId == uid).Select(p => p.Id).ToListAsync(cancellationToken);
            var linkedIds   = invoiceIds.Concat(estimateIds).Concat(clientIds).Concat(projectIds).ToList();

            await db.DigitalSignatures.Where(s => linkedIds.Contains(s.LinkedEntityId)).ExecuteDeleteAsync(cancellationToken);

            // Children first, then parents.
            await db.JobTasks.Where(t => t.Job.Project.Client.UserId == uid).ExecuteDeleteAsync(cancellationToken);
            await db.TimeEntries.Where(t => t.UserId == uid).ExecuteDeleteAsync(cancellationToken);
            await db.InvoiceLineItems.Where(li => li.Invoice.Client.UserId == uid).ExecuteDeleteAsync(cancellationToken);
            await db.InvoiceCurrencyTotals.Where(ct => ct.Invoice.Client.UserId == uid).ExecuteDeleteAsync(cancellationToken);
            await db.EstimateLineItems.Where(li => li.Estimate.UserId == uid).ExecuteDeleteAsync(cancellationToken);
            await db.RecurringInvoiceLineItems.Where(li => li.RecurringInvoice.UserId == uid).ExecuteDeleteAsync(cancellationToken);

            // Bank transactions point at invoices and expenses.
            await db.BankTransactions.Where(bt => bt.UserId == uid).ExecuteDeleteAsync(cancellationToken);
            await db.Expenses.Where(e => e.UserId == uid).ExecuteDeleteAsync(cancellationToken);
            await db.RecurringExpenses.Where(re => re.UserId == uid).ExecuteDeleteAsync(cancellationToken);

            await db.Invoices.Where(i => i.Client.UserId == uid).ExecuteDeleteAsync(cancellationToken);
            await db.Estimates.Where(e => e.UserId == uid).ExecuteDeleteAsync(cancellationToken);
            await db.RecurringInvoices.Where(ri => ri.UserId == uid).ExecuteDeleteAsync(cancellationToken);

            await db.Jobs.Where(j => j.Project.Client.UserId == uid).ExecuteDeleteAsync(cancellationToken);
            await db.ProjectSections.Where(ps => ps.Project.Client.UserId == uid).ExecuteDeleteAsync(cancellationToken);
            await db.Projects.Where(p => p.Client.UserId == uid).ExecuteDeleteAsync(cancellationToken);
            await db.ClientLanguageSettings.Where(x => x.UserId == uid).ExecuteDeleteAsync(cancellationToken);
            await db.Clients.Where(c => c.UserId == uid).ExecuteDeleteAsync(cancellationToken);

            // Remaining directly-owned rows.
            await db.Attachments.Where(x => x.UserId == uid).ExecuteDeleteAsync(cancellationToken);
            await db.Notifications.Where(x => x.UserId == uid).ExecuteDeleteAsync(cancellationToken);
            await db.Products.Where(x => x.UserId == uid).ExecuteDeleteAsync(cancellationToken);
            await db.InvoiceTemplates.Where(x => x.UserId == uid).ExecuteDeleteAsync(cancellationToken);
            await db.BusinessProfiles.Where(x => x.UserId == uid).ExecuteDeleteAsync(cancellationToken);
            await db.CurrencyPaymentSettings.Where(x => x.UserId == uid).ExecuteDeleteAsync(cancellationToken);
            await db.UserLanguagePreferences.Where(x => x.UserId == uid).ExecuteDeleteAsync(cancellationToken);

            // Finally the account itself. Deleted through THIS context so it is
            // part of the same transaction — UserManager.DeleteAsync uses its own
            // context/connection and would block on the uncommitted deletes above.
            // Identity's child tables (UserRoles, UserClaims, UserLogins,
            // UserTokens, UserPasskeys) are all ON DELETE CASCADE in the schema.
            await db.Users.Where(u => u.Id == uid).ExecuteDeleteAsync(cancellationToken);

            await tx.CommitAsync(cancellationToken);
            ok = true;
            });

            if (!ok) return HttpResult.BadRequest();

            logger.LogWarning("Admin {Admin} permanently erased account {Email} ({UserId}).",
                currentUserService.Email, user.Email, uid);

            return HttpResult.NoContent();
        }
        catch (Exception ex)
        {
            // The transaction is rolled back by disposal inside the strategy, so
            // the account is left fully intact rather than half-erased.
            logger.LogError(ex, "Erasing account {UserId} failed; rolled back.", uid);
            return HttpResult.BadRequest();
        }
    }
}
