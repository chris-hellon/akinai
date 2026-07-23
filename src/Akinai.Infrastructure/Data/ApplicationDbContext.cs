using Akinai.Domain.Common;
using Akinai.Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using System.Security.Claims;

namespace Akinai.Infrastructure.Data;

public sealed class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    // Existing entities
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectSection> ProjectSections => Set<ProjectSection>();
    public DbSet<Job> Jobs => Set<Job>();
    public DbSet<JobTask> JobTasks => Set<JobTask>();
    public DbSet<TimeEntry> TimeEntries => Set<TimeEntry>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceLineItem> InvoiceLineItems => Set<InvoiceLineItem>();
    public DbSet<InvoiceCurrencyTotal> InvoiceCurrencyTotals => Set<InvoiceCurrencyTotal>();
    public DbSet<BusinessProfile> BusinessProfiles => Set<BusinessProfile>();
    public DbSet<CurrencyPaymentSettings> CurrencyPaymentSettings => Set<CurrencyPaymentSettings>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<RecurringExpense> RecurringExpenses => Set<RecurringExpense>();

    // New entities - Products & Estimates
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Estimate> Estimates => Set<Estimate>();
    public DbSet<EstimateLineItem> EstimateLineItems => Set<EstimateLineItem>();

    // New entities - Recurring Invoices
    public DbSet<RecurringInvoice> RecurringInvoices => Set<RecurringInvoice>();
    public DbSet<RecurringInvoiceLineItem> RecurringInvoiceLineItems => Set<RecurringInvoiceLineItem>();

    // New entities - Attachments & Signatures
    public DbSet<Attachment> Attachments => Set<Attachment>();
    public DbSet<DigitalSignature> DigitalSignatures => Set<DigitalSignature>();

    // New entities - Bank Import
    public DbSet<BankTransaction> BankTransactions => Set<BankTransaction>();

    // New entities - Templates & Localization
    public DbSet<InvoiceTemplate> InvoiceTemplates => Set<InvoiceTemplate>();
    public DbSet<UserLanguagePreference> UserLanguagePreferences => Set<UserLanguagePreference>();
    public DbSet<ClientLanguageSetting> ClientLanguageSettings => Set<ClientLanguageSetting>();

    // Notifications
    public DbSet<Notification> Notifications => Set<Notification>();

    // Billing
    public DbSet<SubscriptionEvent> SubscriptionEvents => Set<SubscriptionEvent>();
    public DbSet<CheckoutSession> CheckoutSessions => Set<CheckoutSession>();
    public DbSet<PlanSettings> PlanSettings => Set<PlanSettings>();

    // AI + translation usage, for the monthly spend cap
    public DbSet<AiUsage> AiUsages => Set<AiUsage>();
    public DbSet<TranslationCacheEntry> TranslationCache => Set<TranslationCacheEntry>();

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public override int SaveChanges()
    {
        UpdateAuditFields();
        return base.SaveChanges();
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        UpdateAuditFields();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        UpdateAuditFields();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        UpdateAuditFields();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void UpdateAuditFields()
    {
        var now = DateTime.UtcNow;

        // Get IHttpContextAccessor from the service provider
        var httpContextAccessor = this.GetService<IHttpContextAccessor>();
        var userId = httpContextAccessor?.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? "System";

        foreach (var entry in ChangeTracker.Entries<IAuditableEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedBy = userId;
                entry.Entity.CreatedAt = now;
            }

            if (entry.State == EntityState.Added || entry.State == EntityState.Modified)
            {
                entry.Entity.ModifiedBy = userId;
                entry.Entity.ModifiedAt = now;
            }
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        // Subscription fields on the Identity user. Stored as strings so the
        // Users table stays readable when someone is looking at a billing
        // question directly in the database.
        modelBuilder.Entity<ApplicationUser>(user =>
        {
            user.Property(u => u.Plan).HasConversion<string>().HasMaxLength(40).IsRequired();
            user.Property(u => u.SubscriptionStatus).HasConversion<string>().HasMaxLength(40).IsRequired();
            user.Property(u => u.BillingCycle).HasConversion<string>().HasMaxLength(20);
            user.Property(u => u.ProviderCustomerId).HasMaxLength(200);
            user.Property(u => u.ProviderSubscriptionId).HasMaxLength(200);
        });

        // Organise tables into schemas: application tables live in the "App" schema,
        // and the ASP.NET Identity tables live in the "Identity" schema with the
        // redundant "AspNet" prefix removed (AspNetUsers -> Users, AspNetRoles -> Roles, etc.).
        // Note: SQLite has no concept of schemas, so its provider ignores the schema and
        // creates every table in the single default namespace (the renames still apply).
        modelBuilder.HasDefaultSchema("App");

        var identityTableNames = new Dictionary<string, string>
        {
            ["AspNetUsers"] = "Users",
            ["AspNetRoles"] = "Roles",
            ["AspNetUserClaims"] = "UserClaims",
            ["AspNetUserRoles"] = "UserRoles",
            ["AspNetUserLogins"] = "UserLogins",
            ["AspNetUserTokens"] = "UserTokens",
            ["AspNetRoleClaims"] = "RoleClaims",
            ["AspNetUserPasskeys"] = "UserPasskeys",
        };

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var tableName = entityType.GetTableName();
            if (tableName is not null && identityTableNames.TryGetValue(tableName, out var newName))
            {
                entityType.SetTableName(newName);
                entityType.SetSchema("Identity");
            }
        }
    }
}
