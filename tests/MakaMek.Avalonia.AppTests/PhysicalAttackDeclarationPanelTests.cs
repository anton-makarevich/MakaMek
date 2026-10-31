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
using Sanet.MakaMek.Core.Models.Units;
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
/// The physical attack declaration dialog inside the real BattleMapView, over a real game in which
/// an Atlas has selected an adjacent Atlas as its target.
/// </summary>
public class PhysicalAttackDeclarationPanelTests
{
    [Fact]
    public Task PhysicalAttackDeclarationPanel_ShowsTheRows_AndClosesBackToTargetSelection() =>
        HarnessSession.Run(async () =>
        {
            // Arrange
            var (window, view, viewModel, state, attacker, target) =
                await ShowBattleMapWithTargetSelected();
            try
            {
                // Assert the dialog opens straight from target selection, without an attack-type
                // action.
                viewModel.IsPhysicalAttackDeclarationVisible.ShouldBeTrue();
                viewModel.PhysicalAttackOptions.ShouldNotBeEmpty();
                viewModel.PhysicalAttackTarget.ShouldBe(target);

                var panel = view.FindControl<PhysicalAttackDeclarationPanel>("PhysicalAttackDeclarationPanel")
                    .ShouldNotBeNull();
                var gamePanel = panel.GetVisualDescendants().OfType<GamePanel>().Single();
                gamePanel.IsEffectivelyVisible.ShouldBeTrue();

                var texts = panel.GetVisualDescendants().OfType<TextBlock>()
                    .Select(textBlock => textBlock.Text)
                    .ToList();
                texts.ShouldContain("Punch");
                texts.ShouldContain("Kick");
                texts.ShouldContain("Push");
                texts.ShouldContain("Both Arms");
                texts.ShouldContain("Left Leg");
                var punch = viewModel.PhysicalAttackOptions
                    .First(option => option.AttackType == PhysicalAttackType.Punch);
                texts.ShouldContain(punch.TargetNumberText);

                // Checking a limb option selects the attack row automatically.
                var rightArm = punch.LimbOptions.Single(option => option.Label == "Right Arm");
                var limbItems = panel.GetVisualDescendants().OfType<ItemsControl>()
                    .Single(items => ReferenceEquals(items.DataContext, punch));
                limbItems.Bounds.Width.ShouldBeGreaterThan(0);

                var rightArmCheckBox = panel.GetVisualDescendants().OfType<CheckBox>()
                    .Single(checkBox => ReferenceEquals(checkBox.DataContext, rightArm));
                rightArmCheckBox.IsEffectivelyVisible.ShouldBeTrue();
                rightArmCheckBox.IsChecked = true;
                Settle(window);

                punch.IsSelected.ShouldBeTrue();
                punch.SelectedVariant.ShouldBe(rightArm.Variant);
                state.SelectedAttackType.ShouldBe(PhysicalAttackType.Punch);

                // Act: close through the panel's own close binding.
                gamePanel.CloseCommand.ShouldNotBeNull();
                gamePanel.CloseCommand.Execute(null);
                Settle(window);

                // Assert the dialog hides, the target is cleared and the attacker is kept.
                viewModel.IsPhysicalAttackDeclarationVisible.ShouldBeFalse();
                viewModel.PhysicalAttackOptions.ShouldBeEmpty();
                viewModel.PhysicalAttackTarget.ShouldBeNull();
                state.SelectedTarget.ShouldBeNull();
                state.CurrentStep.ShouldBe(PhysicalAttackStep.TargetSelection);
                state.Attacker.ShouldBe(attacker);
            }
            finally
            {
                window.Close();
            }
        });

    [Fact]
    public Task PhysicalAttackDeclarationPanel_DisablesUnavailableRows_AndKeepsTheirInfoAccessible() =>
        HarnessSession.Run(async () =>
        {
            // Arrange: the target is in the right-side arc, so only the right arm can punch and a
            // kick is outside the feet arc.
            var (window, view, viewModel, _, _, _) =
                await ShowBattleMapWithTargetSelected(HexDirection.BottomRight);
            try
            {
                var panel = view.FindControl<PhysicalAttackDeclarationPanel>("PhysicalAttackDeclarationPanel")
                    .ShouldNotBeNull();

                // Only limb options are checkboxes; the attack rows themselves have none. In the
                // side arc only the right arm reaches, so there is exactly one checkbox.
                var checkBoxes = panel.GetVisualDescendants().OfType<CheckBox>().ToList();
                checkBoxes.Count.ShouldBe(1);
                checkBoxes[0].IsEnabled.ShouldBeTrue();

                var punch = viewModel.PhysicalAttackOptions
                    .First(option => option.AttackType == PhysicalAttackType.Punch);
                var kick = viewModel.PhysicalAttackOptions
                    .First(option => option.AttackType == PhysicalAttackType.Kick);
                punch.IsEnabled.ShouldBeTrue();
                punch.HitProbabilityText.ShouldNotBe("-");
                kick.IsEnabled.ShouldBeFalse();
                kick.HitProbabilityText.ShouldBe("-");

                var texts = panel.GetVisualDescendants().OfType<TextBlock>()
                    .Select(textBlock => textBlock.Text)
                    .ToList();
                texts.ShouldContain(punch.HitProbabilityText);
                texts.ShouldContain("-");

                // Every row keeps its info flyout, including the disabled ones.
                panel.GetVisualDescendants().OfType<Button>()
                    .Count(button => button.Flyout != null)
                    .ShouldBe(3);
            }
            finally
            {
                window.Close();
            }
        });

    private static async Task<(Window Window, BattleMapView View, BattleMapViewModel ViewModel,
        PhysicalAttackState State, IUnit Attacker, IUnit Target)>
        ShowBattleMapWithTargetSelected(HexDirection targetDirection = HexDirection.Top)
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
        var targetPosition = attackerPosition.GetNeighbour(targetDirection);
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
        var attacker = UnitOf(game, player.Id);
        var target = UnitOf(game, opponentId);
        state.HandleUnitSelectionFromList(attacker);
        state.HandleUnitSelectionFromList(target);
        Settle(window);

        return (window, view, viewModel, state, attacker, target);
    }

    private static IUnit UnitOf(ClientGame game, Guid playerId) =>
        game.Players.First(player => player.Id == playerId).Units.First();

    private static void Settle(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }
}
