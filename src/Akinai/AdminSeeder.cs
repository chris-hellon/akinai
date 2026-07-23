using Akinai.Infrastructure.Data;
using Akinai.Shared.Services;
using Microsoft.AspNetCore.Identity;

namespace Akinai;

/// <summary>
/// Ensures the Admin role exists and that the configured owner account holds it.
///
/// Driven by the "Admin:Email" setting so the account is never hard-coded, and
/// idempotent so it can run on every start. It only ever *grants* the role —
/// it will not revoke one, so removing the setting can't lock you out, and it
/// creates no accounts.
/// </summary>
public static class AdminSeeder
{
    public static async Task SeedAsync(IServiceProvider services, IConfiguration configuration, ILogger logger)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

        if (!await roleManager.RoleExistsAsync(AdminRoles.Admin))
        {
            await roleManager.CreateAsync(new IdentityRole(AdminRoles.Admin));
            logger.LogInformation("Created the {Role} role.", AdminRoles.Admin);
        }

        var adminEmail = configuration["Admin:Email"];
        if (string.IsNullOrWhiteSpace(adminEmail))
        {
            logger.LogInformation("No Admin:Email configured — skipping admin assignment.");
            return;
        }

        var user = await userManager.FindByEmailAsync(adminEmail);
        if (user is null)
        {
            // Deliberately does not create the account — an admin has to register
            // through the normal flow first.
            logger.LogWarning("Admin:Email '{Email}' matches no registered account.", adminEmail);
            return;
        }

        if (!await userManager.IsInRoleAsync(user, AdminRoles.Admin))
        {
            await userManager.AddToRoleAsync(user, AdminRoles.Admin);
            logger.LogInformation("Granted {Role} to {Email}.", AdminRoles.Admin, adminEmail);
        }
    }
}
