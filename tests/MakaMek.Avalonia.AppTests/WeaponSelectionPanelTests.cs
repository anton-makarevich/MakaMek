using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
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
/// The weapon selection panel inside the real BattleMapView, over a real game in which an Atlas
/// has picked another Atlas as its target. The panel and the round buttons share the right edge
/// of the map, so which of them is drawn on top is decided by the view alone.
/// </summary>
public class WeaponSelectionPanelTests
{
    [Fact]
    public Task WeaponSelectionPanel_CoversTheRoundButtonsItOverlaps() => HarnessSession.Run(async () =>
    {
        // Arrange
        var (window, view, viewModel) = await ShowBattleMapWithTargetSelected(400, 800);
        try
        {
            viewModel.IsWeaponSelectionVisible.ShouldBeTrue();
            var panel = view.FindControl<WeaponSelectionPanel>("WeaponSelectionPanel").ShouldNotBeNull();
            var content = panel.GetVisualChildren().OfType<Control>().First();
            var panelArea = new Rect(content.TranslatePoint(default, window)!.Value, content.Bounds.Size);

            // Act
            var overlappedButtonCentres = view.GetVisualDescendants().OfType<Button>()
                .Where(button => button.Classes.Contains("actionButton")
                                 && button.IsEffectivelyVisible
                                 && !panel.IsVisualAncestorOf(button))
                .Select(button => button.TranslatePoint(
                    new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), window)!.Value)
                .Where(panelArea.Contains)
                .ToList();

            // Assert
            overlappedButtonCentres.ShouldNotBeEmpty("at this size the panel reaches down to the round buttons");
            foreach (var centre in overlappedButtonCentres)
            {
                var topmost = window.GetVisualAt(centre).ShouldNotBeNull();
                panel.IsVisualAncestorOf(topmost).ShouldBeTrue(
                    $"{topmost.GetType().Name} at {centre} is drawn over the weapon selection panel");
            }
        }
        finally
        {
            window.Close();
        }
    });

    private static async Task<(Window Window, BattleMapView View, BattleMapViewModel ViewModel)>
        ShowBattleMapWithTargetSelected(int width, int height)
    {
        var services = ((App)Application.Current!).ServiceProvider!;
        var viewModel = ActivatorUtilities.CreateInstance<BattleMapViewModel>(
            services, new TestDispatcherService());

        // No server runs here, so the join the game sends is handed back to it as a server would.
        JoinGameCommand? sentJoin = null;
        var publisher = Substitute.For<ICommandPublisher>();
        publisher.PublishCommand(Arg.Do<JoinGameCommand>(command => sentJoin = command));
        var game = services.GetRequiredService<IGameFactory>().CreateClientGame(publisher);

        var atlas = (await LocalGameFixture.LoadBundledUnitsAsync(services)).First(unit => unit.Model == "AS7-D");
        var player = new Player(Guid.NewGuid(), "Local", PlayerControlType.Human, "#4A90D9");
        var opponentId = Guid.NewGuid();

        viewModel.Game = game;
        game.SetBattleMap(services.GetRequiredService<IBattleMapFactory>()
            .GenerateMap(12, 10, new SingleTerrainGenerator(12, 10, new ClearTerrain())));
        _ = game.JoinGameWithUnits(player, [atlas with { Id = Guid.NewGuid() }], []);
        game.HandleCommand(sentJoin.ShouldNotBeNull() with { GameOriginId = Guid.NewGuid() });
        game.HandleCommand(new JoinGameCommand
        {
            GameOriginId = Guid.NewGuid(),
            PlayerId = opponentId,
            PlayerName = "Opponent",
            Tint = "#D94A4A",
            Units = [atlas with { Id = Guid.NewGuid() }],
            PilotAssignments = []
        });

        foreach (var owner in game.Players)
        {
            var isLocal = owner.Id == player.Id;
            foreach (var unit in owner.Units)
            {
                unit.AssignPilot(new MechWarrior("Ann", "Lee"));
                game.HandleCommand(new DeployUnitCommand
                {
                    GameOriginId = Guid.NewGuid(),
                    PlayerId = owner.Id,
                    UnitId = unit.Id,
                    Position = new HexCoordinates(5, isLocal ? 6 : 4).ToData(),
                    Direction = isLocal ? 0 : 3
                });
                unit.Move(MovementPath.CreateSingleSegmentPath(unit.Position!), null, true);
            }
        }

        game.HandleCommand(new ChangePhaseCommand { GameOriginId = Guid.NewGuid(), Phase = PhaseNames.WeaponsAttack });
        game.HandleCommand(new ChangeActivePlayerCommand
        {
            GameOriginId = Guid.NewGuid(),
            PlayerId = player.Id,
            UnitsToPlay = 1
        });

        var view = new BattleMapView();
        ((IBaseView)view).ViewModel = viewModel;
        view.DataContext = viewModel;
        var window = new Window { Width = width, Height = height, Content = view };
        window.Show();
        Settle(window);

        var state = viewModel.CurrentState.ShouldBeOfType<WeaponsAttackState>();
        state.HandleUnitSelectionFromList(UnitOf(game, player.Id));
        var selectTarget = viewModel.LocalizationService.GetString("Action_SelectTarget");
        state.GetAvailableActions().First(action => action.Label == selectTarget).OnExecute();
        state.HandleUnitSelectionFromList(UnitOf(game, opponentId));
        Settle(window);

        return (window, view, viewModel);
    }

    private static Sanet.MakaMek.Core.Models.Units.IUnit UnitOf(ClientGame game, Guid playerId) =>
        game.Players.First(player => player.Id == playerId).Units.First();

    private static void Settle(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }
}
