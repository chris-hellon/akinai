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
        // Stripe when it is configured, the simulator otherwise. Chosen at
        // startup so a half-configured deployment cannot take money it has no
        // way to confirm — StripeOptions.IsConfigured requires the webhook
        // secret as well as the API key.
        services.Configure<Services.Billing.StripeOptions>(
            configuration.GetSection(Services.Billing.StripeOptions.SectionName));

        // Where the public contact form delivers. Unset means the form reports a
        // send failure rather than silently swallowing messages.
        services.Configure<Handlers.Contact.ContactOptions>(
            configuration.GetSection(Handlers.Contact.ContactOptions.SectionName));

        var stripe = configuration.GetSection(Services.Billing.StripeOptions.SectionName)
            .Get<Services.Billing.StripeOptions>();

        if (stripe?.IsConfigured == true)
        {
            services.AddScoped<Services.Billing.IBillingProvider, Services.Billing.StripeBillingProvider>();
            services.AddScoped<Services.Billing.StripeWebhookHandler>();
        }
        else
        {
            services.AddScoped<Services.Billing.IBillingProvider, Services.Billing.SimulatedBillingProvider>();
        }
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

        // Anthropic. Unset key means every AI feature that needs it reports
        // itself unconfigured rather than failing at call time.
        services.Configure<Services.Ai.ClaudeOptions>(
            configuration.GetSection(Services.Ai.ClaudeOptions.SectionName));

        // The client cache is the singleton; the service using it is scoped,
        // because it depends on the scoped budget service. Registering the
        // service itself as a singleton would capture that scoped dependency.
        services.AddSingleton<Services.Ai.ClaudeClientFactory>();
        services.AddScoped<ITimesheetSummariser, Services.Ai.ClaudeTimesheetSummariser>();
        services.AddHostedService<InvoiceTotalBackfillService>();

        services.AddHostedService<CountryCodeBackfillService>();


        services.AddHostedService<RecurringExpenseGeneratorService>();

        services.AddHostedService<OverdueInvoiceService>();

        return services;
    }
}
