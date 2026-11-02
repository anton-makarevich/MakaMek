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
                texts.ShouldContain(attacker.Name);
                var punch = viewModel.PhysicalAttackOptions
                    .First(option => option.AttackType == PhysicalAttackType.Punch);
                // The to-hit number lives only in the breakdown flyout now.
                texts.ShouldNotContain(punch.TargetNumberText);

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
                // side arc the left arm and the both-arms combination are shown but disabled, so
                // only the right arm can be checked.
                var checkBoxes = panel.GetVisualDescendants().OfType<CheckBox>().ToList();
                checkBoxes.Count.ShouldBe(6);
                checkBoxes.Count(checkBox => checkBox.IsEnabled).ShouldBe(1);

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

                // Every row keeps its info flyout, including the disabled ones, and each disabled
                // limb option explains its own reason.
                var buttons = panel.GetVisualDescendants().OfType<Button>()
                    .Where(button => button.Flyout != null)
                    .ToList();
                foreach (var option in viewModel.PhysicalAttackOptions)
                    buttons.Count(button => ReferenceEquals(button.DataContext, option)).ShouldBe(1);

                var leftArm = punch.LimbOptions.Single(option => option.Label == "Left Arm");
                leftArm.IsEnabled.ShouldBeFalse();
                leftArm.RestrictionReason.ShouldNotBeNullOrWhiteSpace();
                buttons.Count(button => ReferenceEquals(button.DataContext, leftArm)).ShouldBe(1);

                // A disabled limb keeps a clickable info button, including on a row that is
                // disabled as a whole, otherwise its reason flyout could never be opened.
                var kickLeg = kick.LimbOptions[0];
                foreach (var limb in new[] { leftArm, kickLeg })
                {
                    var info = buttons.Single(button => ReferenceEquals(button.DataContext, limb));
                    info.IsEffectivelyVisible.ShouldBeTrue();
                    info.IsEnabled.ShouldBeTrue();
                }
            }
            finally
            {
                window.Close();
            }
        });

    [Fact]
    public Task PhysicalAttackDeclarationPanel_KeepsProbabilityAndInfoOnTheAttackTypeRow() =>
        HarnessSession.Run(async () =>
        {
            // Arrange
            var (window, view, viewModel, _, _, _) = await ShowBattleMapWithTargetSelected();
            try
            {
                var panel = view.FindControl<PhysicalAttackDeclarationPanel>("PhysicalAttackDeclarationPanel")
                    .ShouldNotBeNull();
                var punch = viewModel.PhysicalAttackOptions
                    .First(option => option.AttackType == PhysicalAttackType.Punch);

                var descriptors = panel.GetVisualDescendants().OfType<TextBlock>()
                    .Where(textBlock => ReferenceEquals(textBlock.DataContext, punch))
                    .ToList();
                var attackName = descriptors.Single(textBlock => textBlock.Text == punch.AttackTypeName);
                var probability = descriptors.Single(textBlock => textBlock.Text == punch.HitProbabilityText);
                var info = panel.GetVisualDescendants().OfType<Button>()
                    .Single(button => ReferenceEquals(button.DataContext, punch) && button.Flyout != null);

                // Assert the row reads like a weapon row: the name on the left and, on the same
                // line, the hit probability followed by the breakdown button.
                var nameTop = attackName.TranslatePoint(new Point(0, 0), panel)!.Value;
                var probabilityTop = probability.TranslatePoint(new Point(0, 0), panel)!.Value;
                var infoTop = info.TranslatePoint(new Point(0, 0), panel)!.Value;
                var nameCenter = nameTop.Y + attackName.Bounds.Height / 2;

                Math.Abs(nameCenter - (probabilityTop.Y + probability.Bounds.Height / 2))
                    .ShouldBeLessThan(8);
                Math.Abs(nameCenter - (infoTop.Y + info.Bounds.Height / 2)).ShouldBeLessThan(8);
                probabilityTop.X.ShouldBeGreaterThan(nameTop.X + attackName.Bounds.Width);
                infoTop.X.ShouldBeGreaterThan(probabilityTop.X);
            }
            finally
            {
                window.Close();
            }
        });

    [Fact]
    public Task PhysicalAttackDeclarationPanel_ShowsDamageOnEachLimbOption() =>
        HarnessSession.Run(async () =>
        {
            // Arrange
            var (window, view, viewModel, _, _, _) = await ShowBattleMapWithTargetSelected();
            try
            {
                var panel = view.FindControl<PhysicalAttackDeclarationPanel>("PhysicalAttackDeclarationPanel")
                    .ShouldNotBeNull();
                var punch = viewModel.PhysicalAttackOptions
                    .First(option => option.AttackType == PhysicalAttackType.Punch);
                var bothArms = punch.LimbOptions.Single(option => option.Label == "Both Arms");
                bothArms.HasDamage.ShouldBeTrue();

                // The damage belongs to the limb option: it sits next to that option's checkbox.
                var damageText = panel.GetVisualDescendants().OfType<TextBlock>()
                    .Single(textBlock => ReferenceEquals(textBlock.DataContext, bothArms)
                                         && textBlock.Text == bothArms.DamageText);
                var checkbox = panel.GetVisualDescendants().OfType<CheckBox>()
                    .Single(checkBox => ReferenceEquals(checkBox.DataContext, bothArms));

                var damageTop = damageText.TranslatePoint(new Point(0, 0), panel)!.Value;
                var checkboxTop = checkbox.TranslatePoint(new Point(0, 0), panel)!.Value;
                Math.Abs((damageTop.Y + damageText.Bounds.Height / 2)
                         - (checkboxTop.Y + checkbox.Bounds.Height / 2)).ShouldBeLessThan(8);
                damageTop.X.ShouldBeGreaterThan(checkboxTop.X);

                // The attack type row itself carries no damage value any more.
                panel.GetVisualDescendants().OfType<TextBlock>()
                    .Any(textBlock => ReferenceEquals(textBlock.DataContext, punch)
                                      && textBlock.Text == bothArms.DamageText)
                    .ShouldBeFalse();
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
