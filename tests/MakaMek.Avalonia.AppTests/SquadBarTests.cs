using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using Sanet.MakaMek.Avalonia;
using Sanet.MakaMek.Avalonia.Controls;
using Sanet.MakaMek.Avalonia.Views;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Factories;
using Sanet.MakaMek.Core.Models.Game.Phases;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Units.Pilots;
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
/// The squad bar inside the real BattleMapView, over a real game holding units from the
/// repository's own data folder.
///
/// BattleMapViewModelTests covers what a tile click does to the selection. What it cannot cover is
/// whether the view binds a tile to that at all, and whether the bar and the buttons above it share
/// the bottom edge without covering each other.
/// </summary>
public class SquadBarTests
{
    [Fact]
    public Task SquadBar_ShowsATilePerLocalUnit_WithTheNextUnitButtonInside() => HarnessSession.Run(async () =>
    {
        var (window, view, viewModel) = await ShowBattleMapWithSquad();
        try
        {
            viewModel.LocalUnits.Count().ShouldBe(4, "the fixture joins one player with four units");

            var bar = view.FindControl<Border>("SquadBar").ShouldNotBeNull();
            bar.IsEffectivelyVisible.ShouldBeTrue();
            var tiles = bar.GetVisualDescendants().OfType<SquadUnitTile>().ToList();
            tiles.Select(tile => tile.DataContext).ShouldBe(viewModel.LocalUnits);

            var next = view.FindControl<Button>("NextUnitButton").ShouldNotBeNull();
            next.IsEffectivelyVisible.ShouldBeTrue();
            bar.GetVisualDescendants().ShouldContain(next, "next available belongs to the squad panel");
        }
        finally
        {
            window.Close();
        }
    });

    [Fact]
    public Task Tile_ShowsWhatIdentifiesTheUnit() => HarnessSession.Run(async () =>
    {
        var (window, view, viewModel) = await ShowBattleMapWithSquad();
        try
        {
            var unit = viewModel.LocalUnits.First();
            var tile = view.GetVisualDescendants().OfType<SquadUnitTile>().First();

            tile.FindControl<TextBlock>("PilotName")!.Text.ShouldBe(unit.Pilot!.Name);
            tile.FindControl<TextBlock>("Tonnage")!.Text.ShouldBe($"{unit.Tonnage}t");
            tile.FindControl<TextBlock>("Heat")!.Text.ShouldBe($"Heat {unit.CurrentHeat}");
            tile.FindControl<TextBlock>("Movement")!.Text
                .ShouldBe($"MP {unit.AvailableWalkingPoints}/{unit.AvailableRunningPoints}");
            tile.FindControl<TextBlock>("Jump")!.IsVisible.ShouldBe(unit.AvailableJumpingPoints > 0);

            var injuries = tile.FindControl<ProgressBar>("PilotInjuries")!;
            injuries.Maximum.ShouldBe(unit.Pilot.Health);
            injuries.Value.ShouldBe(unit.Pilot.Injuries);
            var armor = tile.FindControl<ProgressBar>("Armor")!;
            armor.Maximum.ShouldBe(unit.TotalMaxArmor);
            armor.Value.ShouldBe(unit.TotalCurrentArmor);
            var structure = tile.FindControl<ProgressBar>("Structure")!;
            structure.Maximum.ShouldBe(unit.TotalMaxStructure);
            structure.Value.ShouldBe(unit.TotalCurrentStructure);

            tile.FindControl<global::Avalonia.Controls.Shapes.Path>("NotOperationalMark")!.IsVisible
                .ShouldBeFalse("a fresh unit is operational");
        }
        finally
        {
            window.Close();
        }
    });

    [Fact]
    public Task ClickingATile_SelectsItsUnit_AndOutlinesOnlyThatTile() => HarnessSession.Run(async () =>
    {
        var (window, view, viewModel) = await ShowBattleMapWithSquad();
        try
        {
            viewModel.CurrentState.ShouldBeOfType<EndState>();
            var third = viewModel.LocalUnits.ElementAt(2);
            Tiles(view).ShouldAllBe(tile => !tile.IsActive);

            Click(window, Tiles(view)[2]);

            viewModel.SelectedUnit.ShouldBe(third);
            viewModel.IsRecordSheetExpanded.ShouldBeFalse("a tile click selects, it does not open the sheet");
            // Asked again: the bar rebuilds its tiles whenever LocalUnits is raised.
            Tiles(view).Select(tile => tile.IsActive).ShouldBe([false, false, true, false]);
        }
        finally
        {
            window.Close();
        }
    });

