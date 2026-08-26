using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Platform;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System;
using System.Linq;

namespace Sanet.MakaMek.Avalonia.Behaviors;

public static class KeyboardAwareBehavior
{
    public static readonly AttachedProperty<bool> IsEnabledProperty =
        AvaloniaProperty.RegisterAttached<TemplatedControl, bool>(
            "IsEnabled",
            typeof(KeyboardAwareBehavior));

    public static bool GetIsEnabled(TemplatedControl element) =>
        element.GetValue(IsEnabledProperty);

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
            control.ClearValue(InputPaneProperty);
            control.ClearValue(SubscriptionProperty);
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
        control.SetValue(SubscriptionProperty, new InputPaneSubscription(inputPane, OnStateChanged));
    }

    private static void Detach(TemplatedControl control)
    {
        if (control.GetValue(SubscriptionProperty) is { } subscription)
        {
            ((IDisposable)subscription).Dispose();
        }
    }

    private static void OnInputPaneStateChanged(TemplatedControl control, InputPaneStateEventArgs e)
    {
        if (e.NewState == InputPaneState.Open)
        {
            var occluded = e.EndRect;
            if (occluded.Height > 0)
            {
                control.Padding = new Thickness(0, 0, 0, occluded.Height);
            }
            Dispatcher.UIThread.Post(() => BringFocusedElementIntoView(control));
        }
        else
        {
            control.Padding = new Thickness(0);
            BringFocusedElementIntoView(control);
        }
    }

    private static void BringFocusedElementIntoView(TemplatedControl control)
    {
        var topLevel = TopLevel.GetTopLevel(control);
        var focused = topLevel?.FocusManager?.GetFocusedElement() as Visual;
        if (focused == null) return;

        var scrollViewer = focused.GetVisualAncestors().OfType<ScrollViewer>().FirstOrDefault();
        scrollViewer?.BringIntoView(focused.Bounds);
    }

    private sealed class InputPaneSubscription : IDisposable
    {
        private readonly IInputPane _inputPane;
        private readonly EventHandler<InputPaneStateEventArgs> _handler;

        public InputPaneSubscription(IInputPane inputPane, EventHandler<InputPaneStateEventArgs> handler)
        {
            _inputPane = inputPane;
            _handler = handler;
        }

        public void Dispose()
        {
            _inputPane.StateChanged -= _handler;
        }
    }
}
