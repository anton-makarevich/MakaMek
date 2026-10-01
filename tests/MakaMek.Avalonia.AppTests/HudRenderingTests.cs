using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Headless;
using global::Avalonia.Media.Imaging;
using global::Avalonia.Threading;
using global::Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using Sanet.MakaMek.Avalonia;
using global::Avalonia.Automation;
using Sanet.MakaMek.Avalonia.Controls;
using Sanet.MakaMek.Avalonia.Controls.TemplatedControls;
using Sanet.MakaMek.Avalonia.Views;
using Sanet.MakaMek.Presentation.ViewModels;
using Sanet.MVVM.Core.Views;
using Shouldly;

namespace MakaMek.Avalonia.AppTests;

/// <summary>
/// The battle map HUD, rendered inside the real BattleMapView with a view model from the real
/// service graph.
///
/// The view models in #1533 and #1536 are covered by presentation tests that never render, and the
/// control tests build each control standalone. Neither side can see whether the view actually
/// binds to the properties, or whether the map hooks on the view model reach the map control.
/// </summary>
public class HudRenderingTests
{
    [Fact]
    public Task View_WiresTheMapHooks_OnTheViewModel() => HarnessSession.Run(() =>
    {
        var (window, _, viewModel) = ShowBattleMap();
        try
        {
            // Null here means ZoomInCommand and friends run and do nothing at all, which looks
            // exactly like a map that will not zoom.
            viewModel.ZoomIn.ShouldNotBeNull();
            viewModel.ZoomOut.ShouldNotBeNull();
            viewModel.FitMap.ShouldNotBeNull();
            viewModel.FocusUnit.ShouldNotBeNull();
            viewModel.CenterMap.ShouldNotBeNull();
        }
        finally
        {
            window.Close();
        }
    });

    [Fact]
    public Task MapCommands_RunWithoutThrowing_AgainstTheRealMapControl() => HarnessSession.Run(async () =>
    {
        var (window, _, viewModel) = ShowBattleMap();
        try
        {
            await viewModel.ZoomInCommand.ExecuteAsync();
            await viewModel.ZoomOutCommand.ExecuteAsync();
            await viewModel.FitMapCommand.ExecuteAsync();
            await viewModel.CenterMapCommand.ExecuteAsync();
            Settle(window);
        }
        finally
        {
            window.Close();
        }
    });

    [Fact]
    public Task MapControlsDrawer_IsClosedUntilToggled() => HarnessSession.Run(() =>
    {
        var (window, view, viewModel) = ShowBattleMap();
        try
        {
            viewModel.IsMapControlsDrawerOpen.ShouldBeFalse("the drawer starts closed");
            var fitButton = FitButton(view);
            fitButton.ShouldNotBeNull("the drawer and its buttons should be in the tree");
            fitButton.IsEffectivelyVisible.ShouldBeFalse("a closed drawer should show nothing");

            viewModel.ToggleMapControlsDrawer();
            Settle(window);

            fitButton.IsEffectivelyVisible.ShouldBeTrue("toggling should reveal the drawer");

            viewModel.ToggleMapControlsDrawer();
            Settle(window);

            fitButton.IsEffectivelyVisible.ShouldBeFalse("toggling again should put it away");
        }
        finally
        {
            window.Close();
        }
    });

    /// <summary>
    /// The value itself is driven by a rejected command, which needs a joined game; the presentation
    /// tests cover that. What is only checkable here is that the strip exists in the rendered tree
    /// and starts hidden, which is what a missing or mis-nested Border would break.
    /// </summary>
    [Fact]
    public Task CommandFeedbackStrip_IsInTheTree_AndHiddenAtRest() => HarnessSession.Run(() =>
    {
        var (window, view, viewModel) = ShowBattleMap();
        try
        {
            viewModel.IsCommandFeedbackVisible.ShouldBeFalse("nothing has been rejected yet");

            var status = view.GetVisualDescendants().OfType<Grid>()
                .FirstOrDefault(g => g.Name == "TurnStatus");
            status.ShouldNotBeNull();
            var strip = status!.Children.OfType<Border>()
                .FirstOrDefault(b => Grid.GetRow(b) == 1);
            strip.ShouldNotBeNull("the feedback strip from #1533 should sit under the turn bar");
            strip.IsVisible.ShouldBeFalse("it should stay out of the way until something is rejected");
        }
        finally
        {
            window.Close();
        }
    });

    [Fact]
    public Task SquadBar_IsHidden_WhenThereAreNoLocalUnits() => HarnessSession.Run(() =>
    {
        var (window, view, viewModel) = ShowBattleMap();
        try
        {
            viewModel.LocalUnits.ShouldBeEmpty("no game has been joined in this harness");
            viewModel.IsSquadStatusBarVisible.ShouldBeFalse();

            var cards = view.GetVisualDescendants().OfType<UnitStatusBarItem>().ToList();
            cards.ShouldBeEmpty("an empty squad should render no cards");
        }
        finally
        {
            window.Close();
        }
    });

