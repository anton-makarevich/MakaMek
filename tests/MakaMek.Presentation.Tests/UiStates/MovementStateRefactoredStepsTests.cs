using System.Reactive.Concurrency;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Sanet.MakaMek.Assets.Services;
using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Data.Game.Mechanics.PhysicalAttacks;
using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Data.Units.Components;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Mechs.Falling;
using Sanet.MakaMek.Core.Models.Game.Mechanics.PhysicalAttack;
using Sanet.MakaMek.Core.Models.Game.Phases;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components.Engines;
using Sanet.MakaMek.Core.Models.Units.Components.Internal;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Core.Models.Units.Pilots;
using Sanet.MakaMek.Core.Tests.Utils;
using Sanet.MakaMek.Core.Utils;
using Sanet.Localization;
using Sanet.MakaMek.Map.Factories;
using Sanet.MakaMek.Map.Generators;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Models.Highlights;
using Sanet.MakaMek.Map.Models.Terrains;
using Sanet.MakaMek.Presentation.UiStates;
using Sanet.MakaMek.Presentation.ViewModels;
using Sanet.MakaMek.Services;
using Shouldly;

namespace Sanet.MakaMek.Presentation.Tests.UiStates;

/// <summary>
/// Regression tests for the two steps the cyclomatic-complexity refactor pulled out of
/// <c>MovementState</c>: the movement-attack candidate filter pipeline
/// (<c>FindMovementAttackCandidates</c>) and the post-fall resume state machine
/// (<c>ResumeMovementAfterFall</c>).
/// </summary>
public class MovementStateRefactoredStepsTests
{
    private static readonly IBattleMapFactory BattleMapFactory = new BattleMapFactory();

    private readonly MovementState _sut;
    private readonly IClientGame _game;
    private readonly Mech _attacker;
    private readonly Mech _enemy;
    private readonly Player _player;
    private readonly BattleMapViewModel _battleMapViewModel;
    private readonly IPilot _pilot = Substitute.For<IPilot>();
    private readonly IRulesProvider _rulesProvider = new TotalWarfareRulesProvider();
    private readonly IComponentProvider _componentProvider = new ClassicBattletechComponentProvider();
    private readonly ILocalizationService _localizationService = Substitute.For<ILocalizationService>();

    private readonly HexPosition _attackerPosition = new(new HexCoordinates(1, 2), HexDirection.Bottom);
    private readonly HexPosition _enemyPosition = new(new HexCoordinates(1, 4), HexDirection.Top);

    public MovementStateRefactoredStepsTests()
    {
        _localizationService.GetString(Arg.Any<string>()).Returns(call => call.Arg<string>());

        var dispatcherService = Substitute.For<IDispatcherService>();
        dispatcherService.Scheduler.Returns(Scheduler.Immediate);
        _battleMapViewModel = new BattleMapViewModel(
            Substitute.For<IImageService>(),
            Substitute.For<ITerrainAssetService>(),
            _localizationService,
            dispatcherService,
            _rulesProvider,
            Substitute.For<IPlatformService>());

        var mechFactory = new MechFactory(_rulesProvider, _componentProvider, _localizationService);
        _attacker = mechFactory.Create(CreateMechData());
        _enemy = mechFactory.Create(CreateMechData());

        _pilot.IsConscious.Returns(true);
        _attacker.AssignPilot(_pilot);
        var enemyPilot = Substitute.For<IPilot>();
        enemyPilot.IsConscious.Returns(true);
        _enemy.AssignPilot(enemyPilot);

        var playerId = Guid.NewGuid();
        _player = new Player(playerId, "Player1", PlayerControlType.Human);
        _player.AddUnit(_attacker);
        var enemyPlayer = new Player(Guid.NewGuid(), "Player2", PlayerControlType.Human);
        enemyPlayer.AddUnit(_enemy);

        _game = Substitute.For<IClientGame>();
        _game.Id.Returns(Guid.NewGuid());
        _game.BattleMap.Returns(BattleMapFactory.GenerateMap(
            11, 11, new SingleTerrainGenerator(11, 11, new ClearTerrain())));
        _game.Players.Returns([_player, enemyPlayer]);
        _game.AlivePlayers.Returns([_player, enemyPlayer]);
        _game.RulesProvider.Returns(_rulesProvider);
        _game.MovementAttackDeclarationValidator.Returns(new MovementAttackDeclarationValidator());
        _game.Logger.Returns(Substitute.For<ILogger>());
        _game.TurnPhase.Returns(PhaseNames.Movement);
        _game.PhaseStepState.Returns(new PhaseStepState(PhaseNames.Movement, _player, 1));
        _game.CanActivePlayerAct.Returns(true);
        _game.LocalPlayers.Returns([playerId]);

        _battleMapViewModel.Game = _game;
        _sut = new MovementState(_battleMapViewModel);
        BindViewModelCurrentStateTo(_sut, _battleMapViewModel);
    }

    [Fact]
    public void HandleMovementTypeSelection_WithAnEnemyThatHasNotMoved_DoesNotOfferItAsAnAttackCandidate()
    {
        // Arrange - a target must have finished acting before it can be charged
        DeployUnits();
        SelectAttackerAndWalk();

        // Assert
        EnemyHex().HasHighlight<AttackReachableHighlight>().ShouldBeFalse();
    }

