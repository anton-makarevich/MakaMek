using Microsoft.Extensions.DependencyInjection;
using Sanet.MakaMek.Core.Services.Logging.Factories;
using Sanet.MakaMek.Core.Services.Transport;

namespace Sanet.MakaMek.Avalonia.Android.DependencyInjection;

public static class AndroidServices
{
    public static void RegisterAndroidServices(this IServiceCollection services)
    {
        // Register the dummy network host service for Android
        services.AddSingleton<INetworkHostService, DummyNetworkHostService>();

        // Register console-based command logger for Android platform
        services.AddSingleton<ICommandLoggerFactory, ConsoleCommandLoggerFactory>();
    }
}
