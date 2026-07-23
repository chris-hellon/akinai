namespace Akinai.Shared.Services.Modal;

/// <summary>
/// Default implementation of IModalService.
/// </summary>
public class ModalService : IModalService
{
    public event Action? OnChange;

    public ModalInstance? ActiveModal { get; private set; }

    public Task<TResult?> ShowAsync<TResult>(Type modalType, string title, Dictionary<string, object>? parameters = null, ModalOptions? options = null)
    {
        var tcs = new TaskCompletionSource<object?>();

        ActiveModal = new ModalInstance
        {
            ComponentType = modalType,
            Title = title,
            Parameters = parameters ?? new Dictionary<string, object>(),
            Options = options ?? new ModalOptions(),
            TaskCompletionSource = tcs
        };

        OnChange?.Invoke();

        return tcs.Task.ContinueWith(t =>
        {
            if (t.Result is TResult result)
                return result;
            return default;
        });
    }

    public Task ShowAsync(Type modalType, string title, Dictionary<string, object>? parameters = null, ModalOptions? options = null)
    {
        return ShowAsync<object>(modalType, title, parameters, options);
    }

    public void Close<TResult>(TResult result)
    {
        var modal = ActiveModal;
        ActiveModal = null;
        OnChange?.Invoke();
        modal?.TaskCompletionSource.TrySetResult(result);
    }

    public void Cancel()
    {
        var modal = ActiveModal;
        ActiveModal = null;
        OnChange?.Invoke();
        modal?.TaskCompletionSource.TrySetResult(null);
    }
}