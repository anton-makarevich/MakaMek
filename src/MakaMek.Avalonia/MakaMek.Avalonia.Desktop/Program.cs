using System;
using Avalonia;
using Sanet.MakaMek.Avalonia.Desktop.DependencyInjection;
using Sanet.MVVM.DI.Avalonia.Extensions;
using Velopack;
#if DEBUG
using AvaloniaUI.DiagnosticsSupport;
#endif

namespace Sanet.MakaMek.Avalonia.Desktop;

sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        VelopackApp.Build().Run();
        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .UseDependencyInjection(services=>services.RegisterDesktopServices())
            .WithInterFont()
#if DEBUG
            .WithDeveloperTools()
#endif
            .LogToTrace();
}