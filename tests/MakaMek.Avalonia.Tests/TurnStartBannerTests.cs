using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Sanet.MakaMek.Avalonia.Controls;
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
            var (_, view) = ShowBattleMap();
            var banner = view.FindControl<Border>("TurnStartBanner");

            banner.ShouldNotBeNull();
            banner.Opacity.ShouldBe(0);
            banner.IsHitTestVisible.ShouldBeFalse();
        }, CancellationToken.None);
    }

    [Fact]
    public async Task TurnStartBanner_DoesNotInterceptMapClicks_WhileVisible()
    {
        var session = HeadlessUnitTestSession.GetOrStartForAssembly(typeof(TurnStartBannerTests).Assembly);

        await session.Dispatch(() =>
        {
            var (window, view) = ShowBattleMap();
            var banner = view.FindControl<Border>("TurnStartBanner")!;
            var map = view.FindControl<HexMap>("MapCanvas")!;
            var clicks = 0;
            map.ContentClicked += (_, _) => clicks++;

            // Mid-announcement: the banner is fully opaque and covers this point. Its background
            // comes from the turnStartBanner style rather than a binding, so it is a solid surface
            // even without a view model - which matters, because a Border with no background is not
            // hit-testable at all and would make this assertion pass without proving anything.
            banner.Background.ShouldNotBeNull();
            banner.Opacity = 1;
            Settle(window);
            var overBanner = banner.Bounds.Center;

            Click(window, overBanner);

            clicks.ShouldBe(1, "a click over the visible banner must still reach the map");
        }, CancellationToken.None);
    }

    [Fact]
    public async Task StatusBar_DoesInterceptMapClicks()
    {
        // Negative control for the test above: the status bar is hit-testable, so a click on it
        // must NOT reach the map. Without this, a pass-through assertion could pass vacuously.
        var session = HeadlessUnitTestSession.GetOrStartForAssembly(typeof(TurnStartBannerTests).Assembly);

        await session.Dispatch(() =>
        {
            var (window, view) = ShowBattleMap();
            var statusBar = view.FindControl<Grid>("TurnStatus")!;
            var map = view.FindControl<HexMap>("MapCanvas")!;
            var clicks = 0;
            map.ContentClicked += (_, _) => clicks++;

            Click(window, statusBar.Bounds.Center);

            clicks.ShouldBe(0, "the status bar is interactive and must absorb the click");
        }, CancellationToken.None);
    }

    private static (Window Window, BattleMapView View) ShowBattleMap()
    {
        var view = new BattleMapView();
        var window = new Window
        {
            Width = 1100,
            Height = 700,
            Content = view
        };
        window.Show();
        Settle(window);
        return (window, view);
    }

    private static void Click(Window window, Point point)
    {
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>
    /// Runs layout to completion. Showing a window does not guarantee it, and these assertions are
    /// all about where the banner ended up, so stale bounds would make them depend on what ran
    /// first.
    /// </summary>
    private static void Settle(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
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
