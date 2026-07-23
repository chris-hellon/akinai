using Akinai.Shared.Billing;

namespace Akinai.Shared.Dtos.Admin;

/// <summary>
/// A registered user as seen from the admin area.
///
/// Deliberately carries no security material — no password hash, security
/// stamp, or tokens — so an admin screen can never leak credentials.
/// </summary>
public record AdminUserDto(
    string Id,
    string Email,
    bool EmailConfirmed,
    bool TwoFactorEnabled,
    bool IsAdmin,
    bool IsLockedOut,
    DateTimeOffset? LockoutEnd,
    int ClientCount,
    int InvoiceCount,
    // Whether this row is the signed-in admin. Decided on the server: the
    // browser only receives the name and role claims, so the client cannot
    // identify itself without widening what gets serialised to it.
    bool IsCurrentUser,
    // The effective plan, not the stored one — a lapsed account shows as Free.
    BillingPlan Plan,
    string PlanName,
    SubscriptionStatus SubscriptionStatus,
    DateTime? CurrentPeriodEndUtc);

/// <summary>Per-user detail, including the activity that shows whether the
/// account is actually being used.</summary>
public record AdminUserDetailDto(
    string Id,
    string Email,
    bool EmailConfirmed,
    bool TwoFactorEnabled,
    bool IsAdmin,
    bool IsLockedOut,
    DateTimeOffset? LockoutEnd,
    int ClientCount,
    int ProjectCount,
    int InvoiceCount,
    int TimeEntryCount,
    decimal TotalInvoiced,
    decimal TotalPaid,
    string? Currency,
    DateTime? FirstInvoiceDate,
    DateTime? LastInvoiceDate);
