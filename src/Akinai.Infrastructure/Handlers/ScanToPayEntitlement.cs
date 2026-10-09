using Akinai.Shared.Services;
using Microsoft.Extensions.Logging;

namespace Akinai.Infrastructure.Handlers;

/// <summary>
/// Decides whether a client-facing document carries a scan-to-pay QR.
///
/// Answered here rather than in the renderer, for the same reason as
/// <see cref="DocumentBranding"/>: the person opening a public invoice is an anonymous
/// client with no plan of their own, so the only account whose entitlements mean anything
/// is the one that issued it.
/// </summary>
internal static class ScanToPayEntitlement
{
    public static async Task<bool> ShowForAsync(
        IEntitlementService entitlements,
        string? ownerId,
        CancellationToken cancellationToken,
        ILogger? logger = null)
    {
        if (string.IsNullOrEmpty(ownerId)) return false;

        try
        {
            var resolved = await entitlements.GetAsync(ownerId, cancellationToken);
            return resolved.ScanToPay;
        }
        catch (Exception ex)
        {
            // Fails towards hiding it. Opposite default to branding, and deliberately so:
            // a lookup blip must not hand out a paid feature, and an absent QR still
            // leaves the client the bank details underneath it.
            logger?.LogWarning(ex, "Could not resolve scan-to-pay entitlement for {OwnerId}; hiding it.", ownerId);
            return false;
        }
    }
}
