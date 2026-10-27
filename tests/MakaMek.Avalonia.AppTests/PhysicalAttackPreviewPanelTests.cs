using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sanet.MakaMek.Avalonia;
using Sanet.MakaMek.Avalonia.Controls;
using Sanet.MakaMek.Avalonia.Controls.TemplatedControls;
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
/// The physical attack preview panel inside the real BattleMapView, over a real game in which an
/// Atlas has picked an adjacent Atlas as its punch target.
/// </summary>
public class PhysicalAttackPreviewPanelTests
{
    [Fact]
    public Task PhysicalAttackPreviewPanel_ShowsTheToHitPreview_AndClosesWithoutLosingTheTarget() =>
        HarnessSession.Run(async () =>
        {
            // Arrange
            var (window, view, viewModel, state, target) = await ShowBattleMapWithTargetSelected();
            try
            {
                // Assert
                viewModel.IsPhysicalAttackPreviewVisible.ShouldBeTrue();
                viewModel.PhysicalAttackPreview.ShouldNotBeNull();
                viewModel.PhysicalAttackPreview.Target.ShouldBe(target);

                var panel = view.FindControl<PhysicalAttackPreviewPanel>("PhysicalAttackPreviewPanel")
                    .ShouldNotBeNull();
                var gamePanel = panel.GetVisualDescendants().OfType<GamePanel>().Single();
                gamePanel.IsEffectivelyVisible.ShouldBeTrue();

                var texts = panel.GetVisualDescendants().OfType<TextBlock>()
                    .Select(textBlock => textBlock.Text)
                    .ToList();
                texts.ShouldContain("Punch");
                texts.ShouldContain(viewModel.PhysicalAttackPreview.TargetNumberText);

                // Act
                viewModel.ClosePhysicalAttackPreviewCommand();
                Settle(window);

                // Assert the panel hides but the target selection stays.
                viewModel.IsPhysicalAttackPreviewVisible.ShouldBeFalse();
                state.SelectedTarget.ShouldBe(target);
            }
            finally
            {
                window.Close();
            }
        });

    private static async Task<(Window Window, BattleMapView View, BattleMapViewModel ViewModel,
        PhysicalAttackState State, Sanet.MakaMek.Core.Models.Units.IUnit Target)>
        ShowBattleMapWithTargetSelected()
    {
        var services = ((App)Application.Current!).ServiceProvider!;
        var viewModel = ActivatorUtilities.CreateInstance<BattleMapViewModel>(
            services, new TestDispatcherService());

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

        var attackerPosition = new HexCoordinates(5, 5);
        var targetPosition = attackerPosition.GetNeighbour(HexDirection.Top);
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
                    Position = (isLocal ? attackerPosition : targetPosition).ToData(),
                    Direction = isLocal ? (int)HexDirection.Top : (int)HexDirection.Bottom
                });
                unit.Move(MovementPath.CreateSingleSegmentPath(unit.Position!), null, true);
            }
        }

        game.HandleCommand(new ChangePhaseCommand { GameOriginId = Guid.NewGuid(), Phase = PhaseNames.PhysicalAttack });
        game.HandleCommand(new ChangeActivePlayerCommand
        {
            GameOriginId = Guid.NewGuid(),
            PlayerId = player.Id,
            UnitsToPlay = 1
        });

        var view = new BattleMapView();
        ((IBaseView)view).ViewModel = viewModel;
        view.DataContext = viewModel;
        var window = new Window { Width = 400, Height = 800, Content = view };
        window.Show();
        Settle(window);

        var state = viewModel.CurrentState.ShouldBeOfType<PhysicalAttackState>();
        state.HandleUnitSelectionFromList(UnitOf(game, player.Id));
        var punch = viewModel.LocalizationService.GetString("PhysicalAttackType_Punch");
        state.GetAvailableActions().First(action => action.Label == punch).OnExecute();
        var target = UnitOf(game, opponentId);
        state.HandleUnitSelectionFromList(target);
        Settle(window);

        return (window, view, viewModel, state, target);
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
