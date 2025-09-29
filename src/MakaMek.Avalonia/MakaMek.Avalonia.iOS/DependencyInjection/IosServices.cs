using Microsoft.Extensions.DependencyInjection;
using Sanet.MakaMek.Core.Services;
using Sanet.MakaMek.Core.Services.Logging.Factories;
using Sanet.MakaMek.Core.Services.Transport;

namespace Sanet.MakaMek.Avalonia.iOS.DependencyInjection;

public static class IosServices
{
    public static void RegisterIosServices(this IServiceCollection services)
    {
        // Register the dummy network host service for iOS
        services.AddSingleton<INetworkHostService, DummyNetworkHostService>();

        // Register console-based command logger for iOS platform
        services.AddSingleton<ICommandLoggerFactory, ConsoleCommandLoggerFactory>();

        // Register file system caching service for iOS platform
        services.AddSingleton<IFileCachingService, FileSystemCachingService>();
    }
}
