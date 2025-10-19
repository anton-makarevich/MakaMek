using System.Reactive.Concurrency;
using NSubstitute;
using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Data.Units.Components;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Mechs.Falling;
using Sanet.MakaMek.Core.Models.Game.Phases;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Map.Factory;
using Sanet.MakaMek.Core.Models.Map.Terrains;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components.Engines;
using Sanet.MakaMek.Core.Models.Units.Components.Internal;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Core.Models.Units.Pilots;
using Sanet.MakaMek.Core.Services;
using Sanet.MakaMek.Core.Services.Localization;
using Sanet.MakaMek.Core.Services.Transport;
using Sanet.MakaMek.Core.Tests.Models.Map;
using Sanet.MakaMek.Core.Tests.Utils;
using Sanet.MakaMek.Core.Utils;
using Sanet.MakaMek.Core.Utils.Generators;
using Sanet.MakaMek.Presentation.UiStates;
using Sanet.MakaMek.Presentation.ViewModels;
using Shouldly;

namespace Sanet.MakaMek.Presentation.Tests.UiStates;

public class MovementStateTests
{
    private readonly IToHitCalculator _toHitCalculator = Substitute.For<IToHitCalculator>();
    private readonly IPilotingSkillCalculator _pilotingSkillCalculator = Substitute.For<IPilotingSkillCalculator>();
    private readonly IConsciousnessCalculator _consciousnessCalculator = Substitute.For<IConsciousnessCalculator>();
    private readonly IHeatEffectsCalculator _heatEffectsCalculator = Substitute.For<IHeatEffectsCalculator>();
    private readonly MovementState _sut;
    private readonly ClientGame _game;
    private readonly UnitData _unitData;
    private readonly Unit _unit1;
    private readonly Unit _unit2;
    private readonly Player _player;
    private readonly Hex _hex1;
    private readonly BattleMapViewModel _battleMapViewModel;
    private readonly ICommandPublisher _commandPublisher = Substitute.For<ICommandPublisher>();
    private readonly IPilot _pilot = Substitute.For<IPilot>();
    private readonly IRulesProvider _rulesProvider = new ClassicBattletechRulesProvider();
    private readonly IComponentProvider _componentProvider = new ClassicBattletechComponentProvider();
    private readonly ILocalizationService _localizationService = Substitute.For<ILocalizationService>();

    public MovementStateTests()
    {
        var imageService = Substitute.For<IImageService>();
        
        // Mock localization service responses
        _localizationService.GetString("Action_SelectUnitToMove").Returns("Select unit to move");
        _localizationService.GetString("Action_SelectMovementType").Returns("Select movement type");
        _localizationService.GetString("Action_SelectTargetHex").Returns("Select target hex");
        _localizationService.GetString("Action_SelectFacingDirection").Returns("Select facing direction");
        _localizationService.GetString("Action_MoveUnit").Returns("Move Unit");
        _localizationService.GetString("Action_StandStill").Returns("Stand Still");
        _localizationService.GetString("Action_StayProne").Returns("Stay Prone");
        _localizationService.GetString("Action_MovementPoints").Returns("{0} | MP: {1}");
        _localizationService.GetString("MovementType_Walk").Returns("Walk");
        _localizationService.GetString("MovementType_Run").Returns("Run");
        _localizationService.GetString("MovementType_Jump").Returns("Jump");
        _localizationService.GetString("Action_AttemptStandup").Returns("Attempt Standup");
        _localizationService.GetString("Action_ChangeFacing").Returns("Change Facing | MP: {0}");
        
        var dispatcherService = Substitute.For<IDispatcherService>();
        dispatcherService.Scheduler.Returns(Scheduler.Immediate);
        
         _battleMapViewModel = new BattleMapViewModel(imageService,
             _localizationService,
             dispatcherService,
             _rulesProvider);
        var playerId = Guid.NewGuid();
        
        _pilot.IsConscious.Returns(true);
        var equipment = new List<ComponentData>
                {
                    new()
                    {
                        Type = MakaMekComponent.JumpJet,
                        Assignments = [new LocationSlotAssignment(PartLocation.CenterTorso, 10, 1)]
                    },
                    new()
                    {
                        Type = MakaMekComponent.JumpJet,
                        Assignments = [new LocationSlotAssignment(PartLocation.CenterTorso, 11, 1)]
                    },
                    new()
                    {
                        Type = MakaMekComponent.Engine,
                        Assignments = [
                            new LocationSlotAssignment(PartLocation.CenterTorso, 0, 3),
                            new LocationSlotAssignment(PartLocation.CenterTorso, 7, 3)
                        ],
                        SpecificData = new EngineStateData(EngineType.Fusion, 160)
                    }
                };
        _unitData = MechFactoryTests.CreateDummyMechData(equipment,true);
        
        var mechFactory = new MechFactory(
            _rulesProvider,
            _componentProvider,
            _localizationService);
        _unit1 = mechFactory.Create(_unitData);
        _unit2 = mechFactory.Create(_unitData);
        _unit1.AssignPilot(_pilot);
        
        // Create two adjacent hexes
        _hex1 = new Hex(new HexCoordinates(1, 1));
        
        var battleMap = BattleMapTests.BattleMapFactory.GenerateMap(
            2, 11,
            new SingleTerrainGenerator(2,11, new ClearTerrain()));
         _player = new Player(playerId, "Player1");
        _game = new ClientGame(
            _rulesProvider,
            mechFactory,
            _commandPublisher,
            _toHitCalculator,
            _pilotingSkillCalculator,
            _consciousnessCalculator,
            _heatEffectsCalculator,
            Substitute.For<IBattleMapFactory>());
        
        _game.JoinGameWithUnits(_player,[],[]);
        _game.SetBattleMap(battleMap);
        
        _battleMapViewModel.Game = _game;
        AddPlayerUnits();
        SetActivePlayer();
        _sut = new MovementState(_battleMapViewModel);
    }

    [Fact]
    public void InitialState_HasSelectUnitAction()
    {
        // Assert
        _sut.ActionLabel.ShouldBe("Select unit to move");
        _sut.IsActionRequired.ShouldBeTrue();
    }
    
    [Fact]
    public void InitialState_CannotExecutePlayerAction()
    {
        // Assert
        _sut.CanExecutePlayerAction.ShouldBeFalse();
    }

