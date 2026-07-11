using InvoiceSoftware.Infrastructure.Data;
using InvoiceSoftware.Infrastructure.Services;
using InvoiceSoftware.Shared.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace InvoiceSoftware.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Choose the database provider via the "DatabaseProvider" config setting ("SqlServer" or "Sqlite").
        // Defaults to SqlServer to preserve existing behaviour.
        var databaseProvider = configuration.GetValue<string>("DatabaseProvider") ?? "SqlServer";
        var useSqlite = string.Equals(databaseProvider, "Sqlite", StringComparison.OrdinalIgnoreCase);

        // Register DbContextFactory for concurrent handler support (parallel API calls)
        // Auditing is handled directly in ApplicationDbContext using IHttpContextAccessor via GetService
        services.AddDbContextFactory<ApplicationDbContext>((sp, options) =>
        {
            options.UseApplicationServiceProvider(sp);

            if (useSqlite)
            {
                var sqliteConnection = configuration.GetConnectionString("SqliteConnection") ?? "Data Source=InvoiceSoftware.db";
                // SQLite gets its schema from the model via EnsureCreated() at startup, so it doesn't use the migrations.
                options.UseSqlite(sqliteConnection);
            }
            else
            {
                var connectionString = configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
                options.UseSqlServer(connectionString, sqlOptions =>
                {
                    sqlOptions.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName);
                    sqlOptions.EnableRetryOnFailure(3);
                    // Keep the migrations history table in dbo, independent of the "App" default schema.
                    sqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "dbo");
                });
            }
        });

        // Also register scoped DbContext for backwards compatibility
        // Handlers now use IDbContextFactory directly
        services.AddScoped<ApplicationDbContext>(sp =>
        {
            var factory = sp.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
            return factory.CreateDbContext();
        });

        services.AddScoped<IDateTimeService, DateTimeService>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<PdfGeneratorService>();
        services.AddScoped<EmailTemplateService>();
        services.AddHostedService<RecurringExpenseGeneratorService>();

        return services;
    }
}
