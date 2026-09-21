using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace MakaMek.Avalonia.Tests.TestHelpers;

/// <summary>
/// Drives a headless window with real input, so a view test can assert what a click does rather
/// than read a property back.
/// </summary>
public static class HeadlessInput
{
    /// <summary>
    /// Runs layout to completion. Showing a window does not guarantee it, so bounds read straight
    /// after <see cref="Window.Show"/> can be stale - which makes any test that computes a point
    /// from them pass or fail depending on what ran before it in the suite.
    /// </summary>
    public static void Settle(this Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>
    /// Presses and releases the left mouse button at <paramref name="point"/>, in window
    /// coordinates.
    /// </summary>
    public static void Click(this Window window, Point point)
    {
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>
    /// The centre of <paramref name="visual"/> in window coordinates. Always aim at what a control
    /// renders: a control that stretches to fill its parent while its template draws something
    /// smaller has empty space at its own centre.
    /// </summary>
    public static Point CentreOf(this Window window, Visual visual)
    {
        var local = new Point(visual.Bounds.Width / 2, visual.Bounds.Height / 2);
        return visual.TranslatePoint(local, window)!.Value;
    }

    /// <summary>
    /// Whether a click at <paramref name="point"/> would reach <paramref name="target"/>. Hit
    /// testing returns the innermost visual - for a templated button that is a Border inside it -
    /// so this asks about containment rather than identity.
    /// </summary>
    /// <remarks>
    /// Worth asserting before every negative case. A control with no Background is not hit-testable
    /// at all, and a disabled one drops out of hit testing entirely, so "the command did not run"
    /// is otherwise satisfied just as well by a click that landed on nothing.
    /// </remarks>
    public static bool HitLandsOn(this Window window, Point point, Visual target)
    {
        if (window.InputHitTest(point) is not Visual hit) return false;
        return hit == target || hit.GetVisualAncestors().Contains(target);
    }
}
