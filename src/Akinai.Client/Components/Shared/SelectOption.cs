namespace Akinai.Client.Components.Shared;

/// <summary>
/// A single option for the <see cref="Select{TValue}"/> component.
/// </summary>
public sealed record SelectOption<TValue>(
    TValue Value,
    string Label,
    string? Description = null,
    bool Disabled = false);
