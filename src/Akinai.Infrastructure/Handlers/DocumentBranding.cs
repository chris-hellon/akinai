using Akinai.Shared.Services;
using Microsoft.Extensions.Logging;

namespace Akinai.Infrastructure.Handlers;

/// <summary>
/// Decides whether a client-facing document carries the "Created with Akinai"
/// line.
///
/// Shared by the public invoice and estimate handlers so the two cannot drift,
/// and deliberately answered here rather than in the renderer: the person
/// looking at a public document is an anonymous client with no plan of their
/// own, so the only account whose entitlements mean anything is the one that
/// issued it.
/// </summary>
internal static class DocumentBranding
{
    /// <summary>
    /// True when the branding line should be rendered — that is, when the
    /// issuing account's plan does not include RemoveBranding.
    /// </summary>
    /// <param name="ownerId">
    /// The account that issued the document, not the caller.
    /// </param>
    public static async Task<bool> ShowForAsync(
        IEntitlementService entitlements,
        string? ownerId,
        CancellationToken cancellationToken,
        ILogger? logger = null)
    {
        if (string.IsNullOrEmpty(ownerId))
            return true;

        try
        {
            var resolved = await entitlements.GetAsync(ownerId, cancellationToken);
            return !resolved.RemoveBranding;
        }
        catch (Exception ex)
        {
            // Fails towards showing branding. The alternative — a lookup blip
            // silently handing out a paid perk — is the wrong way to be wrong,
            // and this must never take the document down with it.
            logger?.LogWarning(ex, "Could not resolve branding entitlement for {OwnerId}; showing branding.", ownerId);
            return true;
        }
    }
}
