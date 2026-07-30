using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using Akinai.Infrastructure.Data;
using Akinai.Shared.Api.BusinessProfile;
using Akinai.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace Akinai.Infrastructure.Handlers.BusinessProfile;

public class SaveBusinessLogoHandler(
    IDbContextFactory<ApplicationDbContext> dbFactory,
    ICurrentUserService currentUserService,
    IEntitlementService entitlements)
    : IHandle<SaveBusinessLogo>
{
    /// <summary>Guard rails on the upload. Enforced here, not just in the browser.</summary>
    private const int MaxBytes = 2 * 1024 * 1024;   // 2 MB
    private static readonly string[] AllowedTypes = ["image/png", "image/jpeg", "image/svg+xml", "image/webp"];

    public async Task<HttpResult> Handle(SaveBusinessLogo request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
            return HttpResult.Unauthorized();

        // A logo on the invoice is a Pro feature. The UI hides the control on
        // Free, but hiding a control has never stopped a crafted request.
        var plan = await entitlements.GetAsync(userId, cancellationToken);
        if (!plan.CustomTemplates)
            return HttpResult.Forbidden();

        // Reject before touching the database — an oversized or wrong-typed
        // upload is the caller's mistake, not a 500.
        if (request.Logo is null or { Length: 0 } || request.Logo.Length > MaxBytes)
            return HttpResult.BadRequest();
        if (string.IsNullOrWhiteSpace(request.ContentType)
            || !AllowedTypes.Contains(request.ContentType, StringComparer.OrdinalIgnoreCase))
            return HttpResult.BadRequest();

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var profile = await db.BusinessProfiles.FirstOrDefaultAsync(b => b.UserId == userId, cancellationToken);
        if (profile is null)
            return HttpResult.NotFound();

        profile.UpdateLogo(request.Logo, request.ContentType);
        await db.SaveChangesAsync(cancellationToken);
        return HttpResult.NoContent();
    }
}
