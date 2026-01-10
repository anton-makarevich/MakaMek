using Microsoft.Extensions.DependencyInjection;
using Sanet.MakaMek.Avalonia.iOS.Services;
using Sanet.MakaMek.Core.Services;
using Sanet.MakaMek.Core.Services.Logging.Factories;
using Sanet.MakaMek.Core.Services.Transport;
using Microsoft.Extensions.Logging;

namespace Sanet.MakaMek.Avalonia.iOS.DependencyInjection;

public static class IosServices
{
    public static void RegisterIosServices(this IServiceCollection services)
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
        // Register the dummy network host service for iOS
        services.AddSingleton<INetworkHostService, DummyNetworkHostService>();

        // Register console-based command logger for iOS platform
        services.AddSingleton<ICommandLoggerFactory, ConsoleCommandLoggerFactory>();

        // Register file system caching service for iOS platform
        services.AddSingleton<IFileCachingService, FileSystemCachingService>();

        // Register external navigation service for iOS platform
        services.AddSingleton<IExternalNavigationService, IosExternalNavigationService>();
    }
}
