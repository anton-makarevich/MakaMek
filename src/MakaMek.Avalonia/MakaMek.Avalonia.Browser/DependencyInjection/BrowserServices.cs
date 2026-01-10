using Microsoft.Extensions.DependencyInjection;
using Sanet.MakaMek.Avalonia.Browser.Services;
using Sanet.MakaMek.Core.Services;
using Sanet.MakaMek.Core.Services.Logging.Factories;
using Sanet.MakaMek.Core.Services.Transport;
using Microsoft.Extensions.Logging;

namespace Sanet.MakaMek.Avalonia.Browser.DependencyInjection;

public static class BrowserServices
{
    public static void RegisterBrowserServices(this IServiceCollection services)
    {
        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(
#if DEBUG
                LogLevel.Debug
#else
                LogLevel.Information
#endif
                );
        });
        // Register the dummy network host service for Browser (WASM)
        services.AddSingleton<INetworkHostService, DummyNetworkHostService>();

        // Register console-based command logger for WASM platform
        services.AddSingleton<ICommandLoggerFactory, ConsoleCommandLoggerFactory>();

        // Register browser caching service for WASM platform
        services.AddSingleton<IFileCachingService, BrowserCachingService>();

        // Register external navigation service for browser platform
        services.AddSingleton<IExternalNavigationService, BrowserExternalNavigationService>();
    }
}
