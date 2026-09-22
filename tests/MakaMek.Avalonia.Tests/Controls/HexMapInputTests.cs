using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using MakaMek.Avalonia.Tests.TestHelpers;
using Sanet.MakaMek.Avalonia.Controls;
using Shouldly;

namespace MakaMek.Avalonia.Tests.Controls;

/// <summary>
/// Pointer handling in <see cref="HexMap"/>.
/// </summary>
/// <remarks>
/// This asserts what the control does with the event - marks the wheel handled - rather than the
/// symptom it prevents. The symptom needs an OS: a headless wheel event does not drive a
/// ScrollViewer at all (checked - a plain ScrollViewer stays at offset 0), so a test written
/// against the ancestor scrolling passes whether or not the fix is present.
/// </remarks>
public class HexMapInputTests
{
    [Fact]
    public async Task PointerWheel_ShouldMarkTheEventHandled()
    {
        // An unhandled wheel event keeps bubbling, so an ancestor ScrollViewer scrolls on the same
        // notch that zoomed the map.
        await Dispatch(() =>
        {
            var (window, map) = ShowMap();
            try
            {
                var handled = false;
                map.AddHandler(
                    InputElement.PointerWheelChangedEvent,
                    (_, e) => handled = e.Handled,
                    RoutingStrategies.Bubble,
                    handledEventsToo: true);

                window.MouseWheel(window.CentreOf(map), new Vector(0, -1));

                handled.ShouldBeTrue("the map must consume the wheel so ancestors do not also act on it");
            }
            finally
            {
                window.Close();
            }
        });
    }

    private static (Window Window, HexMap Map) ShowMap()
    {
        var map = new HexMap
        {
            Width = 300,
            Height = 300,
            // A Canvas with no background is not hit-testable, so no pointer event would arrive.
            Background = Brushes.Transparent
        };
        var window = new Window { Width = 800, Height = 800, Content = map };
        window.Show();
        window.Settle();
        return (window, map);
    }

    private static Task Dispatch(Action action)
    {
        var session = HeadlessUnitTestSession.GetOrStartForAssembly(typeof(HexMapInputTests).Assembly);
        return session.Dispatch(action, CancellationToken.None);
    }
}
