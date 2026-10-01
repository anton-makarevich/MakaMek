using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Sanet.MakaMek.Avalonia.Behaviors;

/// <summary>
/// Lets a horizontal strip be scrolled with the wheel or by dragging it, so the overflow can stay
/// hidden instead of spending vertical space on a scrollbar.
/// </summary>
public static class DragScrollBehavior
{
    /// <summary>
    /// How far the pointer must travel before a press becomes a drag. Below this a press is left
    /// alone so the cards underneath still take their clicks.
    /// </summary>
    private const double DragThreshold = 6;

    /// <summary>Scroll distance per wheel notch.</summary>
    private const double WheelStep = 60;

    public static readonly AttachedProperty<bool> EnableHorizontalDragScrollProperty =
        AvaloniaProperty.RegisterAttached<ScrollViewer, bool>(
            "EnableHorizontalDragScroll",
            typeof(DragScrollBehavior));

    public static bool GetEnableHorizontalDragScroll(ScrollViewer element) =>
        element.GetValue(EnableHorizontalDragScrollProperty);

    public static void SetEnableHorizontalDragScroll(ScrollViewer element, bool value) =>
        element.SetValue(EnableHorizontalDragScrollProperty, value);

    private static readonly AttachedProperty<DragState?> StateProperty =
        AvaloniaProperty.RegisterAttached<ScrollViewer, DragState?>("DragScrollState", typeof(DragScrollBehavior));

    static DragScrollBehavior()
    {
        EnableHorizontalDragScrollProperty.Changed.AddClassHandler<ScrollViewer>(OnEnabledChanged);
    }

    private static void OnEnabledChanged(ScrollViewer scrollViewer, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.NewValue is true)
        {
            scrollViewer.AddHandler(InputElement.PointerWheelChangedEvent, OnWheel, RoutingStrategies.Tunnel);
            scrollViewer.AddHandler(InputElement.PointerPressedEvent, OnPressed, RoutingStrategies.Tunnel);
            scrollViewer.AddHandler(InputElement.PointerMovedEvent, OnMoved, RoutingStrategies.Tunnel);
            scrollViewer.AddHandler(InputElement.PointerReleasedEvent, OnReleased, RoutingStrategies.Tunnel);
            scrollViewer.AddHandler(InputElement.PointerCaptureLostEvent, OnCaptureLost, RoutingStrategies.Direct);
            return;
        }

        scrollViewer.RemoveHandler(InputElement.PointerWheelChangedEvent, OnWheel);
        scrollViewer.RemoveHandler(InputElement.PointerPressedEvent, OnPressed);
        scrollViewer.RemoveHandler(InputElement.PointerMovedEvent, OnMoved);
        scrollViewer.RemoveHandler(InputElement.PointerReleasedEvent, OnReleased);
        scrollViewer.RemoveHandler(InputElement.PointerCaptureLostEvent, OnCaptureLost);
        scrollViewer.SetValue(StateProperty, null);
    }

    private static void OnWheel(object? sender, PointerWheelEventArgs e)
    {
        if (sender is not ScrollViewer scrollViewer) return;

        // A wheel over a strip with nothing to scroll vertically should move it sideways, which is
        // the only direction it has.
        var delta = Math.Abs(e.Delta.X) > Math.Abs(e.Delta.Y) ? e.Delta.X : e.Delta.Y;
        if (delta == 0) return;

        if (ScrollBy(scrollViewer, -delta * WheelStep)) e.Handled = true;
    }

    private static void OnPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not ScrollViewer scrollViewer) return;
        if (!e.GetCurrentPoint(scrollViewer).Properties.IsLeftButtonPressed) return;

        // The scrollbar moves the strip itself. Panning as well would move it twice per drag.
        if (IsOnScrollBar(e.Source as Visual)) return;

        scrollViewer.SetValue(StateProperty, new DragState(e.GetPosition(scrollViewer).X, scrollViewer.Offset.X));
    }

    private static bool IsOnScrollBar(Visual? source)
    {
        for (var visual = source; visual is not null; visual = visual.GetVisualParent())
        {
            if (visual is ScrollBar) return true;
        }

        return false;
    }

    private static void OnMoved(object? sender, PointerEventArgs e)
    {
        if (sender is not ScrollViewer scrollViewer) return;
        if (scrollViewer.GetValue(StateProperty) is not { } state) return;

        var travelled = e.GetPosition(scrollViewer).X - state.PointerX;
        if (!state.Dragging && Math.Abs(travelled) < DragThreshold) return;

        // Past the threshold this is a drag, so take the pointer away from whatever was under it.
        if (!state.Dragging)
        {
            state.Dragging = true;
            e.Pointer.Capture(scrollViewer);
        }

        SetOffset(scrollViewer, state.StartOffset - travelled);
        e.Handled = true;
    }

    private static void OnReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (sender is not ScrollViewer scrollViewer) return;

        var wasDragging = scrollViewer.GetValue(StateProperty) is { Dragging: true };
        scrollViewer.SetValue(StateProperty, null);

        // Swallow the release that ended a drag so it is not also read as a click on a card.
        if (wasDragging) e.Handled = true;
    }

    private static void OnCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        if (sender is ScrollViewer scrollViewer) scrollViewer.SetValue(StateProperty, null);
    }

    private static bool ScrollBy(ScrollViewer scrollViewer, double amount)
    {
        var before = scrollViewer.Offset.X;
        SetOffset(scrollViewer, before + amount);
        return Math.Abs(scrollViewer.Offset.X - before) > double.Epsilon;
    }

    private static void SetOffset(ScrollViewer scrollViewer, double x)
    {
        var max = Math.Max(0, scrollViewer.Extent.Width - scrollViewer.Viewport.Width);
        scrollViewer.Offset = scrollViewer.Offset.WithX(Math.Clamp(x, 0, max));
    }

    private sealed class DragState(double pointerX, double startOffset)
    {
        public double PointerX { get; } = pointerX;
        public double StartOffset { get; } = startOffset;
        public bool Dragging { get; set; }
    }
}
