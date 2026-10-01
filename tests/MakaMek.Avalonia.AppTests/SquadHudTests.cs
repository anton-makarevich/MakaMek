using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Controls.Primitives;
using global::Avalonia.Layout;
using global::Avalonia.Headless;
using global::Avalonia.Input;
using global::Avalonia.Interactivity;
using NSubstitute;
using global::Avalonia.Media;
using global::Avalonia.Media.Imaging;
using global::Avalonia.Threading;
using global::Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sanet.MakaMek.Avalonia;
using Sanet.MakaMek.Avalonia.Controls;
using Sanet.MakaMek.Avalonia.Views;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Mechs.Falling;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Services.Cryptography;
using Sanet.MakaMek.Core.Utils;
using Sanet.MakaMek.Core.Services.Transport;
using Sanet.MakaMek.Map.Factories;
using Sanet.MakaMek.Map.Generators;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Models.Terrains;
using Sanet.MakaMek.Presentation.UiStates;
using Sanet.MakaMek.Presentation.ViewModels;
using Sanet.MVVM.Core.Views;
using Shouldly;

namespace MakaMek.Avalonia.AppTests;

/// <summary>
/// The squad bar and the inspection drawer, with a real game holding real units from the
/// repository's own data folder.
///
/// The other HUD tests run against a view model with no game, so everything driven by
/// <c>LocalUnits</c> renders as nothing. The squad bar is the densest part of this HUD and the part
/// most likely to be too much on a small screen, so it has to be rendered with units in it before
/// anyone can judge it.
/// </summary>
public class SquadHudTests
{
    [Fact]
    public Task SquadBar_RendersACardPerLocalUnit() => HarnessSession.Run(async () =>
    {
        var (window, view, viewModel) = await ShowBattleMapWithSquad();
        try
        {
            viewModel.LocalUnits.Count().ShouldBe(4, "the fixture joins one player with four units");
            viewModel.IsSquadStatusBarVisible.ShouldBeTrue();

            var cards = view.GetVisualDescendants().OfType<UnitStatusBarItem>().ToList();
            cards.Count.ShouldBe(4, "every local unit should get a card");
            cards.ShouldAllBe(card => card.IsEffectivelyVisible);
        }
        finally
        {
            window.Close();
        }
    });

    [Fact]
    public Task SquadCard_OpensTheDrawer_WithoutChangingTheSelection() => HarnessSession.Run(async () =>
    {
        var (window, view, viewModel) = await ShowBattleMapWithSquad();
        try
        {
            var unit = viewModel.LocalUnits.Last();
            viewModel.IsRecordSheetPanelVisible.ShouldBeFalse();

            viewModel.InspectUnit(unit);
            Settle(window);

            viewModel.InspectedUnit.ShouldBe(unit);
            viewModel.SelectedUnit.ShouldBeNull("inspecting must not change what the phase acts on");

            var sheet = view.GetVisualDescendants().OfType<UnitRecordSheet>().FirstOrDefault();
            sheet.ShouldNotBeNull();
            sheet.IsEffectivelyVisible.ShouldBeTrue("the drawer should be on screen");
            sheet.Unit.ShouldBe(unit, "and should be showing the inspected unit, not the selection");
        }
        finally
        {
            window.Close();
        }
    });

    [Fact]
    public Task PinnedDrawer_SurvivesAPhaseStep_OnScreen() => HarnessSession.Run(async () =>
    {
        var (window, view, viewModel) = await ShowBattleMapWithSquad();
        try
        {
            var unit = viewModel.LocalUnits.First();
            viewModel.InspectUnit(unit);
            viewModel.ToggleRecordSheetPin();
            Settle(window);

            // The same call TransitionToState and ClearSelection make on every phase step.
            viewModel.NotifySelectedUnitChanged();
            Settle(window);

            var sheet = view.GetVisualDescendants().OfType<UnitRecordSheet>().FirstOrDefault();
            sheet.ShouldNotBeNull();
            sheet.IsEffectivelyVisible.ShouldBeTrue("a pinned drawer should still be up");
            sheet.Unit.ShouldBe(unit, "and should still be showing the unit that was pinned");
        }
        finally
        {
            window.Close();
        }
    });

