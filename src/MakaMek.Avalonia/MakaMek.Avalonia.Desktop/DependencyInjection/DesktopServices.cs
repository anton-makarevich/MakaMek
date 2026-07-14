using System.Reactive.Concurrency;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sanet.MakaMek.Avalonia.Desktop.Services;
using Sanet.MakaMek.Core.Services;
using Sanet.MakaMek.Core.Services.Logging.Factories;
using Sanet.MVVM.ExternalNavigation.Desktop.Extensions;
using Sanet.MakaMek.Core.Services.Transport;
using Sanet.MakaMek.Services;

namespace Sanet.MakaMek.Avalonia.Desktop.DependencyInjection;

public static class DesktopServices
{
    public static void RegisterDesktopServices(this IServiceCollection services)
    {
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(
#if DEBUG
                LogLevel.Debug
#else
                LogLevel.Information
#endif
                );
        });
        // Register the SignalR host service for desktop platforms
        services.AddSingleton<INetworkHostService, SignalRHostService>();

        // Register file-based command logger for desktop platform
        services.AddSingleton<ICommandLoggerFactory, FileCommandLoggerFactory>();

        // Register file system caching service for desktop platform
        services.AddSingleton<IFileCachingService, FileSystemCachingService>();

        // Register external navigation service for desktop platform
        services.AddDesktopExternalNavigation();

        // Register TaskPoolScheduler for desktop (multi-threaded)
        services.AddSingleton<IScheduler>(TaskPoolScheduler.Default);
    }
}
