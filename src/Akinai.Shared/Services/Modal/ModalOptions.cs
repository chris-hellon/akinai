namespace Akinai.Shared.Services.Modal;

/// <summary>
/// Options for configuring modal display.
/// </summary>
public record ModalOptions
{
    public ModalSize Size { get; init; } = ModalSize.Medium;
    public bool CloseOnBackdropClick { get; init; } = true;
    public bool ShowCloseButton { get; init; } = true;
}
