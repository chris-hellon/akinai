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

        // Register HttpClient for server-side rendering (e.g., VietQR API calls)
        builder.Services.AddHttpClient();
        builder.Services.AddScoped(sp => sp.GetRequiredService<IHttpClientFactory>().CreateClient());

        // Register Modal Service for server-side rendering
        builder.Services.AddScoped<IModalService, ModalService>();

        // Register Layout Service for JS interop (needed for prerendering)
        builder.Services.AddScoped<ILayoutService, LayoutService>();

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

        app.UseAuthentication();
        app.UseAuthorization();
        app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

        app.UseAntiforgery();

        // Map EasyApi endpoints
        app.MapRequests();

        app.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode()
            .AddInteractiveWebAssemblyRenderMode()
            .AddAdditionalAssemblies(typeof(Client._Imports).Assembly);

        // Add additional endpoints required by the Identity /Account Razor components.
        app.MapAdditionalIdentityEndpoints();

        app.Run();
    }
}