    [Fact]
    public Task Screenshots_OfTheSquadHud() => HarnessSession.Run(async () =>
    {
        foreach (var (name, width, height) in new[]
                 {
                     ("squad-desktop.png", 1280, 800),
                     ("squad-narrow.png", 430, 880)
                 })
        {
            var (window, _, viewModel) = await ShowBattleMapWithSquad(width, height);
            try
            {
                await SettleUntilTilesDecode(window);
                using var bare = window.CaptureRenderedFrame();
                Save(bare!, name);

                viewModel.InspectUnit(viewModel.LocalUnits.First());
                Settle(window);
                using var withDrawer = window.CaptureRenderedFrame();
                Save(withDrawer!, name.Replace(".png", "-drawer.png"));

                // The case where the squad bar has to give width back.
                viewModel.ToggleRecordSheet();
                viewModel.ToggleMapControlsDrawer();
                Settle(window);
                using var withControls = window.CaptureRenderedFrame();
                Save(withControls!, name.Replace(".png", "-controls.png"));
            }
            finally
            {
                window.Close();
            }
        }
    });

    private static async Task<(Window Window, BattleMapView View, BattleMapViewModel ViewModel)>
        ShowBattleMapWithSquad(int width = 1280, int height = 800, bool deploy = true)
    {
        var services = ((App)Application.Current!).ServiceProvider!;
        var viewModel = services.GetRequiredService<BattleMapViewModel>();
        var game = CreateClientGame(services);

        var units = await LocalGameFixture.LoadBundledUnitsAsync(services);
        var player = new Player(Guid.NewGuid(), "Local", PlayerControlType.Human, "#4A90D9");

        viewModel.Game = game;
        game.SetBattleMap(services.GetRequiredService<IBattleMapFactory>()
            .GenerateMap(12, 10, new SingleTerrainGenerator(12, 10, new ClearTerrain())));
        game.JoinGameWithUnits(player, units.Take(4).ToList(), []);
        game.HandleCommand(new JoinGameCommand
        {
            GameOriginId = Guid.NewGuid(),
            PlayerId = player.Id,
            PlayerName = player.Name,
            Tint = player.Tint,
            Units = units.Take(4).ToList(),
            PilotAssignments = []
        });

        if (deploy)
        {
            // Undeployed units read as "Off map" and draw nothing, so the map underneath the HUD
            // would stay empty. Put each one on its own hex.
            var hex = 1;
            foreach (var unit in game.Players.SelectMany(p => p.Units))
            {
                game.HandleCommand(new DeployUnitCommand
                {
                    GameOriginId = Guid.NewGuid(),
                    PlayerId = player.Id,
                    UnitId = unit.Id,
                    Position = new HexCoordinates(hex, hex).ToData(),
                    Direction = 0
                });
                hex++;
            }
        }

        var view = new BattleMapView();
        ((IBaseView)view).ViewModel = viewModel;
        view.DataContext = viewModel;
        var window = new Window { Width = width, Height = height, Content = view };
        window.Show();
        Settle(window);
        return (window, view, viewModel);
    }

    private static ClientGame CreateClientGame(IServiceProvider services) => new(
        services.GetRequiredService<IRulesProvider>(),
        services.GetRequiredService<IMechFactory>(),
        services.GetRequiredService<ICommandPublisher>(),
        services.GetRequiredService<IToHitCalculator>(),
        services.GetRequiredService<IPilotingSkillCalculator>(),
        services.GetRequiredService<IConsciousnessCalculator>(),
        services.GetRequiredService<IHeatEffectsCalculator>(),
        services.GetRequiredService<IBattleMapFactory>(),
        services.GetRequiredService<IHashService>(),
        services.GetRequiredService<ILogger<ClientGame>>());

    /// <summary>
    /// Hex tiles are decoded off the UI thread, so a single settle captures the map before any of
    /// them arrive. Pump the dispatcher a few times, yielding in between, so the screenshot shows
    /// the terrain rather than bare canvas.
    /// </summary>
    private static async Task SettleUntilTilesDecode(Window window)
    {
        for (var i = 0; i < 25; i++)
        {
            Settle(window);
            await Task.Delay(40);
        }

        Settle(window);
    }

