using Avalonia;
using Avalonia.Headless;

[assembly: AvaloniaTestApplication(typeof(MakaMek.Avalonia.Tests.HeadlessTestSetup))]

namespace MakaMek.Avalonia.Tests;

public static class HeadlessTestSetup
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<TestApp>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions());
}
