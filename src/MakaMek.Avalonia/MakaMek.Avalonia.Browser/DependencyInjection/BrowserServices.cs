using Microsoft.Extensions.DependencyInjection;
using Sanet.MakaMek.Core.Services.Logging.Factories;
using Sanet.MakaMek.Core.Services.Transport;

namespace Sanet.MakaMek.Avalonia.Browser.DependencyInjection;

public static class BrowserServices
{
    public static void RegisterBrowserServices(this IServiceCollection services)
    {
        // Register the dummy network host service for Browser (WASM)
        services.AddSingleton<INetworkHostService, DummyNetworkHostService>();

        // Register console-based command logger for WASM platform
        services.AddSingleton<ICommandLoggerFactory, ConsoleCommandLoggerFactory>();
    }
}
