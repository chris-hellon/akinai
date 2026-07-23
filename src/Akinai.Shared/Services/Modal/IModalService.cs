namespace Akinai.Shared.Services.Modal;

/// <summary>
/// Service for managing modal dialogs throughout the application.
/// </summary>
public interface IModalService
{
    /// <summary>
    /// Event fired when the modal state changes.
    /// </summary>
    event Action? OnChange;

    /// <summary>
    /// Gets the currently active modal, if any.
    /// </summary>
    ModalInstance? ActiveModal { get; }

    /// <summary>
    /// Shows a modal of the specified type with optional parameters.
    /// </summary>
    /// <typeparam name="TResult">The type of result the modal returns.</typeparam>
    /// <param name="modalType">The type of the modal component.</param>
    /// <param name="title">The modal title.</param>
    /// <param name="parameters">Optional parameters to pass to the modal.</param>
    /// <param name="options">Optional modal display options.</param>
    /// <returns>A task that completes with the modal result, or null if cancelled.</returns>
    Task<TResult?> ShowAsync<TResult>(Type modalType, string title, Dictionary<string, object>? parameters = null, ModalOptions? options = null);

    /// <summary>
    /// Shows a modal that doesn't return a result.
    /// </summary>
    Task ShowAsync(Type modalType, string title, Dictionary<string, object>? parameters = null, ModalOptions? options = null);

    /// <summary>
    /// Closes the current modal with a result.
    /// </summary>
    void Close<TResult>(TResult result);

    /// <summary>
    /// Closes the current modal without a result (cancelled).
    /// </summary>
    void Cancel();
}
