using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Headless;
using global::Avalonia.Threading;
using global::Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using Sanet.MakaMek.Avalonia;
using Sanet.MakaMek.Avalonia.Controls.TemplatedControls;
using Sanet.MakaMek.Avalonia.Views;
using Sanet.MakaMek.Presentation.ViewModels;
using Sanet.MakaMek.Presentation.ViewModels.Wrappers;
using Shouldly;

namespace MakaMek.Avalonia.AppTests;

/// <summary>
/// The turn notification banner, rendered inside the real BattleMapView with a view model from the
/// real service graph.
///
/// The existing tests cover the two ends and not the join: TurnNotificationBannerTests builds the
/// control standalone, and BattleMapViewModelTests never renders anything. Nothing checks that
/// BattleMapView actually binds TurnNotifications to the banner, which is the step that decides
/// whether an announcement reaches a player at all.
/// </summary>
public class BannerRenderingTests
{
    [Fact]
    public Task BattleMapView_BindsItsNotificationQueue_ToTheBanner() => HarnessSession.Run(() =>
    {
        var (window, view, viewModel) = ShowBattleMap();
        try
        {
            var banner = Banner(view);
            banner.ShouldNotBeNull("BattleMapView should contain a TurnNotificationBanner");

            viewModel.TurnNotifications.Add(new TurnNotification(
                TurnNotificationKind.Turn, "TURN 1", "#FF0000"));
            Settle(window);

            banner.Notifications.ShouldNotBeNull("the Notifications binding should have resolved");
            banner.Notifications.ShouldContain(n => n.Text == "TURN 1",
                "a silently broken binding path would leave this empty and never show an announcement");
        }
        finally
        {
            window.Close();
        }
    });

    [Fact]
    public Task Banner_RendersEachAnnouncement_InQueueOrder() => HarnessSession.Run(() =>
    {
        var (window, view, viewModel) = ShowBattleMap();
        try
        {
            var banner = Banner(view)!;
            var rendered = new List<string>();

            foreach (var text in new[] { "TURN 1", "INITIATIVE", "WINNER WON WITH 10", "MOVEMENT" })
                viewModel.TurnNotifications.Add(new TurnNotification(
                    TurnNotificationKind.Turn, text, "#FFFFFF"));

            // Drain the queue the way the control does, recording what was actually put on screen.
            for (var i = 0; i < 40 && viewModel.TurnNotifications.Count > 0; i++)
            {
                Settle(window);
                if (!string.IsNullOrEmpty(banner.CurrentText) &&
                    (rendered.Count == 0 || rendered[^1] != banner.CurrentText))
                    rendered.Add(banner.CurrentText);
            }

            rendered.ShouldNotBeEmpty("nothing was ever displayed, so no announcement reaches a player");
            rendered[0].ShouldBe("TURN 1", $"displayed in this order: {string.Join(" -> ", rendered)}");
        }
        finally
        {
            window.Close();
        }
    });

    private static TurnNotificationBanner? Banner(BattleMapView view) =>
        view.GetVisualDescendants().OfType<TurnNotificationBanner>().FirstOrDefault();

    private static (Window Window, BattleMapView View, BattleMapViewModel ViewModel) ShowBattleMap()
    {
        var services = ((App)Application.Current!).ServiceProvider!;
        var viewModel = services.GetRequiredService<BattleMapViewModel>();
        var view = new BattleMapView { DataContext = viewModel };
        var window = new Window { Width = 1100, Height = 700, Content = view };
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

}