    private static void Settle(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    private static void Save(WriteableBitmap frame, string fileName)
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "screenshots");
        Directory.CreateDirectory(directory);
        frame.Save(Path.Combine(directory, fileName));
    }





    /// <summary>
    /// The bar was capped at 700 points, so a wide window showed a cut off card and then empty
    /// bottom edge with the drawer sitting in it. It should use the width it has, and give back
    /// only what the drawer needs while the drawer is open.
    /// </summary>
    [Fact]
    public Task SquadBar_UsesTheFullWidth_AndYieldsOnlyToTheOpenDrawer() => HarnessSession.Run(async () =>
    {
        var (window, view, viewModel) = await ShowBattleMapWithSquad(1280, 800);
        try
        {
            var bar = SquadBar(view);
            var drawer = view.GetVisualDescendants().OfType<StackPanel>()
                .First(p => p.Name == "MapControlsDrawer");

            var closedWidth = bar.Bounds.Width;
            closedWidth.ShouldBeGreaterThan(1100,
                $"a closed drawer should leave the bar the whole width, got {closedWidth}");

            viewModel.ToggleMapControlsDrawer();
            Settle(window);

            var openWidth = bar.Bounds.Width;
            openWidth.ShouldBeLessThan(closedWidth, "an open drawer should push the bar back");
            drawer.Bounds.Width.ShouldBeGreaterThan(0);

            // The bar must stop before the drawer starts, whatever the drawer measured.
            var barRight = (bar.TranslatePoint(new Point(bar.Bounds.Width, 0), view)?.X) ?? 0;
            var drawerLeft = (drawer.TranslatePoint(new Point(0, 0), view)?.X) ?? 0;
            barRight.ShouldBeLessThanOrEqualTo(drawerLeft,
                $"the bar ends at {barRight} and the drawer starts at {drawerLeft}");

            viewModel.ToggleMapControlsDrawer();
            Settle(window);
            bar.Bounds.Width.ShouldBe(closedWidth, "closing it should give the width back");
        }
        finally
        {
            window.Close();
        }
    });

    private static ScrollViewer SquadBar(BattleMapView view) =>
        view.GetVisualDescendants()
            .OfType<ScrollViewer>()
            .First(sv => sv.GetVisualDescendants().OfType<UnitStatusBarItem>().Any());


    [Fact]
    public Task SquadCard_ShowsEveryFieldInFull() => HarnessSession.Run(async () =>
    {
        var (window, view, _) = await ShowBattleMapWithSquad();
        try
        {
            var card = view.GetVisualDescendants().OfType<UnitStatusBarItem>().First();
            var clipped = card.GetVisualDescendants().OfType<TextBlock>()
                .Where(t => !string.IsNullOrEmpty(t.Text))
                .Where(t => t.Bounds.Width + 0.5 < t.DesiredSize.Width)
                .Select(t => $"'{t.Text}' got {t.Bounds.Width:F0} of {t.DesiredSize.Width:F0}")
                .ToList();

            clipped.ShouldBeEmpty($"nothing on the card should be cut off: {string.Join("; ", clipped)}");
        }
        finally
        {
            window.Close();
        }
    });

    [Fact]
    public Task SquadBar_ShowsItsScrollBar_OnlyWhenTheSquadOverflows() => HarnessSession.Run(async () =>
    {
        var (wide, wideView, _) = await ShowBattleMapWithSquad(1280, 800);
        try
        {
            HorizontalScrollBar(SquadBar(wideView)).IsEffectivelyVisible
                .ShouldBeFalse("four cards fit here, so there is nothing to scroll");
        }
        finally
        {
            wide.Close();
        }

        var (narrow, narrowView, _) = await ShowBattleMapWithSquad(620, 800);
        try
        {
            var bar = SquadBar(narrowView);
            bar.Extent.Width.ShouldBeGreaterThan(bar.Viewport.Width, "the cards should overflow here");
            HorizontalScrollBar(bar).IsEffectivelyVisible
                .ShouldBeTrue("the player needs to see there is more squad off to the side");
        }
        finally
        {
            narrow.Close();
        }
    });

    [Fact]
    public Task SquadBar_KeepsItsScrollBarClearOfTheCards() => HarnessSession.Run(async () =>
    {
        var (window, view, _) = await ShowBattleMapWithSquad(620, 800);
        try
        {
            var bar = SquadBar(view);
            var card = view.GetVisualDescendants().OfType<UnitStatusBarItem>().First();
            var scrollBar = HorizontalScrollBar(bar);

            var cardBottom = (card.TranslatePoint(new Point(0, card.Bounds.Height), bar)?.Y) ?? 0;
            var scrollBarTop = (scrollBar.TranslatePoint(new Point(0, 0), bar)?.Y) ?? 0;

            scrollBarTop.ShouldBeGreaterThanOrEqualTo(cardBottom - 0.5,
                $"the scrollbar starts at {scrollBarTop} and the card ends at {cardBottom}, "
                + "so it would be drawn across the damage bars");
        }
        finally
        {
            window.Close();
        }
    });

    [Fact]
    public Task SquadBar_ScrollsOnTheWheel() => HarnessSession.Run(async () =>
    {
        var (window, view, _) = await ShowBattleMapWithSquad(620, 800);
        try
        {
            var bar = SquadBar(view);
            bar.Offset.X.ShouldBe(0);

            Wheel(bar, -1);
            Settle(window);

            bar.Offset.X.ShouldBeGreaterThan(0, "a wheel notch should move the strip sideways");
        }
        finally
        {
            window.Close();
        }
    });

    /// <summary>
    /// The scrollbar scrolls the strip itself. Panning as well would move it twice per drag, so a
    /// press that lands on the scrollbar must not start a pan.
    /// </summary>
    [Fact]
    public Task SquadBar_DoesNotAlsoPan_WhenTheScrollBarIsDragged() => HarnessSession.Run(async () =>
    {
        var (window, view, _) = await ShowBattleMapWithSquad(620, 800);
        try
        {
            var bar = SquadBar(view);
            var scrollBar = HorizontalScrollBar(bar);

            // Leftward, which is the direction that has somewhere to go from a zero offset.
            // Raised on the scrollbar so it tunnels through the bar the way real input does.
            scrollBar.RaiseEvent(Press(bar, scrollBar, 300));
            scrollBar.RaiseEvent(Move(bar, scrollBar, 100));
            Settle(window);

            bar.Offset.X.ShouldBe(0,
                "the behavior should have left this drag to the scrollbar");
        }
        finally
        {
            window.Close();
        }
    });

    [Fact]
    public Task SquadBar_DoesNotScrollPastItsContent() => HarnessSession.Run(async () =>
    {
        var (window, view, _) = await ShowBattleMapWithSquad(620, 800);
        try
        {
            var bar = SquadBar(view);
            var max = bar.Extent.Width - bar.Viewport.Width;

            for (var i = 0; i < 50; i++) Wheel(bar, -1);
            Settle(window);
            bar.Offset.X.ShouldBe(max, 0.5, "it should stop at the last card");

            for (var i = 0; i < 50; i++) Wheel(bar, 1);
            Settle(window);
            bar.Offset.X.ShouldBe(0, 0.5, "and at the first one going back");
        }
        finally
        {
            window.Close();
        }
    });

    [Fact]
    public Task SquadBar_BringsTheActiveUnitIntoView() => HarnessSession.Run(async () =>
    {
        var (window, view, viewModel) = await ShowBattleMapWithSquad(620, 800);
        try
        {
            var bar = SquadBar(view);
            bar.Offset.X.ShouldBe(0);

            var last = viewModel.LocalUnits.Last();
            var state = Substitute.For<IUiState>();
            state.SelectedUnit.Returns(last);
            state.CanSelectUnit(Arg.Any<IUnit>()).Returns(true);
            SetCurrentState(viewModel, state);
            viewModel.NotifySelectedUnitChanged();
            Settle(window);

            bar.Offset.X.ShouldBeGreaterThan(0,
                "the bar should have scrolled to the unit the player has to act with");
        }
        finally
        {
            window.Close();
        }
    });

    private static void Wheel(ScrollViewer bar, double delta) =>
        bar.RaiseEvent(new PointerWheelEventArgs(
            bar,
            new Pointer(0, PointerType.Mouse, true),
            bar,
            new Point(10, 10),
            0,
            new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.Other),
            KeyModifiers.None,
            new Vector(0, delta))
        {
            RoutedEvent = InputElement.PointerWheelChangedEvent
        });

    /// <summary>
    /// BattleMapViewModel takes its state through a private setter, the same way the presentation
    /// tests drive it.
    /// </summary>
    private static void SetCurrentState(BattleMapViewModel viewModel, IUiState state) =>
        typeof(BattleMapViewModel)
            .GetProperty(nameof(BattleMapViewModel.CurrentState))!
            .SetValue(viewModel, state);


    private static ScrollBar HorizontalScrollBar(ScrollViewer bar) =>
        bar.GetVisualDescendants().OfType<ScrollBar>()
            .First(sb => sb.Orientation == Orientation.Horizontal);

    private static PointerPressedEventArgs Press(ScrollViewer bar, Visual source, double x) =>
        new(source, new Pointer(1, PointerType.Mouse, true), bar, new Point(x, 100),
            0,
            new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed),
            KeyModifiers.None)
        {
            RoutedEvent = InputElement.PointerPressedEvent
        };

    private static PointerEventArgs Move(ScrollViewer bar, Visual source, double x) =>
        new(InputElement.PointerMovedEvent, source, new Pointer(1, PointerType.Mouse, true), bar,
            new Point(x, 100), 0,
            new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.Other),
            KeyModifiers.None);

    /// <summary>
    /// Dragging the cards themselves pans the strip. The press has to travel past the threshold
    /// first, so an ordinary click on a card is still a click.
    /// </summary>
    [Fact]
    public Task SquadBar_PansWhenTheCardsAreDragged() => HarnessSession.Run(async () =>
    {
        var (window, view, _) = await ShowBattleMapWithSquad(620, 800);
        try
        {
            var bar = SquadBar(view);
            var card = view.GetVisualDescendants().OfType<UnitStatusBarItem>().First();

            card.RaiseEvent(Press(bar, card, 300));
            card.RaiseEvent(Move(bar, card, 298));
            Settle(window);
            bar.Offset.X.ShouldBe(0, "two points of travel is a click, not a drag");

            card.RaiseEvent(Move(bar, card, 200));
            Settle(window);
            bar.Offset.X.ShouldBe(100, 0.5, "past the threshold it should follow the pointer");
        }
        finally
        {
            window.Close();
        }
    });


    /// <summary>
    /// FitMap measured the canvas against itself. The canvas is given an explicit Width and Height
    /// covering the whole board, so the ratio was always one and the command could not zoom out.
    /// </summary>
    [Fact]
    public Task FitMap_ActuallyZoomsOut_WhenTheBoardIsBiggerThanTheWindow() => HarnessSession.Run(async () =>
    {
        var (window, view, viewModel) = await ShowBattleMapWithSquad(800, 450);
        try
        {
            var canvas = view.GetVisualDescendants().OfType<HexMap>().First();
            canvas.Width.ShouldBeGreaterThan(800, "the board should overflow this window");

            await viewModel.FitMapCommand.ExecuteAsync();
            Settle(window);

            var matrix = (canvas.RenderTransform as MatrixTransform)?.Matrix;
            matrix.ShouldNotBeNull();
            matrix.Value.M11.ShouldBeLessThan(1.0,
                "fitting a board wider than the window has to scale it down");
        }
        finally
        {
            window.Close();
        }
    });

    /// <summary>
    /// Both own the bottom edge. Pinned independently they overlapped: the bar is 108 tall and the
    /// buttons sat 20 off the bottom, inside it and centred over the middle cards, where they
    /// swallowed the clicks.
    /// </summary>
    [Fact]
    public Task PlayerActionButtons_SitAboveTheSquadBar() => HarnessSession.Run(async () =>
    {
        var (window, view, _) = await ShowBattleMapWithSquad(800, 450);
        try
        {
            var bar = SquadBar(view);
            var actions = view.GetVisualDescendants().OfType<StackPanel>()
                .First(sp => sp.Children.OfType<ItemsControl>().Any(c => c.Name == "MobileActionButtonsPanel"));

            var actionsBottom = (actions.TranslatePoint(new Point(0, actions.Bounds.Height), view)?.Y) ?? 0;
            var barTop = (bar.TranslatePoint(new Point(0, 0), view)?.Y) ?? 0;

            actionsBottom.ShouldBeLessThanOrEqualTo(barTop + 0.5,
                $"the actions end at {actionsBottom} and the bar starts at {barTop}");
        }
        finally
        {
            window.Close();
        }
    });
}
