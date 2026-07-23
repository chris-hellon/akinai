using Akinai.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Akinai.Infrastructure.Services;

public class RecurringExpenseGeneratorService(
    IServiceScopeFactory scopeFactory,
    ILogger<RecurringExpenseGeneratorService> logger) : BackgroundService
{
    private readonly TimeSpan _interval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Recurring Expense Generator Service started");

        // Run once on startup
        await GenerateExpensesAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_interval, stoppingToken);
                await GenerateExpensesAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Service is stopping
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in Recurring Expense Generator Service");
            }
        }

        logger.LogInformation("Recurring Expense Generator Service stopped");
    }

    private async Task GenerateExpensesAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var activeRecurring = await db.RecurringExpenses
            .Where(re => re.IsActive && re.NextExpenseDate <= today)
            .ToListAsync(cancellationToken);

        if (activeRecurring.Count == 0)
        {
            logger.LogDebug("No recurring expenses due for generation");
            return;
        }

        var generatedCount = 0;

        foreach (var recurring in activeRecurring)
        {
            // Generate all due expenses (could be multiple if service was offline)
            while (recurring.IsActive && recurring.NextExpenseDate <= today)
            {
                var expense = recurring.GenerateExpense(today);
                if (expense is not null)
                {
                    db.Expenses.Add(expense);
                    generatedCount++;
                    logger.LogDebug("Generated expense {ExpenseId} from recurring expense {RecurringId}",
                        expense.Id, recurring.Id);
                }
                else
                {
                    break;
                }
            }

            db.RecurringExpenses.Update(recurring);
        }

        if (generatedCount > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Generated {Count} expenses from {RecurringCount} recurring expenses",
                generatedCount, activeRecurring.Count);
        }
    }
}
