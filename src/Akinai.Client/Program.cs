using ApexCharts;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Client;
using Akinai.Client.Services;
using Akinai.Shared.Api.Clients;
using Akinai.Shared.Services.Modal;

namespace Akinai.Client;

class Program
{
    static async Task Main(string[] args)
    {
        var builder = WebAssemblyHostBuilder.CreateDefault(args);

        // Register HttpClient for API calls
        builder.Services.AddScoped(_ => new HttpClient
        {
            BaseAddress = new Uri(builder.HostEnvironment.BaseAddress)
        });

        // Register EasyApi client with contracts from shared assembly
        builder.Services.AddEasyApi()
            .WithContract(typeof(GetClients).Assembly)
            .WithClient();

        // Register Modal Service
        builder.Services.AddScoped<IModalService, ModalService>();

        // Register Layout Service for JS interop
        builder.Services.AddScoped<ILayoutService, LayoutService>();

        // Notifications state (loads + live SignalR updates)
        // Running in the browser: realtime comes over SignalR.
        builder.Services.AddScoped<IRealtimeUserChannel, SignalRUserChannel>();
        builder.Services.AddScoped<NotificationState>();

        // Dashboard reporting-currency selection (shared by header + dashboard)
        builder.Services.AddScoped<ReportingCurrencyState>();

        builder.Services.AddScoped<TimeTrackingState>();

        // Entitlements, so pages can offer the right features without each
        // one asking the server separately.
        builder.Services.AddScoped<EntitlementState>();

        // Interactive charts (ApexCharts)
        builder.Services.AddApexCharts();

        builder.Services.AddAuthorizationCore();
        builder.Services.AddCascadingAuthenticationState();
        builder.Services.AddAuthenticationStateDeserialization();

        await builder.Build().RunAsync();
    }
}
