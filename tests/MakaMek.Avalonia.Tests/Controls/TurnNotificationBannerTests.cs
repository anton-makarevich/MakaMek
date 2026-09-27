using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AsyncAwaitBestPractices.MVVM;
using Sanet.MakaMek.Avalonia.Controls;
using Sanet.MakaMek.Avalonia.Controls.TemplatedControls;
using Sanet.MakaMek.Avalonia.Services;
using Sanet.MakaMek.Avalonia.Views;
using Sanet.MakaMek.Presentation.ViewModels.Wrappers;
using Shouldly;

namespace MakaMek.Avalonia.Tests.Controls;

/// <summary>
/// The banner announces state changes and then disappears, so at rest it must be invisible and
/// must not intercept map input. The queue behaviour is asserted with the animation stubbed out:
/// the headless platform has no animation clock to drive, so a real animation would either hang
/// or complete instantly, and neither proves the ordering.
/// </summary>
public class TurnNotificationBannerTests
{
    private static readonly TurnNotification Turn =
        new(TurnNotificationKind.Turn, "TURN 2", "#FF0000");

    private static readonly TurnNotification Phase =
        new(TurnNotificationKind.Phase, "MOVEMENT PHASE", "#FF0000");

    private static readonly TurnNotification ActivePlayer =
        new(TurnNotificationKind.ActivePlayer, "YOUR TURN", "#00FF00");

    [Fact]
    public Task Banner_IsInvisibleAndNonInteractive_AtRest() => Dispatch(() =>
    {
        var (_, view) = ShowBattleMap();
        var banner = FindBanner(view);

        banner.ShouldNotBeNull();
        banner.Opacity.ShouldBe(0);
        banner.IsHitTestVisible.ShouldBeFalse();
        banner.Current.ShouldBeNull();
    });

    [Fact]
    public Task Banner_DoesNotInterceptMapClicks() => Dispatch(() =>
    {
        var (window, view) = ShowBattleMap();
        var banner = FindBanner(view)!;
        var map = view.FindControl<HexMap>("MapCanvas")!;
        var clicks = 0;
        map.ContentClicked += (_, _) => clicks++;

        // Mid-announcement the banner is fully opaque and covers this point.
        banner.Opacity = 1;
        Settle(window);

        Click(window, banner.Bounds.Center);

        clicks.ShouldBe(1, "a click over the visible banner must still reach the map");
    });

    [Fact]
    public Task StatusBar_DoesInterceptMapClicks() => Dispatch(() =>
    {
        // Negative control: the status bar is hit-testable, so a click on it must NOT reach the
        // map. Without this, the pass-through assertion above could pass vacuously.
        var (window, view) = ShowBattleMap();
        var statusBar = view.FindControl<Grid>("TurnStatus")!;
        var map = view.FindControl<HexMap>("MapCanvas")!;
        var clicks = 0;
        map.ContentClicked += (_, _) => clicks++;

        Click(window, statusBar.Bounds.Center);

        clicks.ShouldBe(0, "the status bar is interactive and must absorb the click");
    });

    [Fact]
    public Task Announcements_ArePlayedInQueueOrder_AndReportedBack() => Dispatch(() =>
    {
        var notifications = new ObservableCollection<TurnNotification>();
        var shown = new List<string>();
        var banner = CreateBanner(notifications, shown);

        notifications.Add(Turn);
        notifications.Add(Phase);
        notifications.Add(ActivePlayer);
        Dispatcher.UIThread.RunJobs();

        shown.ShouldBe(["TURN 2", "MOVEMENT PHASE", "YOUR TURN"]);
        notifications.ShouldBeEmpty("each announcement is removed once it has been shown");
        banner.Current.ShouldBeNull("nothing is being announced once the queue drains");
    });

    [Fact]
    public Task Announcement_TakesItsTextAndTintFromTheNotification() => Dispatch(() =>
    {
        var notifications = new ObservableCollection<TurnNotification>();
        var seen = new List<(string Text, string Tint)>();
        var banner = new TurnNotificationBanner();
        banner.AnimationOverride = () =>
        {
            seen.Add((banner.CurrentText, banner.CurrentTint));
            return Task.CompletedTask;
        };
        banner.ShownCommand = new AsyncCommand<TurnNotification>(n =>
        {
            notifications.Remove(n!);
            return Task.CompletedTask;
        });
        banner.Notifications = notifications;

        notifications.Add(ActivePlayer);
        Dispatcher.UIThread.RunJobs();

        seen.ShouldBe([("YOUR TURN", "#00FF00")]);
    });