    private void AddPlayerUnits()
    {
        var playerId2 = Guid.NewGuid();
        _game.HandleCommand(new JoinGameCommand
        {
            PlayerName = "Player1",
            Units = [_unitData],
            Tint = "#FF0000",
            GameOriginId = Guid.NewGuid(),
            PlayerId = _player.Id,
            PilotAssignments = []
        });
        _game.HandleCommand(new JoinGameCommand
        {
            PlayerName = "Player2",
            Units = [_unitData],
            Tint = "#FFFF00",
            GameOriginId = Guid.NewGuid(),
            PlayerId = playerId2,
            PilotAssignments = []
        });
        _game.HandleCommand(new UpdatePlayerStatusCommand
        {
            PlayerStatus = PlayerStatus.Ready,
            GameOriginId = Guid.NewGuid(),
            PlayerId = _player.Id
        });
        _game.HandleCommand(new UpdatePlayerStatusCommand
        {
            PlayerStatus = PlayerStatus.Ready,
            GameOriginId = Guid.NewGuid(),
            PlayerId = playerId2
        });
    }
    private void SetActivePlayer()
    {
        _game.HandleCommand(new ChangeActivePlayerCommand
        {
            GameOriginId = Guid.NewGuid(),
            PlayerId = _player.Id,
            UnitsToPlay = 1
        });
    }
    
    private void SetPhase(PhaseNames phase)
    {
        _game.HandleCommand(new ChangePhaseCommand
        {
            GameOriginId = Guid.NewGuid(),
            Phase = phase,
        });
    }

    [Fact]
    public void HandleUnitSelection_TransitionsToMovementTypeSelection()
    {
        // Act
        _sut.HandleUnitSelection(_unit1);

        // Assert
        _sut.ActionLabel.ShouldBe("Select movement type");
    }

    [Fact]
    public void HandleMovementTypeSelection_TransitionsToHexSelection()
    {
        // Arrange
        _sut.HandleUnitSelection(_unit1);
        
        // Act
        _sut.HandleMovementTypeSelection(MovementType.Walk);

        // Assert
        _sut.ActionLabel.ShouldBe("Select target hex");
    }

    [Fact]
    public void HandleHexSelection_TransitionsToDirectionSelection()
    {
        // Arrange
        _unit1.Deploy(new HexPosition(new HexCoordinates(1,2),HexDirection.Bottom));
        _sut.HandleUnitSelection(_unit1);
        _sut.HandleMovementTypeSelection(MovementType.Walk);
        
        // Act
        _sut.HandleHexSelection(_hex1);

        // Assert
        _sut.ActionLabel.ShouldBe("Select facing direction");
    }

    [Fact]
    public void HandleHexSelection_SelectsUnit_WhenUnitIsOnHex()
    {
        // Arrange
        SetPhase(PhaseNames.Movement);
        SetActivePlayer();
        var state = _battleMapViewModel.CurrentState;
        var position = new HexPosition(new HexCoordinates(1, 1),HexDirection.Bottom);
        var unit = _battleMapViewModel.Units.First();
        unit.Deploy(position);
        var hex = new Hex(position.Coordinates);

        // Act
        state.HandleHexSelection(hex);

        // Assert
        _battleMapViewModel.SelectedUnit.ShouldBe(unit);
        state.ActionLabel.ShouldBe("Select movement type");
    }

    [Fact]
    public void HandleHexSelection_SelectsUnit_WhenUnitIsOnHex_AndOtherUnitIsSelected()
    {
        // Arrange
        SetPhase(PhaseNames.Movement);
        SetActivePlayer();
        var state = _battleMapViewModel.CurrentState;
        var position = new HexPosition(new HexCoordinates(1, 1),HexDirection.Bottom);
        var unit = _battleMapViewModel.Units.First();
        unit.Deploy(position);
        var hex = new Hex(position.Coordinates);
        _battleMapViewModel.SelectedUnit = _unit2;

        // Act
        state.HandleHexSelection(hex);

        // Assert
        _battleMapViewModel.SelectedUnit.ShouldBe(unit);
        state.ActionLabel.ShouldBe("Select movement type");
    }

    [Fact]
    public void HandleHexSelection_DoesNotSelectsUnit_WhenUnitIsOnHex_ButNotOwnedByActivePlayer()
    {
        // Arrange
        SetPhase(PhaseNames.Movement);
        SetActivePlayer();
        var state = _battleMapViewModel.CurrentState;
        var position = new HexCoordinates(1, 1);
        var unit = _battleMapViewModel.Units.Last();
        unit.Deploy(new HexPosition(position,HexDirection.Bottom));
        var hex = new Hex(position);

        // Act
        state.HandleHexSelection(hex);

        // Assert
        _battleMapViewModel.SelectedUnit.ShouldBeNull();
    }

    [Fact]
    public void HandleHexSelection_DoesNothing_WhenNoUnitOnHex()
    {
        // Arrange
        var hex = new Hex(new HexCoordinates(1, 1));
        _unit1.Deploy(new HexPosition(1, 2, HexDirection.Bottom));
        var newPosition = new HexPosition(new HexCoordinates(2, 2), HexDirection.Top);
        _unit1.Move(MovementType.Walk,
            [new PathSegment(new HexPosition(1, 2, HexDirection.Bottom), newPosition, 1)
                .ToData()]);

        // Act
        _sut.HandleHexSelection(hex);

        // Assert
        _sut.ActionLabel.ShouldBe("Select unit to move");
    }

    [Fact]
    public void Constructor_ShouldThrow_IfGameNull()
    {
        // Arrange
        _battleMapViewModel.Game=null;
        // Act & Assert
        Should.Throw<InvalidOperationException>(() => new MovementState(_battleMapViewModel));
    }

    [Fact]
    public void Constructor_ShouldThrow_IfActivePlayerNull()
    {
        // Arrange
        SetPhase(PhaseNames.WeaponsAttack);
        // Act & Assert
        Should.Throw<InvalidOperationException>(() => new MovementState(_battleMapViewModel));
    }

    [Fact]
    public void HandleTargetHexSelection_ShowsDirectionSelector_WithPossibleDirections()
    {
        // Arrange
        SetPhase(PhaseNames.Movement);
        SetActivePlayer();
        var position = new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom);
        var unit = _battleMapViewModel.Units.First();
        unit.Deploy(position);
        _sut.HandleUnitSelection(unit);
        _sut.HandleMovementTypeSelection(MovementType.Walk);
        
        var targetHex = _game.BattleMap!.GetHex(new HexCoordinates(1, 2))!;
        
        // Act
        _sut.HandleHexSelection(targetHex);
        
