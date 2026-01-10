using Microsoft.Extensions.DependencyInjection;
using Sanet.MakaMek.Avalonia.Android.Services;
using Sanet.MakaMek.Core.Services;
using Sanet.MakaMek.Core.Services.Logging.Factories;
using Sanet.MakaMek.Core.Services.Transport;
using Microsoft.Extensions.Logging;

namespace Sanet.MakaMek.Avalonia.Android.DependencyInjection;

public static class AndroidServices
{
    public static void RegisterAndroidServices(this IServiceCollection services)
    {
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });
        // Register the dummy network host service for Android
        services.AddSingleton<INetworkHostService, DummyNetworkHostService>();

        // Register console-based command logger for Android platform
        services.AddSingleton<ICommandLoggerFactory, ConsoleCommandLoggerFactory>();

        // Register file system caching service for Android platform
        services.AddSingleton<IFileCachingService, FileSystemCachingService>();

        // Register external navigation service for Android platform
        services.AddSingleton<IExternalNavigationService, AndroidExternalNavigationService>();
    }
}
