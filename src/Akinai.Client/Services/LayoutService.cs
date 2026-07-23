using Microsoft.JSInterop;

namespace Akinai.Client.Services;

public interface ILayoutService
{
    ValueTask ToggleMobileSidebarAsync();
    ValueTask<bool> ToggleSidebarCollapseAsync();
    ValueTask<bool> ToggleDarkModeAsync();
    ValueTask<bool> GetDarkModeAsync();
    ValueTask SetDarkModeAsync(bool isDark);
    ValueTask<bool> GetSidebarCollapsedAsync();
    ValueTask<bool> RestoreSidebarStateAsync();
    ValueTask InitializeAsync();
}

public class LayoutService(IJSRuntime jsRuntime) : ILayoutService, IAsyncDisposable
{
    private IJSObjectReference? _module;
    private bool _initialized;

    private async ValueTask EnsureModuleLoadedAsync()
    {
        _module ??= await jsRuntime.InvokeAsync<IJSObjectReference>(
            "import", "./js/layout.js");
    }

    public async ValueTask InitializeAsync()
    {
        if (_initialized) return;

        await EnsureModuleLoadedAsync();

        // Restore sidebar state
        await RestoreSidebarStateAsync();

        _initialized = true;
    }

    public async ValueTask ToggleMobileSidebarAsync()
    {
        await EnsureModuleLoadedAsync();
        await _module!.InvokeVoidAsync("toggleMobileSidebar");
    }

    public async ValueTask<bool> ToggleSidebarCollapseAsync()
    {
        await EnsureModuleLoadedAsync();
        return await _module!.InvokeAsync<bool>("toggleSidebarCollapse");
    }

    public async ValueTask<bool> ToggleDarkModeAsync()
    {
        await EnsureModuleLoadedAsync();
        return await _module!.InvokeAsync<bool>("toggleDarkMode");
    }

    public async ValueTask<bool> GetDarkModeAsync()
    {
        await EnsureModuleLoadedAsync();
        return await _module!.InvokeAsync<bool>("getDarkMode");
    }

    public async ValueTask SetDarkModeAsync(bool isDark)
    {
        await EnsureModuleLoadedAsync();
        await _module!.InvokeVoidAsync("setDarkMode", isDark);
    }

    public async ValueTask<bool> GetSidebarCollapsedAsync()
    {
        await EnsureModuleLoadedAsync();
        return await _module!.InvokeAsync<bool>("getSidebarCollapsed");
    }

    public async ValueTask<bool> RestoreSidebarStateAsync()
    {
        await EnsureModuleLoadedAsync();
        return await _module!.InvokeAsync<bool>("restoreSidebarState");
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            await _module.DisposeAsync();
        }
    }
}
