using System.Runtime.Versioning;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Browser;
using MonacoLabs.Presto;
using Sanet.MakaMek.Avalonia.Browser.DependencyInjection;
using Sanet.MVVM.DI.Avalonia.Extensions;

[assembly: SupportedOSPlatform("browser")]

namespace Sanet.MakaMek.Avalonia.Browser;

internal sealed partial class Program
{
    private static Task Main(string[] args) => BuildAvaloniaApp()
        .WithInterFont()
        .UseDependencyInjection(services=>services.RegisterBrowserServices())
        .StartBrowserAppAsync("out");

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePresto();
}