using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Platform;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System;

namespace Sanet.MakaMek.Avalonia.Behaviors;

public static class KeyboardAwareBehavior
{
    private static readonly AttachedProperty<bool> IsEnabledProperty =
        AvaloniaProperty.RegisterAttached<TemplatedControl, bool>(
            "IsEnabled",
            typeof(KeyboardAwareBehavior));

    public static void SetIsEnabled(TemplatedControl element, bool value) =>
        element.SetValue(IsEnabledProperty, value);

    private static readonly AttachedProperty<IInputPane?> InputPaneProperty =
        AvaloniaProperty.RegisterAttached<TemplatedControl, IInputPane?>(
            "InputPane",
            typeof(KeyboardAwareBehavior));

    private static readonly AttachedProperty<IDisposable?> SubscriptionProperty =
        AvaloniaProperty.RegisterAttached<TemplatedControl, IDisposable?>(
            "Subscription",
            typeof(KeyboardAwareBehavior));

    private static readonly AttachedProperty<Thickness?> SavedPaddingProperty =
        AvaloniaProperty.RegisterAttached<TemplatedControl, Thickness?>(
            "SavedPadding",
            typeof(KeyboardAwareBehavior));

    static KeyboardAwareBehavior()
    {
        IsEnabledProperty.Changed.AddClassHandler<TemplatedControl>(OnIsEnabledChanged);
    }

    private static void OnIsEnabledChanged(TemplatedControl control, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.NewValue is true)
        {
            control.AttachedToVisualTree += OnAttachedToVisualTree;
            control.DetachedFromVisualTree += OnDetachedFromVisualTree;

            if (control.IsAttachedToVisualTree())
            {
                Attach(control);
            }
        }
        else
        {
            control.AttachedToVisualTree -= OnAttachedToVisualTree;
            control.DetachedFromVisualTree -= OnDetachedFromVisualTree;
            Detach(control);
        }
    }

    private static void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (sender is TemplatedControl control)
        {
            Attach(control);
        }
    }

    private static void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (sender is TemplatedControl control)
        {
            Detach(control);
            control.ClearValue(SavedPaddingProperty);
            control.ClearValue(InputPaneProperty);
        }
    }

    private static void Attach(TemplatedControl control)
    {
        var topLevel = TopLevel.GetTopLevel(control);
        var inputPane = topLevel?.InputPane;
        if (inputPane == null) return;

        control.SetValue(InputPaneProperty, inputPane);

        void OnStateChanged(object? s, InputPaneStateEventArgs args)
        {
            OnInputPaneStateChanged(control, args);
        }

        inputPane.StateChanged += OnStateChanged;
        control.GotFocus += OnFocusChanged;
        control.SetValue(SubscriptionProperty, new InputPaneSubscription(inputPane, OnStateChanged, control));
    }

    private static void Detach(TemplatedControl control)
    {
        if (control.GetValue(SubscriptionProperty) is { } subscription)
        {
            subscription.Dispose();
        }
    }

    private static void OnFocusChanged(object? sender, EventArgs e)
    {
        if (sender is not TemplatedControl control) return;

        var topLevel = TopLevel.GetTopLevel(control);
        var inputPane = topLevel?.InputPane;
        if (inputPane == null || inputPane.State != InputPaneState.Open) return;
        if (inputPane.OccludedRect.Height <= 0) return;

        Dispatcher.UIThread.Post(() => BringFocusedElementIntoView(control));
    }

    private static void OnInputPaneStateChanged(TemplatedControl control, InputPaneStateEventArgs e)
    {
        if (e.NewState == InputPaneState.Open)
        {
            var occluded = e.EndRect;
            if (occluded.Height > 0)
            {
                var saved = control.GetValue(SavedPaddingProperty);
                if (saved == null)
                {
                    saved = control.Padding;
                    control.SetValue(SavedPaddingProperty, saved);
                }
                control.Padding = new Thickness(
                    saved.Value.Left, saved.Value.Top, saved.Value.Right, saved.Value.Bottom + occluded.Height);
            }
            Dispatcher.UIThread.Post(() => BringFocusedElementIntoView(control));
        }
        else
        {
            var saved = control.GetValue(SavedPaddingProperty);
            control.Padding = saved ?? new Thickness(0);
            BringFocusedElementIntoView(control);
        }
    }

    private static void BringFocusedElementIntoView(TemplatedControl control)
    {
        var topLevel = TopLevel.GetTopLevel(control);
        var focused = topLevel?.FocusManager.GetFocusedElement() as Control;
        if (focused == null) return;

        focused.BringIntoView();
    }

    private sealed class InputPaneSubscription : IDisposable
    {
        private readonly IInputPane _inputPane;
        private readonly EventHandler<InputPaneStateEventArgs> _handler;
        private readonly TemplatedControl _control;

        public InputPaneSubscription(IInputPane inputPane, EventHandler<InputPaneStateEventArgs> handler, TemplatedControl control)
        {
            _inputPane = inputPane;
            _handler = handler;
            _control = control;
        }

        public void Dispose()
        {
            _inputPane.StateChanged -= _handler;
            _control.GotFocus -= OnFocusChanged;
        }
    }
}
