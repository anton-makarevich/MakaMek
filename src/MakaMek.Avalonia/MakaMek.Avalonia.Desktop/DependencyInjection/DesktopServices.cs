using Microsoft.Extensions.DependencyInjection;
using Sanet.MakaMek.Avalonia.Desktop.Services;
using Sanet.MakaMek.Core.Services.Logging.Factories;
using Sanet.MakaMek.Core.Services.Transport;

namespace Sanet.MakaMek.Avalonia.Desktop.DependencyInjection;

public static class DesktopServices
{
    public static void RegisterDesktopServices(this IServiceCollection services)
    {
        // Register the SignalR host service for desktop platforms
        services.AddSingleton<INetworkHostService, SignalRHostService>();

        // Register file-based command logger for desktop platform
        services.AddSingleton<ICommandLoggerFactory, FileCommandLoggerFactory>();
    }
}
