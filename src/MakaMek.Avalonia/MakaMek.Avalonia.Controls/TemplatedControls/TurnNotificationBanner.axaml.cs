using System.Collections.Specialized;
using System.Windows.Input;
using AsyncAwaitBestPractices;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Sanet.MakaMek.Presentation.ViewModels.Wrappers;

namespace Sanet.MakaMek.Avalonia.Controls.TemplatedControls;

/// <summary>
/// Announces transient game state changes one at a time: it takes a queue of notifications,
/// animates each one on its own, and reports it back so the source can drop it.
///
/// The queue and the animation live here rather than in a view's code-behind so any view that
/// wants announcements only has to bind a collection.
/// </summary>
public class TurnNotificationBanner : TemplatedControl
{
    /// <summary>
    /// The pending notifications. Shown in order; an <see cref="INotifyCollectionChanged"/>
    /// source starts the next announcement as soon as one is added.
    /// </summary>
    public static readonly StyledProperty<IEnumerable<TurnNotification>?> NotificationsProperty =
        AvaloniaProperty.Register<TurnNotificationBanner, IEnumerable<TurnNotification>?>(
            nameof(Notifications));

    /// <summary>
    /// Executed with a notification once it has finished animating. Removing it from
    /// <see cref="Notifications"/> is the source's job, which keeps this control stateless.
    /// </summary>
    public static readonly StyledProperty<ICommand?> ShownCommandProperty =
        AvaloniaProperty.Register<TurnNotificationBanner, ICommand?>(nameof(ShownCommand));

    /// <summary>
    /// The notification currently being announced, or null between announcements.
    /// </summary>
    public static readonly StyledProperty<TurnNotification?> CurrentProperty =
        AvaloniaProperty.Register<TurnNotificationBanner, TurnNotification?>(nameof(Current));

    /// <summary>
    /// The text of the current announcement. Flattened out of <see cref="Current"/> because
    /// TemplateBinding binds a single property rather than a path.
    /// </summary>
    public static readonly StyledProperty<string> CurrentTextProperty =
        AvaloniaProperty.Register<TurnNotificationBanner, string>(nameof(CurrentText), string.Empty);

    /// <summary>
    /// The colour of the current announcement, as a hex string.
    /// </summary>
    public static readonly StyledProperty<string> CurrentTintProperty =
        AvaloniaProperty.Register<TurnNotificationBanner, string>(nameof(CurrentTint), "#FFFFFF");

    /// <summary>
    /// The name of the animation resource used for each announcement.
    /// </summary>
    public static readonly StyledProperty<string> AnimationResourceKeyProperty =
        AvaloniaProperty.Register<TurnNotificationBanner, string>(
            nameof(AnimationResourceKey), "TurnNotificationAnimation");

    /// <summary>
    /// How long an announcement stays up when the animation resource cannot be found.
    /// </summary>
    public static readonly StyledProperty<TimeSpan> FallbackDurationProperty =
        AvaloniaProperty.Register<TurnNotificationBanner, TimeSpan>(
            nameof(FallbackDuration), TimeSpan.FromMilliseconds(1200));

    private INotifyCollectionChanged? _observed;
    private bool _isAnnouncing;

    /// <summary>
    /// Replaces the animation for tests, which cannot drive a real animation clock headlessly.
    /// </summary>
    internal Func<Task>? AnimationOverride { get; set; }

    public IEnumerable<TurnNotification>? Notifications
    {
        get => GetValue(NotificationsProperty);
        set => SetValue(NotificationsProperty, value);
    }

    public ICommand? ShownCommand
    {
        get => GetValue(ShownCommandProperty);
        set => SetValue(ShownCommandProperty, value);
    }

    public TurnNotification? Current
    {
        get => GetValue(CurrentProperty);
        private set
        {
            SetValue(CurrentProperty, value);
            CurrentText = value?.Text ?? string.Empty;
            CurrentTint = value?.Tint ?? "#FFFFFF";
        }
    }

    public string CurrentText
    {
        get => GetValue(CurrentTextProperty);
        private set => SetValue(CurrentTextProperty, value);
    }

    public string CurrentTint
    {
        get => GetValue(CurrentTintProperty);
        private set => SetValue(CurrentTintProperty, value);
    }

    public string AnimationResourceKey
    {
        get => GetValue(AnimationResourceKeyProperty);
        set => SetValue(AnimationResourceKeyProperty, value);
    }

    public TimeSpan FallbackDuration
    {
        get => GetValue(FallbackDurationProperty);
        set => SetValue(FallbackDurationProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property != NotificationsProperty) return;

        if (_observed != null)
            _observed.CollectionChanged -= OnNotificationsChanged;
        _observed = change.GetNewValue<IEnumerable<TurnNotification>?>() as INotifyCollectionChanged;
        if (_observed != null)
            _observed.CollectionChanged += OnNotificationsChanged;

        PumpAsync();
    }

    private void OnNotificationsChanged(object? sender, NotifyCollectionChangedEventArgs e) => PumpAsync();

    /// <summary>
    /// Announces queued notifications until the queue is empty. Re-entrant calls return
    /// immediately, so an announcement is never interrupted by the next arrival.
    /// </summary>
    private void PumpAsync()
    {
        if (_isAnnouncing) return;
        AnnounceQueueAsync().SafeFireAndForget();
    }

    private async Task AnnounceQueueAsync()
    {
        if (_isAnnouncing) return;
        _isAnnouncing = true;
        try
        {
            while (Notifications?.FirstOrDefault() is { } notification)
            {
                Current = notification;
                try
                {
                    await AnimateAsync();
                }
                catch (Exception)
                {
                    // A failed animation must not leave the banner on screen or stop the rest of
                    // the queue, so hide it and treat this notification as announced.
                    Opacity = 0;
                }
                Current = null;

                if (ShownCommand?.CanExecute(notification) == true)
                    ShownCommand.Execute(notification);

                // The source removes it, which is what advances the queue. Look for the announced
                // object anywhere in the queue rather than just at the head: a higher priority
                // notification arriving mid-announcement is inserted ahead of it, so comparing
                // heads would read as "nothing was removed". Compare by reference because
                // TurnNotification has value equality and an equal record may be queued too.
                if (Notifications?.Any(queued => ReferenceEquals(queued, notification)) == true)
                    break;
            }
        }
        finally
        {
            _isAnnouncing = false;
        }
    }

    private async Task AnimateAsync()
    {
        if (AnimationOverride != null)
        {
            await AnimationOverride();
            return;
        }

        if (this.TryFindResource(AnimationResourceKey, out var resource) && resource is Animation animation)
        {
            await animation.RunAsync(this);
            return;
        }

        Opacity = 1;
        await Task.Delay(FallbackDuration);
        Opacity = 0;
    }
}
