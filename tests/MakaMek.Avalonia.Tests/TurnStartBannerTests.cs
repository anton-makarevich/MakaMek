using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Sanet.MakaMek.Avalonia.Services;
using Sanet.MakaMek.Avalonia.Views;
using Shouldly;

namespace MakaMek.Avalonia.Tests;

/// <summary>
/// The turn-start banner replaces the status-bar turn label: it announces the local player's turn
/// and then disappears, so at rest it must be invisible and must not intercept map input.
/// </summary>
public class TurnStartBannerTests
{
    [Fact]
    public async Task TurnStartBanner_IsInvisibleAndNonInteractive_AtRest()
    {
        var session = HeadlessUnitTestSession.GetOrStartForAssembly(typeof(TurnStartBannerTests).Assembly);

        await session.Dispatch(() =>
        {
            var view = new BattleMapView();
            var window = new Window
            {
                Width = 1000,
                Height = 600,
                Content = view
            };
            window.Show();
            Dispatcher.UIThread.RunJobs();

            var banner = view.FindControl<Border>("TurnStartBanner");

            banner.ShouldNotBeNull();
            banner.Opacity.ShouldBe(0);
            banner.IsHitTestVisible.ShouldBeFalse();
        }, CancellationToken.None);
    }

    [Fact]
    public async Task TurnStartAnimation_ResourceIsAvailable()
    {
        // The view falls back to a plain delay when the resource is missing, which would silently
        // downgrade the announcement to a static label. Assert the resource actually resolves.
        var session = HeadlessUnitTestSession.GetOrStartForAssembly(typeof(TurnStartBannerTests).Assembly);

        await session.Dispatch(() =>
        {
            var resourcesLocator = new AvaloniaResourcesLocator();

            resourcesLocator.TryFindResource("TurnStartAnimation").ShouldBeOfType<Animation>();
        }, CancellationToken.None);
    }
}
