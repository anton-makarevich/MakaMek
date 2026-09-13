using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Threading;
using Shouldly;

namespace MakaMek.Avalonia.Tests;

public class ContainerQueryLayoutTests
{
    [Fact]
    public async Task AdaptiveGrid_Is_Wide_When_Container_Starts_Wide()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(TestApp));

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
    public async Task AdaptiveGrid_Reflows_From_Narrow_To_Wide()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(TestApp));

        await session.Dispatch(() =>
        {
            var probe = new AdaptiveGridProbe();
            var window = new Window
            {
                Width = 400,
                Height = 600,
                Content = probe
            };
            window.Show();
            Dispatcher.UIThread.RunJobs();

            window.ClientSize.Width.ShouldBe(400);
            probe.AdaptiveGrid.Rows.ShouldBe(2);
            probe.AdaptiveGrid.Columns.ShouldBe(1);

            window.Width = 1000;
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();

            window.ClientSize.Width.ShouldBe(1000);
            probe.AdaptiveGrid.Rows.ShouldBe(1);
            probe.AdaptiveGrid.Columns.ShouldBe(2);
        }, CancellationToken.None);
    }
}