    [Fact]
    public void HandleMovementTypeSelection_WithAShutdownEnemy_OffersItAsAnAttackCandidate()
    {
        // Arrange - a shutdown unit has finished acting even though it never moved
        DeployUnits();
        _enemy.Shutdown(new ShutdownData { Reason = ShutdownReason.Voluntary, Turn = 1 });
        SelectAttackerAndWalk();

        // Assert
        EnemyHex().HasHighlight<AttackReachableHighlight>().ShouldBeTrue();
    }

    [Fact]
    public void HandleMovementTypeSelection_WithAnUnconsciousEnemyPilot_OffersItAsAnAttackCandidate()
    {
        // Arrange - an unconscious pilot cannot move, so the unit is a legal target
        DeployUnits();
        var unconscious = Substitute.For<IPilot>();
        unconscious.IsConscious.Returns(false);
        _enemy.AssignPilot(unconscious);
        SelectAttackerAndWalk();

        // Assert
        EnemyHex().HasHighlight<AttackReachableHighlight>().ShouldBeTrue();
    }

    [Fact]
    public void HandleMovementTypeSelection_WithAnEnemyAlreadyMidAttack_DoesNotOfferItAsAnAttackCandidate()
    {
        // Arrange - a unit that already declared a movement attack cannot be charged
        DeployUnits();
        _enemy.Move(MovementPath.CreateSingleSegmentPath(_enemyPosition), null);
        _enemy.DeclarePhysicalAttack(new PhysicalAttackDeclarationData
        {
            AttackType = PhysicalAttackType.Charge,
            AttackerLimbs = [],
            TargetId = _attacker.Id
        });
        SelectAttackerAndWalk();

        // Assert
        EnemyHex().HasHighlight<AttackReachableHighlight>().ShouldBeFalse();
    }

    [Fact]
    public void ResumeMovementAfterFall_WhenTheMechCanStandUp_ReturnsToMovementTypeSelection()
    {
        // Arrange - the unit was walking when it fell
        DeployUnits();
        SelectAttackerAndWalk();
        _attacker.SetProne();

        // Act
        _sut.ResumeMovementAfterFall(_attacker.Id);

        // Assert
        _sut.CurrentMovementStep.ShouldBe(MovementStep.SelectingMovementType);
        _sut.SelectedUnit.ShouldBe(_attacker);
        _sut.CanSelectUnit(_enemy).ShouldBeFalse();
    }

    [Fact]
    public void ResumeMovementAfterFall_WhenTheMechCannotStandUp_CompletesMovementAtItsCurrentPosition()
    {
        // Arrange - a shutdown mech cannot stand up again
        DeployUnits();
        SelectAttackerAndWalk();
        _attacker.SetProne();
        _attacker.Shutdown(new ShutdownData { Reason = ShutdownReason.Voluntary, Turn = 1 });

        // Act
        _sut.ResumeMovementAfterFall(_attacker.Id);

        // Assert
        _sut.CurrentMovementStep.ShouldBe(MovementStep.Completed);
        _game.Received(1).MoveUnit(Arg.Is<MoveUnitCommand>(command => command.UnitId == _attacker.Id));
    }

    [Fact]
    public void ResumeMovementAfterFall_ForAnotherUnitsCommand_LeavesTheSelectionUntouched()
    {
        // Arrange - a fall reported for a unit the state is not moving
        DeployUnits();
        SelectAttackerAndWalk();

        // Act
        _sut.ResumeMovementAfterFall(_enemy.Id);

        // Assert
        _sut.SelectedUnit.ShouldBe(_attacker);
        _sut.CurrentMovementStep.ShouldBe(MovementStep.SelectingTargetHex);
    }

    private void DeployUnits()
    {
        _attacker.Deploy(_attackerPosition, null);
        _enemy.Deploy(_enemyPosition, null);
    }

    private void SelectAttackerAndWalk()
    {
        _sut.HandleUnitSelectionFromList(_attacker);
        _sut.HandleMovementTypeSelection(MovementType.Walk);
    }

    private Hex EnemyHex() => _game.BattleMap!.GetHex(_enemyPosition.Coordinates)!;

    private static UnitData CreateMechData()
    {
        var equipment = new List<ComponentData>
        {
            new()
            {
                Type = MakaMekComponent.Engine,
                Assignments =
                [
                    new LocationSlotAssignment(PartLocation.CenterTorso, 0, 3),
                    new LocationSlotAssignment(PartLocation.CenterTorso, 7, 3)
                ],
                SpecificData = new EngineStateData(EngineType.Fusion, 160)
            }
        };
        var data = MechFactoryTests.CreateDummyMechData(equipment, true);
        data.Id = Guid.NewGuid();
        return data;
    }

    private static void BindViewModelCurrentStateTo(MovementState state, BattleMapViewModel viewModel)
    {
        typeof(BattleMapViewModel).GetProperty(nameof(BattleMapViewModel.CurrentState))!
            .GetSetMethod(true)!.Invoke(viewModel, [state]);
    }
}
