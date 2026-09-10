using Akinai.Domain.Entities;
using Akinai.Infrastructure.Data;
using Akinai.Infrastructure.Services;
using Akinai.Shared.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Akinai.Tests;

/// <summary>
/// A real database for handler tests — SQLite in memory, built from the same model the
/// app uses. Not the EF in-memory provider: these tests exist to catch things that only
/// show up once queries, keys and relationships behave relationally.
///
/// The connection is held open for the fixture's lifetime because a SQLite in-memory
/// database exists only as long as a connection to it does.
/// </summary>
public sealed class InvoiceTestContext : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<ApplicationDbContext> _options;

    public const string UserId = "user-1";

    public IDbContextFactory<ApplicationDbContext> Factory { get; }
    public ICurrentUserService CurrentUser { get; }

    public Guid ClientId { get; private set; }
    public Guid PortalToken { get; private set; }
    public Guid ProjectId { get; private set; }
    public Guid JobId { get; private set; }
    public Guid InvoiceId { get; private set; }

    public InvoiceTestContext()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        // ApplicationDbContext resolves IHttpContextAccessor to stamp audit fields, and
        // EF throws outright when it is not registered — so give it one with no context.
        // Audit fields then fall back to "System", exactly as they do in a background job.
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions
            .AddSingleton<Microsoft.AspNetCore.Http.IHttpContextAccessor>(services, new NoHttpContext());

        _options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .UseApplicationServiceProvider(
                Microsoft.Extensions.DependencyInjection.ServiceCollectionContainerBuilderExtensions
                    .BuildServiceProvider(services))
            .Options;

        using (var db = new ApplicationDbContext(_options))
            db.Database.EnsureCreated();

        Factory = new TestDbContextFactory(_options);
        CurrentUser = new FakeCurrentUser(UserId);
    }

    public ApplicationDbContext NewContext() => new(_options);

    /// <summary>
    /// A client billing at <paramref name="clientRate"/>/hr with one job, one invoice,
    /// and a time entry per entry in <paramref name="entryDays"/> (day-of-July, hours).
    /// Totals are snapshotted through the real service, exactly as the handlers expect.
    /// </summary>
    public async Task SeedAsync(
        decimal clientRate = 100m,
        decimal taxRate = 20m,
        params (int Day, decimal Hours)[] entryDays)
    {
        if (entryDays.Length == 0) entryDays = [(1, 2m)];

        await using var db = NewContext();

        db.Users.Add(new ApplicationUser { Id = UserId, UserName = "u@example.com", Email = "u@example.com" });

        var client = Client.Create(UserId, "Acme", "acme@example.com", clientRate, "GBP");
        db.Clients.Add(client);

        var project = Project.Create(client.Id, "Website");
        db.Projects.Add(project);

        var job = Job.Create(project.Id, "Design");
        db.Jobs.Add(job);

        var invoice = Invoice.Create(
            "INV-00001", client.Id,
            new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31),
            taxRate: taxRate, currency: "GBP");
        db.Invoices.Add(invoice);
        await db.SaveChangesAsync();

        foreach (var (day, hours) in entryDays)
        {
            var entry = TimeEntry.Create(job.Id, new DateOnly(2026, 7, day), hours, $"Work {day}", UserId);
            entry.LinkToInvoice(invoice.Id, clientRate);
            db.TimeEntries.Add(entry);
        }
        await db.SaveChangesAsync();

        ClientId = client.Id;
        PortalToken = client.PortalToken!.Value;
        ProjectId = project.Id;
        JobId = job.Id;
        InvoiceId = invoice.Id;

        await RecalculateAsync();
    }

    /// <summary>Re-snapshots the invoice totals the way the write handlers do.</summary>
    public async Task RecalculateAsync()
    {
        await using var db = NewContext();
        var service = new InvoiceTotalService(new FixedExchangeRateService());
        await service.RecalculateAsync(db, InvoiceId, UserId);
        await db.SaveChangesAsync();
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    private sealed class TestDbContextFactory(DbContextOptions<ApplicationDbContext> options)
        : IDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext() => new(options);
    }

    /// <summary>Returns every string untouched — these tests assert money, not language.</summary>
    public sealed class PassThroughTranslator : Akinai.Shared.Services.IInvoiceTranslator
    {
        public Task<Akinai.Shared.Enums.SupportedLanguage> ResolveForInvoiceAsync(
            string userId, Guid clientId,
            Akinai.Shared.Enums.SupportedLanguage? invoiceLanguage = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(invoiceLanguage ?? Akinai.Shared.Enums.SupportedLanguage.English);

        public Task<IReadOnlyDictionary<string, string>> TranslateAsync(
            string userId, Akinai.Shared.Enums.SupportedLanguage target,
            IReadOnlyDictionary<string, string> textsByKey,
            CancellationToken cancellationToken = default)
            => Task.FromResult(textsByKey);
    }

    /// <summary>No HttpContext: the handlers under test all null-check it.</summary>
    public sealed class NoHttpContext : Microsoft.AspNetCore.Http.IHttpContextAccessor
    {
        public Microsoft.AspNetCore.Http.HttpContext? HttpContext { get; set; }
    }

    private sealed class FakeCurrentUser(string? userId) : ICurrentUserService
    {
        public string? UserId { get; } = userId;
        public string? UserName => "u@example.com";
        public string? Email => "u@example.com";
        public bool IsAuthenticated => UserId is not null;
        public bool IsInRole(string role) => false;
    }

    /// <summary>1:1 for every pair, so currency conversion never clouds a total assertion.</summary>
    private sealed class FixedExchangeRateService : IExchangeRateService
    {
        public Task<IReadOnlyDictionary<string, decimal>> GetUsdRatesAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyDictionary<string, decimal>>(
                new Dictionary<string, decimal> { ["USD"] = 1m, ["GBP"] = 1m });

        public Task<decimal?> ConvertAsync(decimal amount, string from, string to, CancellationToken ct = default)
            => Task.FromResult<decimal?>(amount);
    }
}