    [Fact]
    public Task Announcements_ThatArriveDuringOneAnotherAreStillPlayedInOrder() => Dispatch(() =>
    {
        var notifications = new ObservableCollection<TurnNotification>();
        var shown = new List<string>();
        var banner = CreateBanner(notifications, shown);

        // A notification arriving while another is being announced must queue, not interrupt.
        banner.AnimationOverride = () =>
        {
            if (shown.Count == 0)
                notifications.Add(Phase);
            return Task.CompletedTask;
        };

        notifications.Add(Turn);
        Dispatcher.UIThread.RunJobs();

        shown.ShouldBe(["TURN 2", "MOVEMENT PHASE"]);
    });

    [Fact]
    public Task Announcement_InsertedAheadOfTheDisplayedOne_IsStillAnnounced() => Dispatch(() =>
    {
        // Notifications are queued by priority, so a new turn arriving while a phase is being
        // announced is inserted ahead of it. The queue head is then unchanged by the removal, so
        // the banner must not read that as "nothing was removed" and stop.
        var notifications = new ObservableCollection<TurnNotification>();
        var shown = new List<string>();
        var banner = CreateBanner(notifications, shown);

        banner.AnimationOverride = () =>
        {
            if (shown.Count == 0)
                notifications.Insert(0, Turn);
            return Task.CompletedTask;
        };

        notifications.Add(Phase);
        Dispatcher.UIThread.RunJobs();

        shown.ShouldBe(["MOVEMENT PHASE", "TURN 2"]);
        notifications.ShouldBeEmpty();
    });

    [Fact]
    public Task Announcement_ThatFailsToAnimate_HidesTheBannerAndKeepsGoing() => Dispatch(() =>
    {
        // A broken animation must not strand the banner on screen or stop the queue.
        var notifications = new ObservableCollection<TurnNotification>();
        var shown = new List<string>();
        var banner = CreateBanner(notifications, shown);

        banner.AnimationOverride = () =>
        {
            banner.Opacity = 1;
            if (shown.Count == 0)
                throw new InvalidOperationException("animation failed");
            return Task.CompletedTask;
        };

        notifications.Add(Turn);
        notifications.Add(Phase);
        Dispatcher.UIThread.RunJobs();

        shown.ShouldBe(["TURN 2", "MOVEMENT PHASE"], "the failed announcement is skipped, not retried");
        notifications.ShouldBeEmpty();
        banner.Current.ShouldBeNull();
    });

    [Fact]
    public Task Announcement_ThatFailsToAnimate_LeavesTheBannerInvisible() => Dispatch(() =>
    {
        var notifications = new ObservableCollection<TurnNotification>();
        var shown = new List<string>();
        var banner = CreateBanner(notifications, shown);

        banner.AnimationOverride = () =>
        {
            banner.Opacity = 1;
            throw new InvalidOperationException("animation failed");
        };

        notifications.Add(Turn);
        Dispatcher.UIThread.RunJobs();

        banner.Opacity.ShouldBe(0, "a failed animation must not leave the banner on screen");
    });

    [Fact]
    public Task Banner_StopsAnnouncing_WhenNothingRemovesTheNotification() => Dispatch(() =>
    {
        // A source that ignores ShownCommand must not put the banner in an endless loop.
        var notifications = new ObservableCollection<TurnNotification> { Turn };
        var plays = 0;
        var banner = new TurnNotificationBanner();
        // The override has to be in place before the queue is attached: attaching it is what
        // starts the pump.
        banner.AnimationOverride = () =>
        {
            plays++;
            return Task.CompletedTask;
        };
        banner.Notifications = notifications;
        Dispatcher.UIThread.RunJobs();

        plays.ShouldBe(1, "the same notification must not be announced repeatedly");
        notifications.ShouldHaveSingleItem("nothing removed it, so it stays queued");
    });

    [Fact]
    public Task AnimationResource_IsAvailable() => Dispatch(() =>
    {
        // Without the resource the control falls back to a plain delay, which would silently
        // downgrade the announcement to a static label.
        var resourcesLocator = new AvaloniaResourcesLocator();

        resourcesLocator.TryFindResource("TurnNotificationAnimation").ShouldBeOfType<Animation>();
    });

    private static TurnNotificationBanner CreateBanner(
        ObservableCollection<TurnNotification> notifications, List<string> shown)
    {
        var banner = new TurnNotificationBanner();
        banner.AnimationOverride = () => Task.CompletedTask;
        banner.ShownCommand = new AsyncCommand<TurnNotification>(notification =>
        {
            shown.Add(notification!.Text);
            notifications.Remove(notification);
            return Task.CompletedTask;
        });
        banner.Notifications = notifications;
        return banner;
    }

    private static TurnNotificationBanner? FindBanner(BattleMapView view) =>
        view.GetVisualDescendants().OfType<TurnNotificationBanner>().FirstOrDefault();

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

    private static void Settle(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    private static Task Dispatch(Action action)
    {
        var session = HeadlessUnitTestSession.GetOrStartForAssembly(
            typeof(TurnNotificationBannerTests).Assembly);
        return session.Dispatch(action, CancellationToken.None);
    }
}
