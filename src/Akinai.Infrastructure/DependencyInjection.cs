using Akinai.Infrastructure.Data;
using Akinai.Infrastructure.Services;
using Akinai.Shared.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Akinai.Infrastructure;

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
        // PDFs are printed from the app's own pages by headless Chromium, so
        // the document a client opens and the one they receive are the same
        // markup. Singleton: one browser process, reused across requests.
        services.AddSingleton<Services.Pdf.IHtmlPdfRenderer, Services.Pdf.HtmlPdfRenderer>();
        // Still registered: estimates have not moved to HTML rendering yet.
        services.AddScoped<PdfGeneratorService>();
        services.AddScoped<EmailTemplateService>();
        services.AddScoped<INotificationService, NotificationService>();
        // Singleton: it fans out to every live circuit in this process.
        services.AddSingleton<IUserEventBus, UserEventBus>();

        // Multi-currency invoice totals: live FX (exchangerate-api.com) + snapshot service.
        services.Configure<ExchangeRateOptions>(configuration.GetSection(ExchangeRateOptions.SectionName));
        services.AddHttpClient<IExchangeRateService, ExchangeRateService>();
        services.AddScoped<IInvoiceTotalService, InvoiceTotalService>();

        // Billing. Swapping SimulatedBillingProvider for a Stripe implementation
        // is the whole of "going live" — nothing above this line knows which
        // provider is in use.
        services.AddScoped<Services.Billing.IBillingProvider, Services.Billing.SimulatedBillingProvider>();
        services.AddScoped<ISubscriptionService, Services.Billing.SubscriptionService>();
        // Plan catalogue reads from the database, cached, with the compiled
        // Plans class as its seed and its fallback.
        services.AddMemoryCache();
        services.AddScoped<IPlanCatalog, Services.Billing.PlanCatalog>();
        services.AddScoped<IEntitlementService, EntitlementService>();

        // AI + translation. The Azure Translator key is Akinai's, not the
        // user's; per-account spend is bounded by IAiBudgetService.
        services.Configure<Services.Translation.TranslationOptions>(
            configuration.GetSection(Services.Translation.TranslationOptions.SectionName));
        services.AddHttpClient<ITranslationService, Services.Translation.AzureTranslatorService>();
        services.AddScoped<IAiBudgetService, AiBudgetService>();
        services.AddScoped<IInvoiceTranslator, Services.Translation.InvoiceTranslator>();
        services.AddHostedService<InvoiceTotalBackfillService>();


        services.AddHostedService<RecurringExpenseGeneratorService>();

        services.AddHostedService<OverdueInvoiceService>();

        return services;
    }
}