    [Fact]
    public Task NextUnitButton_SelectsAUnitAndCentersTheMapOnIt() => HarnessSession.Run(async () =>
    {
        var (window, view, viewModel) = await ShowBattleMapWithSquad();
        try
        {
            var map = view.FindControl<HexMap>("MapCanvas").ShouldNotBeNull();
            var before = map.RenderTransform?.Value;

            Click(window, view.FindControl<Button>("NextUnitButton")!);

            var selected = viewModel.SelectedUnit.ShouldNotBeNull();
            selected.ShouldBe(viewModel.LocalUnits.First());
            (map.RenderTransform?.Value == before).ShouldBeFalse("the view wires FocusUnit to the map");

            // The transform centres the map on the selected unit: its hex centre, run through the
            // map's render transform, lands in the middle of the visual parent's viewport (the parent
            // clips the oversized canvas, so it is the visible viewport).
            var position = selected.Position.ShouldNotBeNull("the selected unit is deployed");
            var hexCentre = new Point(
                position.Coordinates.H + HexCoordinatesPixelExtensions.HexWidth / 2,
                position.Coordinates.V + HexCoordinatesPixelExtensions.HexHeight / 2);
            var rendered = map.RenderTransform!.Value.Transform(hexCentre);
            var viewport = map.GetVisualParent()!.Bounds.Size;
            rendered.X.ShouldBe(viewport.Width / 2, 0.01, "the selected unit is centred horizontally");
            rendered.Y.ShouldBe(viewport.Height / 2, 0.01, "the selected unit is centred vertically");
        }
        finally
        {
            window.Close();
        }
    });

    [Fact]
    public Task BottomButtons_SitAboveTheSquadBar_OnANarrowScreen() => HarnessSession.Run(async () =>
    {
        var (window, view, _) = await ShowBattleMapWithSquad(width: 430, height: 880);
        try
        {
            var bar = view.FindControl<Border>("SquadBar").ShouldNotBeNull();
            var barTop = bar.TranslatePoint(new Point(0, 0), view)!.Value.Y;

            foreach (var button in view.GetVisualDescendants().OfType<Button>()
                         .Where(b => b.IsEffectivelyVisible && !bar.IsVisualAncestorOf(b)))
            {
                var bottom = button.TranslatePoint(new Point(0, button.Bounds.Height), view)!.Value.Y;
                bottom.ShouldBeLessThanOrEqualTo(barTop, $"{button.Name ?? button.Content} overlaps the squad bar");
            }
        }
        finally
        {
            window.Close();
        }
    });

    private static List<SquadUnitTile> Tiles(BattleMapView view) =>
        view.GetVisualDescendants().OfType<SquadUnitTile>().ToList();

    private static void Click(Window window, Control control)
    {
        var point = control.TranslatePoint(
            new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)!.Value;
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Settle(window);
    }

    private static async Task<(Window Window, BattleMapView View, BattleMapViewModel ViewModel)>
        ShowBattleMapWithSquad(int width = 1280, int height = 800)
    {
        var services = ((App)Application.Current!).ServiceProvider!;
        var viewModel = ActivatorUtilities.CreateInstance<BattleMapViewModel>(
            services, new TestDispatcherService());
        var game = CreateClientGame(services);

        var units = (await LocalGameFixture.LoadBundledUnitsAsync(services)).Take(4).ToList();
        var player = new Player(Guid.NewGuid(), "Local", PlayerControlType.Human, "#4A90D9");

        viewModel.Game = game;
        game.SetBattleMap(services.GetRequiredService<IBattleMapFactory>()
            .GenerateMap(12, 10, new SingleTerrainGenerator(12, 10, new ClearTerrain())));
        _ = game.JoinGameWithUnits(player, units, []);
        game.HandleCommand(new JoinGameCommand
        {
            GameOriginId = Guid.NewGuid(),
            PlayerId = player.Id,
            PlayerName = player.Name,
            Tint = player.Tint,
            Units = units,
            PilotAssignments = []
        });

        var hex = 1;
        foreach (var unit in game.Players.SelectMany(p => p.Units))
        {
            unit.AssignPilot(new MechWarrior("Ann", $"Lee {hex}"));
            game.HandleCommand(new DeployUnitCommand
            {
                GameOriginId = Guid.NewGuid(),
                PlayerId = player.Id,
                UnitId = unit.Id,
                Position = new HexCoordinates(hex + 3, hex + 2).ToData(),
                Direction = 0
            });
            hex++;
        }

        // The end phase lets any unit be selected, so a tile click has something to do.
        game.HandleCommand(new ChangePhaseCommand { GameOriginId = Guid.NewGuid(), Phase = PhaseNames.End });
        game.HandleCommand(new ChangeActivePlayerCommand
        {
            GameOriginId = Guid.NewGuid(),
            PlayerId = player.Id,
            UnitsToPlay = 0
        });

        var view = new BattleMapView();
        ((IBaseView)view).ViewModel = viewModel;
        view.DataContext = viewModel;
        var window = new Window { Width = width, Height = height, Content = view };
        window.Show();

        // Unit images and hex tiles are decoded off the UI thread.
        for (var i = 0; i < 15; i++)
        {
            Settle(window);
            await Task.Delay(40);
        }

        Settle(window);
        return (window, view, viewModel);
    }

    private static ClientGame CreateClientGame(IServiceProvider services) =>
        services.GetRequiredService<IGameFactory>()
            .CreateClientGame(services.GetRequiredService<ICommandPublisher>());

    private static void Settle(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }
}
