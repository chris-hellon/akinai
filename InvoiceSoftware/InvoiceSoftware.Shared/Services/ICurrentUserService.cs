namespace InvoiceSoftware.Shared.Services;

public interface ICurrentUserService
{
    string? UserId { get; }
    string? UserName { get; }
    string? Email { get; }
    bool IsAuthenticated { get; }

    /// <summary>True if the signed-in user is in <paramref name="role"/>.
    /// Used for server-side admin checks — never trust the UI for this.</summary>
    bool IsInRole(string role);
}
