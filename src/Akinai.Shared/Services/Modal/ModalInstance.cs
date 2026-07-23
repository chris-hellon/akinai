namespace Akinai.Shared.Services.Modal;

/// <summary>
/// Represents an active modal instance.
/// </summary>
public class ModalInstance
{
    public required Type ComponentType { get; init; }
    public required string Title { get; init; }
    public Dictionary<string, object> Parameters { get; init; } = new();
    public ModalOptions Options { get; init; } = new();
    public TaskCompletionSource<object?> TaskCompletionSource { get; init; } = new();
}