    [Fact]
    public Task BattleMapView_RendersAFrame_WithTheHudInIt() => HarnessSession.Run(() =>
    {
        var (window, view, viewModel) = ShowBattleMap();
        try
        {
            viewModel.ToggleMapControlsDrawer();
            Settle(window);

            using var frame = window.CaptureRenderedFrame();
            frame.ShouldNotBeNull();
            // A surface is returned even when nothing is drawn, so count the pixels that vary.
            DistinctPixelCount(frame).ShouldBeGreaterThan(1,
                "a blank frame means the HUD was never actually drawn");

            Save(frame, "hud-desktop.png");
            view.ShouldNotBeNull();
        }
        finally
        {
            window.Close();
        }
    });

    [Fact]
    public Task BattleMapView_RendersAFrame_AtNarrowWidth() => HarnessSession.Run(() =>
    {
        var (window, _, viewModel) = ShowBattleMap(420, 780);
        try
        {
            viewModel.ToggleMapControlsDrawer();
            Settle(window);

            using var frame = window.CaptureRenderedFrame();
            frame.ShouldNotBeNull();
            DistinctPixelCount(frame).ShouldBeGreaterThan(1);

            Save(frame, "hud-narrow.png");
        }
        finally
        {
            window.Close();
        }
    });

    /// <summary>
    /// The fit control, found by the accessible name the real localization service resolves. The
    /// key itself is not usable here because the running application resolves it to display text.
    /// </summary>
    private static Control? FitButton(BattleMapView view) =>
        view.GetVisualDescendants()
            .OfType<ActionButton>()
            .FirstOrDefault(b => AutomationProperties.GetName(b) == "Fit map to view");

    private static (Window Window, BattleMapView View, BattleMapViewModel ViewModel) ShowBattleMap(
        int width = 1100,
        int height = 700)
    {
        var services = ((App)Application.Current!).ServiceProvider!;
        var viewModel = services.GetRequiredService<BattleMapViewModel>();
        var view = new BattleMapView();
        ((IBaseView)view).ViewModel = viewModel;
        view.DataContext = viewModel;
        var window = new Window { Width = width, Height = height, Content = view };
        window.Show();
        Settle(window);
        return (window, view, viewModel);
    }

    private static void Settle(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>
    /// Writes a captured frame next to the test binaries so a human can look at it. Screenshots are
    /// evidence for review, not an assertion; nothing here depends on the file.
    /// </summary>
    private static void Save(WriteableBitmap frame, string fileName)
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "screenshots");
        Directory.CreateDirectory(directory);
        frame.Save(Path.Combine(directory, fileName));
    }

    private static int DistinctPixelCount(WriteableBitmap frame)
    {
        using var buffer = frame.Lock();
        var pixels = new byte[buffer.RowBytes * buffer.Size.Height];
        System.Runtime.InteropServices.Marshal.Copy(buffer.Address, pixels, 0, pixels.Length);
        var seen = new HashSet<uint>();
        for (var i = 0; i + 3 < pixels.Length; i += 4)
            seen.Add(BitConverter.ToUInt32(pixels, i));
        return seen.Count;
    }

    /// <summary>
    /// Every control in the map drawer is the same tap target. The zoom buttons were text buttons
    /// sized to their glyph, which made "+" 28x27, "-" 25x27, and both smaller than the 40x40 icon
    /// buttons they sit between.
    /// </summary>
    [Fact]
    public Task MapControlsDrawer_UsesOneButtonSize() => HarnessSession.Run(() =>
    {
        var (window, view, viewModel) = ShowBattleMap();
        try
        {
            viewModel.ToggleMapControlsDrawer();
            Settle(window);

            var sizes = view.GetVisualDescendants()
                .OfType<global::Avalonia.Controls.Shapes.Path>()
                .Select(icon => icon.FindAncestorOfType<Button>())
                .Where(button => button is { IsEffectivelyVisible: true })
                .Select(button => button!.Bounds.Size)
                .Distinct()
                .ToList();

            sizes.ShouldNotBeEmpty("the drawer should have rendered its icon buttons");
            sizes.Count.ShouldBe(1,
                $"icon buttons should all be one size, found {string.Join(", ", sizes)}");
            sizes[0].Width.ShouldBe(sizes[0].Height, "and that size should be square");
        }
        finally
        {
            window.Close();
        }
    });



    /// <summary>
    /// The drawer and the toggle that opens it form one column. They did not: children stretch to
    /// the panel's widest item, so each icon drew against the left edge of a 145pt box at x=943
    /// while the toggle sat at x=1052.
    /// </summary>
    [Fact]
    public Task MapControlsDrawer_LinesUpWithItsToggle() => HarnessSession.Run(() =>
    {
        var (window, view, viewModel) = ShowBattleMap();
        try
        {
            viewModel.ToggleMapControlsDrawer();
            Settle(window);

            var lefts = view.GetVisualDescendants()
                .OfType<ActionButton>()
                .Where(b => b.IsEffectivelyVisible && b.Bounds.Width > 0)
                .Select(b => b.TranslatePoint(new Point(0, 0), view)?.X)
                .Where(x => x is not null)
                // The leave-game button belongs to the top bar, not this column.
                .Where(x => x > 100)
                .Distinct()
                .ToList();

            lefts.Count.ShouldBe(1,
                $"every map control should share one left edge, found {string.Join(", ", lefts)}");
        }
        finally
        {
            window.Close();
        }
    });
}
