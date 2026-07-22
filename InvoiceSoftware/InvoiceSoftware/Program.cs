using ApexCharts;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using InvoiceSoftware.Components;
using InvoiceSoftware.Components.Account;
using InvoiceSoftware.Infrastructure;
using InvoiceSoftware.Infrastructure.Data;
using InvoiceSoftware.Infrastructure.Services;
using InvoiceSoftware.Infrastructure.Handlers.Clients;
using InvoiceSoftware.Shared.Api.Clients;
using InvoiceSoftware.Client.Services;
using InvoiceSoftware.Shared.Services;
using InvoiceSoftware.Shared.Services.Modal;
using InvoiceSoftware.Services;

namespace InvoiceSoftware;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add Infrastructure layer
        builder.Services.AddInfrastructure(builder.Configuration);

        // Add services to the container.
        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents()
            .AddInteractiveWebAssemblyComponents()
            .AddAuthenticationStateSerialization();

        builder.Services.AddCascadingAuthenticationState();
        builder.Services.AddScoped<IdentityRedirectManager>();
        builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

        builder.Services.AddAuthentication(options =>
            {
                options.DefaultScheme = IdentityConstants.ApplicationScheme;
                options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
            })
            .AddIdentityCookies();

        builder.Services.AddDatabaseDeveloperPageExceptionFilter();

        builder.Services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.SignIn.RequireConfirmedAccount = true;
                options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
            })
            .AddRoles<IdentityRole>()          // enables [Authorize(Roles=…)] and role claims
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        // Configure SMTP email sender
        builder.Services.Configure<SmtpSettings>(builder.Configuration.GetSection("Smtp"));
        builder.Services.AddTransient<SmtpEmailSender>();
        builder.Services.AddTransient<IEmailSender<ApplicationUser>>(sp => sp.GetRequiredService<SmtpEmailSender>());
        builder.Services.AddTransient<IEmailSender>(sp => sp.GetRequiredService<SmtpEmailSender>());
        builder.Services.AddTransient<IInvoiceEmailSender>(sp => sp.GetRequiredService<SmtpEmailSender>());

        // Add HttpContextAccessor for CurrentUserService
        builder.Services.AddHttpContextAccessor();

        // Register EasyApi with handlers from Infrastructure and contracts from Shared
        builder.Services.AddEasyApi()
            .WithContract(typeof(GetClients).Assembly)
            .WithServer(typeof(GetClientsHandler).Assembly);

        // Real-time notifications hub
        builder.Services.AddSignalR();

        // Register HttpClient for server-side rendering (e.g., VietQR API calls)
        builder.Services.AddHttpClient();
        builder.Services.AddScoped(sp => sp.GetRequiredService<IHttpClientFactory>().CreateClient());

        // Register Modal Service for server-side rendering
        builder.Services.AddScoped<IModalService, ModalService>();

        // Register Layout Service for JS interop (needed for prerendering)
        builder.Services.AddScoped<ILayoutService, LayoutService>();

        // Notifications state so it resolves for interactive-server rendering too
        builder.Services.AddScoped<InvoiceSoftware.Client.Services.NotificationState>();
        builder.Services.AddScoped<InvoiceSoftware.Client.Services.ReportingCurrencyState>();
        builder.Services.AddScoped<InvoiceSoftware.Client.Services.TimeTrackingState>();

        // Interactive charts (ApexCharts) — registered on the server too for prerender/interactive-server
        builder.Services.AddApexCharts();

        var app = builder.Build();

        // Apply the database schema at startup.
        // SQLite builds its schema from the model (EnsureCreated); SQL Server applies EF migrations.
        using (var scope = app.Services.CreateScope())
        {
            var databaseProvider = app.Configuration.GetValue<string>("DatabaseProvider") ?? "SqlServer";
            var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
            using var db = dbFactory.CreateDbContext();

            if (string.Equals(databaseProvider, "Sqlite", StringComparison.OrdinalIgnoreCase))
            {
                db.Database.EnsureCreated();
            }
            else
            {
                db.Database.Migrate();
            }

            AdminSeeder.SeedAsync(
                scope.ServiceProvider,
                app.Configuration,
                scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("AdminSeeder"))
                .GetAwaiter().GetResult();
        }

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.UseWebAssemblyDebugging();
            app.UseMigrationsEndPoint();
        }
        else
        {
            app.UseExceptionHandler("/Error");
            app.UseHsts();
        }

        app.UseHttpsRedirection();

        app.MapStaticAssets();

        // Must sit before the auth middleware so the re-executed "/not-found" request
        // still passes through authentication/authorization (otherwise a 404 throws
        // "endpoint contains authorization metadata, but a middleware was not found").
        app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

        app.UseAuthentication();
        app.UseAuthorization();

        app.UseAntiforgery();

        // ---------------------------------------------------------------------
        // API authentication gate.
        //
        // EasyApi's MapRequests() maps every contract with no authorization
        // metadata and returns void, so it cannot be chained with
        // RequireAuthorization(). Without this gate every /api endpoint was
        // reachable anonymously — GET /api/clients?Page=1&PageSize=5 returned
        // real client names and email addresses to an unauthenticated caller.
        //
        // Everything under /api therefore requires an authenticated user,
        // except the client-facing routes below, which are authorised by an
        // unguessable token in the URL (invoice/estimate links and the client
        // portal) and are used by people who have no account.
        //
        // This runs after UseAuthentication so context.User is populated, and
        // before endpoint execution, so it applies to every verb.
        // ---------------------------------------------------------------------
        app.Use(async (context, next) =>
        {
            var path = context.Request.Path;

            // Admin endpoints need the Admin role, not just a login. Enforced
            // here AND re-checked inside each handler — the UI hiding a nav
            // link is not access control.
            if (path.StartsWithSegments("/api/admin") && !context.User.IsInRole(AdminRoles.Admin))
            {
                var scp = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IStatusCodePagesFeature>();
                if (scp is not null) scp.Enabled = false;
                context.Response.StatusCode = context.User.Identity?.IsAuthenticated == true
                    ? StatusCodes.Status403Forbidden
                    : StatusCodes.Status401Unauthorized;
                return;
            }

            if (path.StartsWithSegments("/api")
                && !ApiPublicPaths.IsPublic(path)
                && context.User.Identity?.IsAuthenticated != true)
            {
                // Suppress UseStatusCodePagesWithReExecute for API calls — an API
                // client wants a bare 401, not a 9KB HTML error page.
                var statusCodePages = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IStatusCodePagesFeature>();
                if (statusCodePages is not null) statusCodePages.Enabled = false;
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            await next();
        });

        // Map EasyApi endpoints
        app.MapRequests();

        // Real-time notifications hub
        app.MapHub<InvoiceSoftware.Infrastructure.Hubs.NotificationHub>("/hubs/notifications");

        app.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode()
            .AddInteractiveWebAssemblyRenderMode()
            .AddAdditionalAssemblies(typeof(Client._Imports).Assembly);

        // Add additional endpoints required by the Identity /Account Razor components.
        app.MapAdditionalIdentityEndpoints();

        app.Run();
    }
}
