using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Shouldly;
using Sanet.MakaMek.Avalonia.Controls.TemplatedControls;

namespace MakaMek.Avalonia.Tests.Controls;

public class ActionButtonTests
{
    [Fact]
    public void ActionButton_WhenCreated_ShouldHaveDefaultProperties()
    {
        // Arrange & Act
        var button = new ActionButton();

        // Assert
        button.ShouldNotBeNull();
        button.IsEnabled.ShouldBeTrue();
    }

    [Fact]
    public async Task ActionButton_WhenClicked_ShouldExecuteItsCommand()
    {
        await Dispatch(() =>
        {
            var command = new CountingCommand();
            var (window, button) = ShowButton(command);
            try
            {
                var rendered = Rendered(button);
                var point = CentreOf(window, rendered);
                HitLandsOn(window, point, rendered).ShouldBeTrue("the click must land on the button");

                Click(window, point);

                command.Executions.ShouldBe(1);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task ActionButton_WhenDisabled_ShouldNotExecuteItsCommand()
    {
        await Dispatch(() =>
        {
            var command = new CountingCommand();
            var (window, button) = ShowButton(command);
            try
            {
                // Take the point while the button is still live, so the click below is known to be
                // aimed at it - otherwise "nothing happened" could just mean "nothing was there".
                var rendered = Rendered(button);
                var point = CentreOf(window, rendered);
                HitLandsOn(window, point, rendered).ShouldBeTrue();

                button.IsEnabled = false;
                Settle(window);

                Click(window, point);

                command.Executions.ShouldBe(0);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task ActionButton_WhenItsCommandCannotExecute_ShouldNotExecuteIt()
    {
        await Dispatch(() =>
        {
            var command = new CountingCommand { CanRun = false };
            var (window, button) = ShowButton(command);
            try
            {
                var rendered = Rendered(button);
                var point = CentreOf(window, rendered);

                Click(window, point);

                command.Executions.ShouldBe(0);
                // A command that cannot execute disables the button, which also takes it out of
                // hit testing - the click falls through to whatever is behind it.
                HitLandsOn(window, point, rendered).ShouldBeFalse();
            }
            finally
            {
                window.Close();
            }
        });
    }

    private static Task Dispatch(Action action)
    {
        var session = HeadlessUnitTestSession.GetOrStartForAssembly(typeof(ActionButtonTests).Assembly);
        return session.Dispatch(action, CancellationToken.None);
    }

    private static (Window Window, ActionButton Button) ShowButton(ICommand command)
    {
        var button = new ActionButton { Command = command };
        var window = new Window
        {
            Width = 200,
            Height = 200,
            Content = button
        };
        window.Show();
        Settle(window);
        return (window, button);
    }

    /// <summary>
    /// The button the template actually draws. ActionButton stretches to fill its parent while its
    /// template draws a fixed 40x40 button at the left edge, so the centre of the outer control is
    /// empty space - a click there hits nothing and proves nothing.
    /// </summary>
    private static Button Rendered(ActionButton button)
        => button.GetVisualDescendants().OfType<Button>().First();

    /// <summary>
    /// Whether a click at <paramref name="point"/> reaches <paramref name="target"/>. Hit testing
    /// returns the innermost visual - for a templated button that is a Border inside it - so this
    /// asks about containment rather than identity.
    /// </summary>
    private static bool HitLandsOn(Window window, Point point, Visual target)
    {
        if (window.InputHitTest(point) is not Visual hit) return false;
        return hit == target || hit.GetVisualAncestors().Contains(target);
    }

    private static Point CentreOf(Window window, Visual visual)
    {
        var local = new Point(visual.Bounds.Width / 2, visual.Bounds.Height / 2);
        return visual.TranslatePoint(local, window)!.Value;
    }

    private static void Click(Window window, Point point)
    {
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>
    /// Runs layout to completion. Showing a window does not guarantee it, and every assertion here
    /// is about where a control ended up, so stale bounds make the tests fail depending on what
    /// else ran first.
    /// </summary>
    private static void Settle(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    private sealed class CountingCommand : ICommand
    {
        public int Executions { get; private set; }

        public bool CanRun { get; init; } = true;

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter) => CanRun;

        public void Execute(object? parameter) => Executions++;
    }
}
