using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Sanet.MakaMek.Avalonia.Views;
using Sanet.MakaMek.Avalonia.Views.JoinGame;
using Sanet.MakaMek.Avalonia.Views.StartNewGame;
using Shouldly;

namespace MakaMek.Avalonia.Tests;

public class ContainerQueryLayoutTests
{
    [Fact]
    public async Task AdaptiveGrid_Is_Wide_When_Container_Starts_Wide()
    {
        var session = HeadlessUnitTestSession.GetOrStartForAssembly(typeof(ContainerQueryLayoutTests).Assembly);

        await session.Dispatch(() =>
        {
            var probe = new AdaptiveGridProbe();
            var window = new Window
            {
                Width = 1000,
                Height = 600,
                Content = probe
            };
            window.Show();
            Dispatcher.UIThread.RunJobs();

            window.ClientSize.Width.ShouldBe(1000);
            probe.AdaptiveGrid.Rows.ShouldBe(1);
            probe.AdaptiveGrid.Columns.ShouldBe(2);
        }, CancellationToken.None);
    }

    [Fact]
    public async Task JoinGameViewWide_ShowsTwoColumns_When_Container_Is_Wide()
    {
        var session = HeadlessUnitTestSession.GetOrStartForAssembly(typeof(ContainerQueryLayoutTests).Assembly);

        await session.Dispatch(() =>
        {
            var view = new JoinGameViewWide();
            var window = new Window
            {
                Width = 1200,
                Height = 600,
                Content = view
            };
            window.Show();
            Dispatcher.UIThread.RunJobs();

            var grid = view.FindControl<UniformGrid>("JoinGameAdaptiveGrid");
            grid.ShouldNotBeNull();
            grid.Rows.ShouldBe(0);
            grid.Columns.ShouldBe(2);
        }, CancellationToken.None);
    }

    [Fact]
    public async Task JoinGameViewWide_ShowsTwoRows_When_Container_Is_Narrow()
    {
        var session = HeadlessUnitTestSession.GetOrStartForAssembly(typeof(ContainerQueryLayoutTests).Assembly);

        await session.Dispatch(() =>
        {
            var view = new JoinGameViewWide();
            var window = new Window
            {
                Width = 500,
                Height = 600,
                Content = view
            };
            window.Show();
            Dispatcher.UIThread.RunJobs();

            var grid = view.FindControl<UniformGrid>("JoinGameAdaptiveGrid");
            grid.ShouldNotBeNull();
            grid.Rows.ShouldBe(2);
            grid.Columns.ShouldBe(0);
        }, CancellationToken.None);
    }

    [Fact]
    public async Task StartNewGameViewWide_ShowsTwoColumns_When_Container_Is_Wide()
    {
        var session = HeadlessUnitTestSession.GetOrStartForAssembly(typeof(ContainerQueryLayoutTests).Assembly);

        await session.Dispatch(() =>
        {
            var view = new StartNewGameViewWide();
            var window = new Window
            {
                Width = 1200,
                Height = 600,
                Content = view
            };
            window.Show();
            Dispatcher.UIThread.RunJobs();

            var grid = view.FindControl<UniformGrid>("StartNewGameAdaptiveGrid");
            grid.ShouldNotBeNull();
            grid.Rows.ShouldBe(1);
            grid.Columns.ShouldBe(2);
        }, CancellationToken.None);
    }

    [Fact]
    public async Task StartNewGameViewWide_ShowsTwoRows_When_Container_Is_Narrow()
    {
        var session = HeadlessUnitTestSession.GetOrStartForAssembly(typeof(ContainerQueryLayoutTests).Assembly);

        await session.Dispatch(() =>
        {
            var view = new StartNewGameViewWide();
            var window = new Window
            {
                Width = 500,
                Height = 600,
                Content = view
            };
            window.Show();
            Dispatcher.UIThread.RunJobs();

            var grid = view.FindControl<UniformGrid>("StartNewGameAdaptiveGrid");
            grid.ShouldNotBeNull();
            grid.Rows.ShouldBe(2);
            grid.Columns.ShouldBe(1);
        }, CancellationToken.None);
    }

    [Fact]
    public async Task BattleMapView_Uses_CompactStatusPadding_When_Container_Is_Narrow()
    {
        var session = HeadlessUnitTestSession.GetOrStartForAssembly(typeof(ContainerQueryLayoutTests).Assembly);

        await session.Dispatch(() =>
        {
            var view = new BattleMapView();
            var window = new Window
            {
                Width = 400,
                Height = 600,
                Content = view
            };
            window.Show();
            Dispatcher.UIThread.RunJobs();

            var section = view.FindControl<Grid>("TurnInfoPanel")?
                .GetVisualDescendants().OfType<Border>().FirstOrDefault(b => b.Classes.Contains("statusSection"));
            section.ShouldNotBeNull();
            section.Padding.ShouldBe(new Thickness(6, 3));

            window.Width = 1000;
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();

            section.Padding.ShouldBe(new Thickness(10, 5));
        }, CancellationToken.None);
    }

    [Fact]
    public async Task BattleMapView_Uses_DenseTurnText_When_Container_Is_Narrow()
    {
        var session = HeadlessUnitTestSession.GetOrStartForAssembly(typeof(ContainerQueryLayoutTests).Assembly);

        await session.Dispatch(() =>
        {
            var view = new BattleMapView();
            var window = new Window
            {
                Width = 400,
                Height = 600,
                Content = view
            };
            window.Show();
            Dispatcher.UIThread.RunJobs();

            var label = view.FindControl<Grid>("TurnInfoPanel")?
                .GetVisualDescendants().OfType<TextBlock>().FirstOrDefault(b => b.Classes.Contains("turnInfo"));
            label.ShouldNotBeNull();
            label.FontSize.ShouldBe(11);

            window.Width = 1000;
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();

            label.FontSize.ShouldBe(14);
        }, CancellationToken.None);
    }
}