        // Assert
        _battleMapViewModel.IsDirectionSelectorVisible.ShouldBeTrue();
        _battleMapViewModel.DirectionSelectorPosition.ShouldBe(targetHex.Coordinates);
        _battleMapViewModel.AvailableDirections.ShouldNotBeEmpty();
        // All directions should be available for adjacent hex with clear terrain
        _battleMapViewModel.AvailableDirections.ToList().Count.ShouldBe(6);
    }

    [Fact]
    public void HandleHexSelection_ResetsHighlighting_WhenUnitIsReselected()
    {
        // Arrange
        SetPhase(PhaseNames.Movement);
        SetActivePlayer();
        var position = new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom);
        var unit = _battleMapViewModel.Units.First();
        unit.Deploy(position);
        _battleMapViewModel.SelectedUnit = unit;
        _sut.HandleMovementTypeSelection(MovementType.Walk);
        var targetHex = _game.BattleMap!.GetHex(new HexCoordinates(1, 2))!;
        _sut.HandleHexSelection(targetHex);
        
        // Act
        _sut.HandleHexSelection(new Hex(position.Coordinates));
        
        // Assert
        foreach (var hex in _battleMapViewModel.Game!.BattleMap!.GetHexes())
        {
            hex.IsHighlighted.ShouldBeFalse();
        }
    }

    [Fact]
    public void HandleTargetHexSelection_DoesNotShowDirectionSelector_ForUnreachableHex()
    {
        // Arrange
        SetPhase(PhaseNames.Movement);
        SetActivePlayer();
        var position = new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom);
        var unit = _battleMapViewModel.Units.First();
        unit.Deploy(position);
        _sut.HandleUnitSelection(unit);
        _sut.HandleMovementTypeSelection(MovementType.Walk);
        
        // Create a hex that is too far to reach
        var unreachableHex = new Hex(new HexCoordinates(10, 10));
        
        // Act
        _sut.HandleHexSelection(unreachableHex);

        // Assert
        _battleMapViewModel.IsDirectionSelectorVisible.ShouldBeFalse();
        _battleMapViewModel.AvailableDirections.ShouldBeNull();
    }
    
    [Fact]
    public void HandleFacingSelection_ShouldPublishCommand_WhenInSelectingStandingUpDirectionStep()
    {
        // Arrange
        SetPhase(PhaseNames.Movement);
        SetActivePlayer();
        
        var position = new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom);
        var unit = _battleMapViewModel.Units.First() as Mech;
        unit!.Deploy(position);
        unit.AssignPilot(_pilot);
        unit.SetProne();
        _pilotingSkillCalculator.GetPsrBreakdown(unit, PilotingSkillRollType.StandupAttempt)
            .Returns(new PsrBreakdown
            {
                BasePilotingSkill = 4,
                Modifiers = []
            });
        _sut.HandleUnitSelection(unit);
        _sut.GetAvailableActions().First(a => a.Label.Contains("Walk")).OnExecute();
        
        // Act
        _sut.HandleFacingSelection(HexDirection.BottomLeft);
        
        // Assert
        _commandPublisher.Received().PublishCommand(Arg.Is<TryStandupCommand>(cmd =>
            cmd.UnitId == unit.Id && cmd.NewFacing == HexDirection.BottomLeft));
        _battleMapViewModel.IsDirectionSelectorVisible.ShouldBeFalse();
    }

    [Fact]
    public void HandleFacingSelection_DisplaysPath_WhenInDirectionSelectionStep()
    {
        // Arrange
        SetPhase(PhaseNames.Movement);
        SetActivePlayer();
        var position = new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom);
        var unit = _battleMapViewModel.Units.First();
        unit.Deploy(position);
        _sut.HandleUnitSelection(unit);
        _sut.HandleMovementTypeSelection(MovementType.Walk);
        
        var targetHex = _game.BattleMap!.GetHex(new HexCoordinates(1, 2))!;
        _sut.HandleHexSelection(targetHex);
        
        // Act
        _sut.HandleFacingSelection(HexDirection.Top);
        
        // Assert
        _battleMapViewModel.MovementPath.ShouldNotBeNull();
        _battleMapViewModel.MovementPath.Last().To.Coordinates.ShouldBe(targetHex.Coordinates);
        _battleMapViewModel.MovementPath.Last().To.Facing.ShouldBe(HexDirection.Top);
    }
    
    [Fact]
    public void HandleFacingSelection_ShouldNotCrash_WhenMovingToSamePosition()
    {
        // Arrange
        SetPhase(PhaseNames.Movement);
        SetActivePlayer();
        var position = new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom);
        var unit = _battleMapViewModel.Units.First();
        unit.Deploy(position);
        _sut.HandleUnitSelection(unit);
        _sut.HandleMovementTypeSelection(MovementType.Walk);
        
        var targetHex = _game.BattleMap!.GetHex(position.Coordinates)!;
        _sut.HandleHexSelection(targetHex);
        
        // Act
        _sut.HandleFacingSelection(HexDirection.Bottom);
    }

    [Fact]
    public void HandleFacingSelection_DoesNothing_WhenNotInDirectionSelectionStep()
    {
        // Arrange
        SetPhase(PhaseNames.Movement);
        SetActivePlayer();
        
        // Act
        _sut.HandleFacingSelection(HexDirection.Top);
        
        // Assert
        _sut.ActionLabel.ShouldBe("Select unit to move");
    }

    [Fact]
    public void HandleMovementTypeSelection_CalculatesBackwardReachableHexes_WhenWalking()
    {
        // Arrange
        var startPosition = new HexPosition(new HexCoordinates(1, 1), HexDirection.Top);
        _unit1.Deploy(startPosition);
        _sut.HandleUnitSelection(_unit1);
        
        // Act
        _sut.HandleMovementTypeSelection(MovementType.Walk);

        // Assert
        // The hex behind the unit (at 1,2) should be reachable
        var hexBehind = _game.BattleMap!.GetHex(new HexCoordinates(1, 9));
        hexBehind!.IsHighlighted.ShouldBeTrue();
    }

    [Fact]
    public void HandleMovementTypeSelection_DoesNotCalculateBackwardReachableHexes_WhenRunning()
    {
        // Arrange
        var startPosition = new HexPosition(new HexCoordinates(1, 1), HexDirection.Top);
        _unit1.Deploy(startPosition);
        _sut.HandleUnitSelection(_unit1);
        
        // Act
        _sut.HandleMovementTypeSelection(MovementType.Run);

        // Assert
        // The hex behind the unit (at 1,11) should not be reachable (12 running MP are not enough to reach it)
        var hexBehind = _game.BattleMap!.GetHex(new HexCoordinates(1, 11));
        hexBehind!.IsHighlighted.ShouldBeFalse();
    }

    [Fact]
    public void HandleTargetHexSelection_AllowsBackwardMovement_WhenWalking()
    {
        // Arrange
        var startPosition = new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom);
        _unit1.Deploy(startPosition);
        _sut.HandleUnitSelection(_unit1);
        _sut.HandleMovementTypeSelection(MovementType.Walk);
        var targetHex = _game.BattleMap!.GetHex(new HexCoordinates(1, 2))!;

        // Act
        _sut.HandleHexSelection(targetHex);

        // Assert
        _battleMapViewModel.IsDirectionSelectorVisible.ShouldBeTrue();
        _battleMapViewModel.DirectionSelectorPosition.ShouldBe(targetHex.Coordinates);
    }

    [Fact]
    public void HandleTargetHexSelection_SwapsDirectionsForBackwardMovement()
    {
        // Arrange
        var startPosition = new HexPosition(new HexCoordinates(1, 1), HexDirection.Top);
        _unit1.Deploy(startPosition);
        _sut.HandleUnitSelection(_unit1);
        _sut.HandleMovementTypeSelection(MovementType.Walk);
        var targetHex = _game.BattleMap!.GetHex(new HexCoordinates(1, 4))!;
        _sut.HandleHexSelection(targetHex);

        // Act
        _sut.HandleFacingSelection(HexDirection.Top);

        // Assert
        // Check that the path segments have correct facing directions
        var path = _battleMapViewModel.MovementPath;
        path.ShouldNotBeNull();
        path[0].From.Facing.ShouldBe(HexDirection.Top); // Original facing
        path[0].To.Coordinates.ShouldBe(new HexCoordinates(1, 2)); // Target hex
        path[0].To.Facing.ShouldBe(HexDirection.Top); // Maintains facing for backward movement
        path.Last().To.Coordinates.ShouldBe(targetHex.Coordinates);
        path.Last().To.Facing.ShouldBe(HexDirection.Top);
    }
    
    [Fact]
    public void HandleUnitSelection_ClearsHexHighlighting_WhenUnitSelectedAgain()
    {
        // Arrange
        var startPosition = new HexPosition(new HexCoordinates(1, 1), HexDirection.Top);
        var unit = _battleMapViewModel.Units.First();
        unit.Deploy(startPosition);
        var unitHex = _game.BattleMap!.GetHex(unit.Position!.Coordinates)!;
        _sut.HandleHexSelection(unitHex);
        _sut.HandleMovementTypeSelection(MovementType.Walk);
        var targetHex = _game.BattleMap.GetHex(new HexCoordinates(1, 4))!;
        _sut.HandleHexSelection(targetHex);
        _sut.HandleFacingSelection(HexDirection.Top);

        // Act
        _sut.HandleHexSelection(unitHex);
        
        // Assert
        _battleMapViewModel.MovementPath.ShouldBeNull();
        foreach (var hex in _battleMapViewModel.Game!.BattleMap!.GetHexes())
        {
            hex.IsHighlighted.ShouldBeFalse();
        }
    }

    [Fact]
    public void HandleMovementTypeSelection_ForJumping_CalculatesReachableHexes()
    {
        // Arrange
        var position = new HexPosition(new HexCoordinates(1, 2), HexDirection.Bottom);
        _unit1.Deploy(position);
        _sut.HandleUnitSelection(_unit1);
        
        // Act
        _sut.HandleMovementTypeSelection(MovementType.Jump);

        // Assert
        var reachableHexes = _game.BattleMap!.GetHexes()
            .Count(h => h.IsHighlighted);
        reachableHexes.ShouldBeGreaterThan(0, "Should highlight reachable hexes");

        // Verify only hexes within jump range are highlighted
        foreach (var hex in _game.BattleMap.GetHexes())
        {
            if (hex.IsHighlighted)
            {
                hex.Coordinates.DistanceTo(position.Coordinates)
                    .ShouldBeLessThanOrEqualTo(_unit1.GetMovementPoints(MovementType.Jump),
                        "Should only highlight hexes within jump range");
            }
        }
    }

    [Fact]
    public void HandleTargetHexSelection_ForJumping_ShowsAllDirections()
    {
        // Arrange
        var startPosition = new HexPosition(new HexCoordinates(1, 2), HexDirection.Bottom);
        _unit1.Deploy(startPosition);
        _sut.HandleUnitSelection(_unit1);
        _sut.HandleMovementTypeSelection(MovementType.Jump);
        
        var targetHex = _game.BattleMap!.GetHex(new HexCoordinates(1, 3))!;
        
        // Act
        _sut.HandleHexSelection(targetHex);
        
        // Assert
        _battleMapViewModel.IsDirectionSelectorVisible.ShouldBeTrue("Should show direction selector");
        _battleMapViewModel.DirectionSelectorPosition.ShouldBe(targetHex.Coordinates);
        _battleMapViewModel.AvailableDirections!.ToList().Count.ShouldBe(6, "All directions should be available for jumping");
    }

    [Fact]
    public void HandleMovementTypeSelection_CompletesMovement_WhenStandingStill()
    {
        // Arrange
        var startPosition = new HexPosition(new HexCoordinates(1,2), HexDirection.Bottom);
        _unit1.Deploy(startPosition);
        _sut.HandleUnitSelection(_unit1);
        
        // Act
        _sut.HandleMovementTypeSelection(MovementType.StandingStill);

        // Assert
        _sut.ActionLabel.ShouldBe(string.Empty); // Movement should be completed
    }

    [Fact]
    public void HandleMovementTypeSelection_IncludesCurrentHex_InReachableHexes()
    {
        // Arrange
        var position = new HexPosition(new HexCoordinates(1, 2), HexDirection.Bottom);
        _unit1.Deploy(position);
        _sut.HandleUnitSelection(_unit1);
        
        // Act
        _sut.HandleMovementTypeSelection(MovementType.Walk);

        // Assert
        var reachableHexes = _battleMapViewModel.Game!.BattleMap!.GetHexes()
            .Where(h => h.IsHighlighted)
            .Select(h => h.Coordinates)
            .ToList();
        reachableHexes.ShouldContain(position.Coordinates);
    }

    [Fact]
    public void HandleTargetHexSelection_ShowsDirectionSelector_WhenSelectingCurrentHex()
    {
        // Arrange
        var position = new HexPosition(new HexCoordinates(1, 2), HexDirection.Bottom);
        _unit1.Deploy(position);
        _sut.HandleUnitSelection(_unit1);
        _sut.HandleMovementTypeSelection(MovementType.Walk);
        var currentHex = _battleMapViewModel.Game!.BattleMap!.GetHex(position.Coordinates);

        // Act
        _sut.HandleHexSelection(currentHex!);

        // Assert
        _battleMapViewModel.IsDirectionSelectorVisible.ShouldBeTrue();
        _battleMapViewModel.DirectionSelectorPosition.ShouldBe(position.Coordinates);
    }

    [Fact]
    public void HandleTargetHexSelection_ResetsSelection_WhenClickingOutsideReachableHexes()
    {
        // Arrange
        var unit = _battleMapViewModel.Units.First();
        var startPosition = new HexPosition(new HexCoordinates(1, 2), HexDirection.Top);
        unit.Deploy(startPosition);
        var unitHex = _game.BattleMap!.GetHex(unit.Position!.Coordinates)!;
        _sut.HandleHexSelection(unitHex);
        _sut.HandleUnitSelection(unit);
        _sut.HandleMovementTypeSelection(MovementType.Walk);
        var unreachableHex = _battleMapViewModel.Game!.BattleMap!.GetHex(new HexCoordinates(1, 11)); // Far away hex

        // Act
        _sut.HandleHexSelection(unreachableHex!);

        // Assert
        _battleMapViewModel.SelectedUnit.ShouldBeNull(); // Selection should be reset
        _sut.CurrentMovementStep.ShouldBe(MovementStep.SelectingUnit); // Back to an initial step
        _battleMapViewModel.Game.BattleMap.GetHexes()
            .Any(h => h.IsHighlighted)
            .ShouldBeFalse(); // No highlighted hexes
    }
    
    [Fact]
    public void HandleTargetHexSelection_ShouldNotResetSelection_WhenClickingOutsideReachableHexes_ButInPostStandupMovement()
    {
        // Arrange
        var position = new HexPosition(new HexCoordinates(1, 2), HexDirection.Bottom);
        var proneMech = _battleMapViewModel.Units.First() as Mech;
        _pilotingSkillCalculator.GetPsrBreakdown(proneMech!, PilotingSkillRollType.StandupAttempt)
            .Returns(new PsrBreakdown
            {
                BasePilotingSkill = 4,
                Modifiers = []
            });
        proneMech!.Deploy(position);
        proneMech.AssignPilot(_pilot);
        proneMech.SetProne();
        var unitHex = _game.BattleMap!.GetHex(proneMech.Position!.Coordinates)!;
        _sut.HandleHexSelection(unitHex);
        _sut.HandleUnitSelection(proneMech);

        var walkStandupAction = _sut.GetAvailableActions().First(a => a.Label.StartsWith("Walk"));
        walkStandupAction.OnExecute();
        _sut.HandleFacingSelection(HexDirection.Bottom);
        proneMech.StandUp(HexDirection.Bottom);
        _sut.ResumeMovementAfterStandup();
        var unreachableHex = _battleMapViewModel.Game!.BattleMap!.GetHex(new HexCoordinates(1, 11)); // Far away hex
 
        // Act
        _sut.HandleHexSelection(unreachableHex!);

        // Assert
        _battleMapViewModel.SelectedUnit.ShouldBe(proneMech); // Selection should be reset
        _battleMapViewModel.Game.BattleMap.GetHexes()
            .Any(h => h.IsHighlighted)
            .ShouldBeTrue();
    } 

    [Fact]
    public void GetAvailableActions_NoSelectedUnit_ReturnsEmpty()
    {
        // Act
        var actions = _sut.GetAvailableActions();

        // Assert
        actions.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(MovementType.StandingStill)]
    [InlineData(MovementType.Run)]
    [InlineData(MovementType.Walk)]
    [InlineData(MovementType.Jump)]
    public void GetAvailableActions_NotInMovementTypeSelection_ReturnsEmpty(MovementType type)
    {
        // Arrange
        _sut.HandleUnitSelection(_unit1);
        _sut.HandleMovementTypeSelection(type); // This moves us past movement type selection

        // Act
        var actions = _sut.GetAvailableActions();

        // Assert
        actions.ShouldBeEmpty();
    }

    [Fact]
    public void GetAvailableActions_InMovementTypeSelection_ReturnsMovementOptions()
    {
        // Arrange
        _sut.HandleUnitSelection(_unit1);

        // Act
        var actions = _sut.GetAvailableActions().ToList();

        // Assert
        actions.Count.ShouldBe(4); // Stand Still, Walk, Run, Jump (since the unit has jump jets)
        actions[0].Label.ShouldBe("Stand Still");
        actions[1].Label.ShouldBe($"Walk | MP: {_unit1.GetMovementPoints(MovementType.Walk)}");
        actions[2].Label.ShouldBe($"Run | MP: {_unit1.GetMovementPoints(MovementType.Run)}");
        actions[3].Label.ShouldBe($"Jump | MP: {_unit1.GetMovementPoints(MovementType.Jump)}");
    }
    
    [Fact]
    public void GetAvailableActions_ShouldNotReturnRunOption_WhenMechCannotRun()
    {
        // Arrange
        _sut.HandleUnitSelection(_unit1);
        var leg = _unit1.Parts[PartLocation.LeftLeg];
        leg.ApplyDamage(20, HitDirection.Front); // Destroy the leg

        // Act
        var actions = _sut.GetAvailableActions().ToList();

        // Assert
        actions.Count.ShouldBe(3); // Stand Still, Walk, Jump (since the unit has jump jets)
        actions[0].Label.ShouldBe("Stand Still");
        actions[1].Label.ShouldBe($"Walk | MP: {_unit1.GetMovementPoints(MovementType.Walk)}");
        actions[2].Label.ShouldBe($"Jump | MP: {_unit1.GetMovementPoints(MovementType.Jump)}");
    }
    
    [Fact]
    public void GetAvailableActions_ShouldReturnStillOnly_InMovementTypeSelection_WhenUnitIsImmobile()
    {
        // Arrange
        _sut.HandleUnitSelection(_unit1);
        var shutdownData = new ShutdownData { Reason = ShutdownReason.Voluntary, Turn = 1 };
        _unit1.Shutdown(shutdownData);

        // Act
        var actions = _sut.GetAvailableActions().ToList();

        // Assert
        actions.Count.ShouldBe(1); // Stand Still only
        actions[0].Label.ShouldBe("Stand Still");
    }

    [Fact]
    public void GetAvailableActions_NoJumpJets_DoesNotShowJumpOption()
    {
        // Arrange
        var unitData = MechFactoryTests.CreateDummyMechData();
        var unitWithoutJumpJets = new MechFactory(
                _rulesProvider,
                _componentProvider,
                _localizationService)
            .Create(unitData);
        _sut.HandleUnitSelection(unitWithoutJumpJets);
        unitWithoutJumpJets.AssignPilot(_pilot);

        // Act
        var actions = _sut.GetAvailableActions().ToList();

        // Assert
        actions.Count.ShouldBe(3); // Stand Still, Walk, Run
        actions.ShouldNotContain(a => a.Label.StartsWith("Jump"));
    }
    
    
    [Fact]
    public void GetAvailableActions_ProneMech_ShowsBothStandupAndChangeFacingActions()
    {
        // Arrange
        var proneMech = _unit1 as Mech;
        _pilotingSkillCalculator.GetPsrBreakdown(proneMech!, PilotingSkillRollType.StandupAttempt)
            .Returns(new PsrBreakdown
            {
                BasePilotingSkill = 4,
                Modifiers = []
            });
        proneMech!.SetProne();
        _sut.HandleUnitSelection(proneMech);

        // Act
        var actions = _sut.GetAvailableActions().ToList();

        // Assert
        actions.Count.ShouldBe(4, "Prone mech should have 2 standup actions, change facing, and stay prone actions");

        var standupWalkAction = actions.FirstOrDefault(a => a.Label.Contains("Walk"));
        standupWalkAction.ShouldNotBeNull("Should have standup action for walk");
        standupWalkAction.Label.ShouldBe("Walk | MP: 8 (92%)");

        var standupRunAction = actions.FirstOrDefault(a => a.Label.Contains("Run"));
        standupRunAction.ShouldNotBeNull("Should have standup action for walk");
        standupRunAction.Label.ShouldBe("Run | MP: 12 (92%)");
        
        var changeFacingAction = actions.FirstOrDefault(a => a.Label.Contains("Change Facing"));
        changeFacingAction.ShouldNotBeNull("Should have change facing action");
        changeFacingAction.Label.ShouldBe("Change Facing | MP: 8", "Should show available MP in localized format");

        var stayProneAction = actions.FirstOrDefault(a => a.Label.Contains("Stay Prone"));
        stayProneAction.ShouldNotBeNull("Should have stay prone action");
        stayProneAction.Label.ShouldBe("Stay Prone");
    }
    
    [Fact]
    public void GetAvailableActions_ForProneMech_ShouldReturnStillOnly_WhenUnitIsImmobile()
    {
        // Arrange
        _sut.HandleUnitSelection(_unit1);
        var shutdownData = new ShutdownData { Reason = ShutdownReason.Voluntary, Turn = 1 };
        var proneMech = _unit1 as Mech;
        proneMech!.SetProne();
        proneMech.Shutdown(shutdownData);

        // Act
        var actions = _sut.GetAvailableActions().ToList();

        // Assert
        actions.Count.ShouldBe(1); // Stand Still only
        actions[0].Label.ShouldBe("Stay Prone");
    }

    [Fact]
    public void GetAvailableActions_ShouldContainAttemptStandupWithoutMovementAction_WhenMechIsProne_AndHasMinimumMovement()
    {
        // Arrange
        var proneMech = _unit1 as Mech;
        _pilotingSkillCalculator.GetPsrBreakdown(proneMech!, PilotingSkillRollType.StandupAttempt)
            .Returns(new PsrBreakdown
            {
                BasePilotingSkill = 4,
                Modifiers = []
            });
        proneMech!.SetProne();
        var leg = proneMech.Parts[PartLocation.LeftLeg];
        leg.ApplyDamage(20, HitDirection.Front); // Destroy the leg
        _sut.HandleUnitSelection(proneMech);
        
        // Act
        var actions = _sut.GetAvailableActions().ToList();

        // Assert
        actions.Count.ShouldBe(3, "Prone mech with minimum movement should have 1 standup action, stay prone action and change facing action");
        var standupAction = actions.FirstOrDefault(a => a.Label.Contains("Attempt Standup"));
        standupAction.ShouldNotBeNull("Should have standup action");
    }
    
    [Fact]
    public void GetAvailableActions_ProneMech_CannotStandup_StillHasStayProneAction()
    {
        // Arrange
        _pilotingSkillCalculator.GetPsrBreakdown(Arg.Any<Mech>(), PilotingSkillRollType.StandupAttempt)
            .Returns(new PsrBreakdown
            {
                BasePilotingSkill = 4,
                Modifiers = []
            });
        // Set up a prone Mech that cannot stand up
        var proneMech = _unit1 as Mech;
        proneMech!.SetProne();
        proneMech.Shutdown(new ShutdownData { Reason = ShutdownReason.Voluntary, Turn = 1 }); // shutdown cannot stand up
        _sut.HandleUnitSelection(proneMech);

        // Act
        var actions = _sut.GetAvailableActions().ToList();

        // Assert
        actions.Count.ShouldBe(1, "Prone mech that cannot stand up should still have stay prone action");
        var stayProneAction = actions.FirstOrDefault(a => a.Label.Contains("Stay Prone"));
        stayProneAction.ShouldNotBeNull("Should have stay prone action even when cannot stand up");
    }

    [Theory]
    [InlineData("Run")]
    [InlineData("Walk")]
    public void GetAvailableActions_ExecutingAction_UpdatesState(string startWithLabel)
    {
        // Arrange
        _sut.HandleUnitSelection(_unit1);
        var actions = _sut.GetAvailableActions().ToList();
        var walkAction = actions.First(a => a.Label.StartsWith(startWithLabel));

        // Act
        walkAction.OnExecute();

        // Assert
        _sut.CurrentMovementStep.ShouldBe(MovementStep.SelectingTargetHex);
    }
    
    [Fact]
    public void GetAvailableActions_ExecutingStandingAction_CompletesState()
    {
        // Arrange
        _sut.HandleUnitSelection(_unit1);
        var actions = _sut.GetAvailableActions().ToList();
        var walkAction = actions.First(a => a.Label.StartsWith("Stand"));

        // Act
        walkAction.OnExecute();

        // Assert
        _sut.CurrentMovementStep.ShouldBe(MovementStep.Completed);
    }

    [Fact]
    public void HandleFacingSelection_TransitionsToConfirmMovement_AfterSelectingDirection()
    {
        // Arrange
        SetPhase(PhaseNames.Movement);
        SetActivePlayer();
        var position = new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom);
        var unit = _battleMapViewModel.Units.First();
        unit.Deploy(position);
        _sut.HandleUnitSelection(unit);
        _sut.HandleMovementTypeSelection(MovementType.Walk);
        
        var targetHex = _game.BattleMap!.GetHex(new HexCoordinates(1, 2))!;
        _sut.HandleHexSelection(targetHex);
        
        // Act
        _sut.HandleFacingSelection(HexDirection.Top);
        
        // Assert
        _battleMapViewModel.MovementPath.ShouldNotBeNull();
        _sut.ActionLabel.ShouldBe("Move Unit");
    }

    [Fact]
    public void HandleFacingSelection_CompletesMovement_WhenInConfirmMovementStep()
    {
        // Arrange
        SetPhase(PhaseNames.Movement);
        SetActivePlayer();
        var position = new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom);
        var unit = _battleMapViewModel.Units.First();
        unit.Deploy(position);
        _sut.HandleUnitSelection(unit);
        _sut.HandleMovementTypeSelection(MovementType.Walk);
        
        var targetHex = _game.BattleMap!.GetHex(new HexCoordinates(1, 2))!;
        _sut.HandleHexSelection(targetHex);
        
        // First selection transitions to ConfirmMovement
        _sut.HandleFacingSelection(HexDirection.Top);
        
        // Act - Second selection confirms the movement
        _sut.HandleFacingSelection(HexDirection.Top);
        
        // Assert
        _battleMapViewModel.IsDirectionSelectorVisible.ShouldBeFalse();
        _sut.ActionLabel.ShouldBeEmpty();
        _sut.IsActionRequired.ShouldBeFalse();
        foreach (var hex in _battleMapViewModel.Game!.BattleMap!.GetHexes())
        {
            hex.IsHighlighted.ShouldBeFalse();
        }
    }
    
    [Fact]
    public void ExecutePlayerAction_CompletesMovement_WhenInConfirmMovementStep()
    {
        // Arrange
        SetPhase(PhaseNames.Movement);
        SetActivePlayer();
        var position = new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom);
        var unit = _battleMapViewModel.Units.First();
        unit.Deploy(position);
        _sut.HandleUnitSelection(unit);
        _sut.HandleMovementTypeSelection(MovementType.Walk);
        
        var targetHex = _game.BattleMap!.GetHex(new HexCoordinates(1, 2))!;
        _sut.HandleHexSelection(targetHex);
        _sut.CanExecutePlayerAction.ShouldBeFalse();
        
        // First selection transitions to ConfirmMovement
        _sut.HandleFacingSelection(HexDirection.Top);
        _sut.CanExecutePlayerAction.ShouldBeTrue();
        _sut.PlayerActionLabel.ShouldBe("Move Unit");
        _sut.ExecutePlayerAction();
        
        // Assert
        _battleMapViewModel.IsDirectionSelectorVisible.ShouldBeFalse();
        _sut.ActionLabel.ShouldBeEmpty();
        _sut.IsActionRequired.ShouldBeFalse();
        
        foreach (var hex in _battleMapViewModel.Game!.BattleMap!.GetHexes())
        {
            hex.IsHighlighted.ShouldBeFalse();
        }
    }
    
    [Fact]
    public void PlayerActionLabel_ReturnsEmptyString_WhenNotInConfirmMovementStep()
    {
        // Arrange
        _sut.HandleUnitSelection(_unit1);
        
        // Act
        var result = _sut.PlayerActionLabel;
        
        // Assert
        result.ShouldBe(string.Empty);
    }
    
    [Fact]
    public void ActionLabel_ShouldBeSelectDirection_WhenInSelectingStandingUpDirectionStep()
    {
        // Arrange
        var position = new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom);
        var unit = _battleMapViewModel.Units.First() as Mech;
        unit!.Deploy(position);
        unit.AssignPilot(_pilot);
        unit.SetProne();
        _pilotingSkillCalculator.GetPsrBreakdown(unit, PilotingSkillRollType.StandupAttempt)
            .Returns(new PsrBreakdown
            {
                BasePilotingSkill = 4,
                Modifiers = []
            });
        _sut.HandleUnitSelection(unit);
        _sut.GetAvailableActions().First(a => a.Label.Contains("Walk")).OnExecute();
        
        // Act
        var result = _sut.ActionLabel;
        
        // Assert
        result.ShouldBe("Select facing direction");
    }
    
    [Fact]
    public void HandleStandupAttempt_ShowsDirectionSelector()
    {
        // Arrange
        SetPhase(PhaseNames.Movement);
        SetActivePlayer();

        // Set up a prone Mech
        var proneMech = _unit1 as Mech;
        _pilotingSkillCalculator.GetPsrBreakdown(proneMech!, PilotingSkillRollType.StandupAttempt)
            .Returns(new PsrBreakdown
            {
                BasePilotingSkill = 4,
                Modifiers = []
            });
        proneMech!.Deploy(new HexPosition(1,1,HexDirection.Bottom));
        proneMech.SetProne();
        _sut.HandleUnitSelection(proneMech);
        var standupAction = _sut.GetAvailableActions().First(a=> a.Label.Contains("Walk"));
        
        // Act
        standupAction.OnExecute();
        
        // Assert
        _battleMapViewModel.IsDirectionSelectorVisible.ShouldBeTrue();
    }
    
    [Fact]
    public void HandleStandupAttempt_ShouldNotSendTryStandupCommand_WhenNoActivePlayer()
    {
        // Arrange
        SetPhase(PhaseNames.Movement);
        SetActivePlayer();
        
        // Set up a prone Mech
        var proneMech = _unit1 as Mech;
        _pilotingSkillCalculator.GetPsrBreakdown(proneMech!, PilotingSkillRollType.StandupAttempt)
            .Returns(new PsrBreakdown
            {
                BasePilotingSkill = 4,
                Modifiers = []
            });
        proneMech!.SetProne();
        _sut.HandleUnitSelection(proneMech);
        var standupAction = _sut.GetAvailableActions().First();
        SetPhase(PhaseNames.Start); //no active player in that phase
        
        // Act
        standupAction.OnExecute();
        
        // Assert
        _commandPublisher.DidNotReceive().PublishCommand(Arg.Any<TryStandupCommand>());
    }

    [Fact]
    public void GetAvailableActions_MechJustStoodUp_ExcludesJumpAction()
    {
        // Arrange
        var unitData = MechFactoryTests.CreateDummyMechData() with
        {
            Equipment =
            [
                new ComponentData
                {
                    Type = MakaMekComponent.LRM5,
                    Assignments = [new LocationSlotAssignment(PartLocation.LeftTorso, 1, 1)]
                },
                new ComponentData
                {
                    Type = MakaMekComponent.MediumLaser,
                    Assignments = [new LocationSlotAssignment(PartLocation.RightTorso, 1, 1)]
                }
            ]
        };
        
        var mechThatStoodUp = new MechFactory(
                _rulesProvider,
                _componentProvider,
                _localizationService)
            .Create(unitData);
        mechThatStoodUp.AssignPilot(_pilot);

        // Simulate the mech having just stood up
        mechThatStoodUp.SetProne();
        mechThatStoodUp.AttemptStandup();
        mechThatStoodUp.StandUp(HexDirection.Bottom);

        _sut.HandleUnitSelection(mechThatStoodUp);

        // Act
        var actions = _sut.GetAvailableActions().ToList();

        // Assert
        actions.Count.ShouldBe(3); // Stand Still, Walk, Run (no Jump due to standing up)
        var jumpAction = actions.FirstOrDefault(a => a.Label.StartsWith("Jump"));
        jumpAction.ShouldBeNull("Jump action should not be included when mech just stood up");
    }

    [Fact]
    public void GetAvailableActions_WhenJumpWithDamagedGyro_ShouldIncludeProbability()
    {
        // Arrange
        var mech = _unit1 as Mech;

        // Damage the gyro to require PSR
        var gyro = mech!.GetAllComponents<Gyro>().First();
        gyro.Hit();

        _sut.HandleUnitSelection(mech);

        // Mock PSR breakdown for damaged gyro jump
        var psrBreakdown = new PsrBreakdown
        {
            BasePilotingSkill = 4,
            Modifiers = []
        };

        _pilotingSkillCalculator
            .GetPsrBreakdown(mech, PilotingSkillRollType.JumpWithDamage)
            .Returns(psrBreakdown);

        // Act
        var actions = _sut.GetAvailableActions().ToList();

        // Assert
        var jumpAction = actions.FirstOrDefault(a => a.Label.Contains("Jump"));
        jumpAction.ShouldNotBeNull();
        jumpAction.Label.ShouldContain("("); // Should contain probability percentage
        jumpAction.Label.ShouldContain("%)"); // Should contain a percentage symbol

    }

    [Fact]
    public void GetAvailableActions_ProneMech_StayProneAction_CompletesMovement()
    {
        // Arrange
        var proneMech = _unit1 as Mech;
        _pilotingSkillCalculator.GetPsrBreakdown(proneMech!, PilotingSkillRollType.StandupAttempt)
            .Returns(new PsrBreakdown
            {
                BasePilotingSkill = 4,
                Modifiers = []
            });
        proneMech!.SetProne();
        _sut.HandleUnitSelection(proneMech);

        var actions = _sut.GetAvailableActions().ToList();
        var stayProneAction = actions.FirstOrDefault(a => a.Label.Contains("Stay Prone"));
        stayProneAction.ShouldNotBeNull();

        // Act
        stayProneAction.OnExecute();

        // Assert
        _sut.CurrentMovementStep.ShouldBe(MovementStep.Completed, "Stay prone should complete movement like standing still");
    }

    [Fact]
    public void HandleProneFacingChange_ShowsDirectionSelector_WithValidDirections()
    {
        // Arrange
        SetPhase(PhaseNames.Movement);
        SetActivePlayer();
        var proneMech = _unit1 as Mech;

        // Set up piloting skill calculator mock
        _pilotingSkillCalculator.GetPsrBreakdown(proneMech!, PilotingSkillRollType.StandupAttempt)
            .Returns(new PsrBreakdown
            {
                BasePilotingSkill = 4,
                Modifiers = []
            });

        var position = new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom);
        proneMech!.Deploy(position);
        proneMech.SetProne();
        _sut.HandleUnitSelection(proneMech);

        var actions = _sut.GetAvailableActions().ToList();
        var changeFacingAction = actions.FirstOrDefault(a => a.Label.Contains("Change Facing"));
        changeFacingAction.ShouldNotBeNull();

        // Act
        changeFacingAction.OnExecute();

        // Assert
        _battleMapViewModel.IsDirectionSelectorVisible.ShouldBeTrue("Direction selector should be visible");
        _battleMapViewModel.DirectionSelectorPosition.ShouldBe(position.Coordinates, "Direction selector should be at mech position");
        _battleMapViewModel.AvailableDirections.ShouldNotBeNull("Available directions should not be null");

        var availableDirections = _battleMapViewModel.AvailableDirections.ToList();
        availableDirections.ShouldNotBeEmpty("Should have available directions");

        // Should not include the current facing direction
        availableDirections.ShouldNotContain(HexDirection.Bottom, "Should not include current facing direction");

        // With 4 MP, should be able to rotate up to 3 hexsides in either direction
        // This means we can reach: Top (3 steps), TopRight (2 steps), BottomRight (1 step),
        // BottomLeft (1 step), TopLeft (2 steps) - all except Bottom (current facing)
        availableDirections.Count.ShouldBe(5, "Should have 5 available directions (all except current facing)");

        _sut.ActionLabel.ShouldBe("Select facing direction", "Should transition to direction selection step");
    }
    
    [Fact]
    public void ResumeMovementAfterStandup_ShouldHighlightReachableHexes_WhenMechStoodUp()
    {
        // Arrange
        var position = new HexPosition(new HexCoordinates(1, 2), HexDirection.Bottom);
        _unit1.Deploy(position);
        var proneMech = _unit1 as Mech;
        _pilotingSkillCalculator.GetPsrBreakdown(proneMech!, PilotingSkillRollType.StandupAttempt)
            .Returns(new PsrBreakdown
            {
                BasePilotingSkill = 4,
                Modifiers = []
            });
        proneMech!.SetProne();
        _sut.HandleUnitSelection(proneMech);

        var walkStandupAction = _sut.GetAvailableActions().First(a => a.Label.StartsWith("Walk"));
        walkStandupAction.OnExecute();
        proneMech.StandUp(HexDirection.Bottom);
        
        // Act
        _sut.ResumeMovementAfterStandup();

        // Assert
        var reachableHexes = _battleMapViewModel.Game!.BattleMap!.GetHexes()
            .Where(h => h.IsHighlighted)
            .ToList();
        reachableHexes.ShouldNotBeEmpty();
    }
    
    [Fact]
    public void ResumeMovementAfterStandup_ShouldNotHighlightReachableHexes_WhenMechIsProne()
    {
        // Arrange
        var position = new HexPosition(new HexCoordinates(1, 2), HexDirection.Bottom);
        _unit1.Deploy(position);
        var proneMech = _unit1 as Mech;
        _pilotingSkillCalculator.GetPsrBreakdown(proneMech!, PilotingSkillRollType.StandupAttempt)
            .Returns(new PsrBreakdown
            {
                BasePilotingSkill = 4,
                Modifiers = []
            });
        proneMech!.SetProne();
        _sut.HandleUnitSelection(proneMech);

        var walkStandupAction = _sut.GetAvailableActions().First(a => a.Label.StartsWith("Walk"));
        walkStandupAction.OnExecute();
        
        // Act
        _sut.ResumeMovementAfterStandup();

        // Assert
        var reachableHexes = _battleMapViewModel.Game!.BattleMap!.GetHexes()
            .Where(h => h.IsHighlighted)
            .ToList();
        reachableHexes.ShouldBeEmpty();
    }
    
    [Fact]
    public void ResumeMovementAfterStandup_ShouldNotHighlightReachableHexes_WhenMechHasNoMp()
    {
        // Arrange
        var position = new HexPosition(new HexCoordinates(1, 2), HexDirection.Bottom);
        _unit1.Deploy(position);
        var proneMech = _unit1 as Mech;
        _pilotingSkillCalculator.GetPsrBreakdown(proneMech!, PilotingSkillRollType.StandupAttempt)
            .Returns(new PsrBreakdown
            {
                BasePilotingSkill = 4,
                Modifiers = []
            });
        var leg = proneMech!.Parts[PartLocation.LeftLeg];
        proneMech.SetProne();
        _sut.HandleUnitSelection(proneMech);

        var walkStandupAction = _sut.GetAvailableActions().First(a => a.Label.StartsWith("Walk"));
        walkStandupAction.OnExecute();
        proneMech.StandUp(HexDirection.Bottom);
        leg.ApplyDamage(100, HitDirection.Front); // Destroy the leg
        proneMech.AttemptStandup();
        
        // Act
        _sut.ResumeMovementAfterStandup();

        // Assert
        var reachableHexes = _battleMapViewModel.Game!.BattleMap!.GetHexes()
            .Where(h => h.IsHighlighted)
            .ToList();
        reachableHexes.ShouldBeEmpty();
    }
}
