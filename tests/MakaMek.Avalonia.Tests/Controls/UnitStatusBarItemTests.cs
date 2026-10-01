using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.VisualTree;
using MakaMek.Avalonia.Tests.TestHelpers;
using Sanet.MakaMek.Avalonia.Controls;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Shouldly;

namespace MakaMek.Avalonia.Tests.Controls;

public class UnitStatusBarItemTests
{
    [Fact]
    public async Task Card_WhenClicked_InspectsItsOwnUnit()
    {
        await Dispatch(() =>
        {
            var inspect = new CountingCommand();
            var unit = CreateUnit();
            var (window, card) = ShowCard(unit, inspect, new CountingCommand());
            try
            {
                var button = Buttons(card)[0];
                var point = window.CentreOf(button);
                window.HitLandsOn(point, button).ShouldBeTrue("the click must land on the card");

                window.Click(point);

                inspect.Executions.ShouldBe(1);
                inspect.LastParameter.ShouldBe(unit);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task FocusButton_WhenClicked_FocusesItsOwnUnit()
    {
        await Dispatch(() =>
        {
            var focus = new CountingCommand();
            var unit = CreateUnit();
            var (window, card) = ShowCard(unit, new CountingCommand(), focus);
            try
            {
                var button = Buttons(card)[1];
                var point = window.CentreOf(button);
                window.HitLandsOn(point, button).ShouldBeTrue("the click must land on the focus button");

                window.Click(point);

                focus.Executions.ShouldBe(1);
                focus.LastParameter.ShouldBe(unit);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task Card_WithoutCommands_DoesNotThrowWhenClicked()
    {
        await Dispatch(() =>
        {
            var (window, card) = ShowCard(CreateUnit(), null, null);
            try
            {
                var button = Buttons(card)[0];
                Should.NotThrow(() => window.Click(window.CentreOf(button)));
            }
            finally
            {
                window.Close();
            }
        });
    }

    private static Task Dispatch(Action action)
    {
        var session = HeadlessUnitTestSession.GetOrStartForAssembly(typeof(UnitStatusBarItemTests).Assembly);
        return session.Dispatch(action, CancellationToken.None);
    }

    private static IUnit CreateUnit() => new Mech("Locust", "LCT-1V", 20, []);

    private static (Window Window, UnitStatusBarItem Card) ShowCard(
        IUnit unit,
        ICommand? inspectCommand,
        ICommand? focusCommand)
    {
        var card = new UnitStatusBarItem
        {
            DataContext = unit,
            InspectCommand = inspectCommand,
            FocusCommand = focusCommand
        };
        var window = new Window
        {
            Width = 400,
            Height = 200,
            Content = card
        };
        window.Show();
        window.Settle();
        return (window, card);
    }

    /// <summary>
    /// The card's two buttons in layout order: the body opens the drawer, the trailing one
    /// centers the map.
    /// </summary>
    private static IReadOnlyList<Button> Buttons(UnitStatusBarItem card)
        => card.GetVisualDescendants().OfType<Button>().ToList();

    private sealed class CountingCommand : ICommand
    {
        public int Executions { get; private set; }

        public object? LastParameter { get; private set; }

        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter)
        {
            Executions++;
            LastParameter = parameter;
        }
    }
}
