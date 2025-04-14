using Shouldly;
using NSubstitute;
using Sanet.MakaMek.Core.Data.Community;
using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Combat;
using Sanet.MakaMek.Core.Models.Game.Commands.Client;
using Sanet.MakaMek.Core.Models.Game.Commands.Server;
using Sanet.MakaMek.Core.Models.Game.Phases;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Map.Factory;
using Sanet.MakaMek.Core.Models.Map.Terrains;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Services;
using Sanet.MakaMek.Core.Services.Localization;
using Sanet.MakaMek.Core.Services.Transport;
using Sanet.MakaMek.Core.Tests.Data.Community;
using Sanet.MakaMek.Core.Tests.Models.Map;
using Sanet.MakaMek.Core.UiStates;
using Sanet.MakaMek.Core.Utils;
using Sanet.MakaMek.Core.Utils.Generators;
using Sanet.MakaMek.Core.Utils.TechRules;
using Sanet.MakaMek.Core.ViewModels;

namespace Sanet.MakaMek.Core.Tests.UiStates;

public class MovementStateTests
{
    private readonly MovementState _sut;
    private readonly ClientGame _game;
    private readonly UnitData _unitData;
    private readonly Unit _unit1;
    private readonly Unit _unit2;
    private readonly Player _player;
    private readonly Hex _hex1;
    private readonly BattleMapViewModel _battleMapViewModel;

    public MovementStateTests()
    {
        var imageService = Substitute.For<IImageService>();
        var localizationService = Substitute.For<ILocalizationService>();
        
        // Mock localization service responses
        localizationService.GetString("Action_SelectUnitToMove").Returns("Select unit to move");
        localizationService.GetString("Action_SelectMovementType").Returns("Select movement type");
        localizationService.GetString("Action_SelectTargetHex").Returns("Select target hex");
        localizationService.GetString("Action_SelectFacingDirection").Returns("Select facing direction");
        localizationService.GetString("Action_MoveUnit").Returns("Move Unit");
        localizationService.GetString("Action_StandStill").Returns("Stand Still");
        localizationService.GetString("Action_MovementPoints").Returns("{0} | MP: {1}");
        localizationService.GetString("MovementType_Walk").Returns("Walk");
        localizationService.GetString("MovementType_Run").Returns("Run");
        localizationService.GetString("MovementType_Jump").Returns("Jump");
        
        _battleMapViewModel = new BattleMapViewModel(imageService, localizationService);
        var playerId = Guid.NewGuid();
        
        
        var rules = new ClassicBattletechRulesProvider();
        _unitData = MechFactoryTests.CreateDummyMechData();
        var ct = _unitData.LocationEquipment[PartLocation.CenterTorso];
        ct.AddRange(MakaMekComponent.JumpJet,MakaMekComponent.JumpJet);
        _unit1 = new MechFactory(rules).Create(_unitData);
        _unit2 = new MechFactory(rules).Create(_unitData);
        
        // Create two adjacent hexes
        _hex1 = new Hex(new HexCoordinates(1, 1));
        
        var battleMap = BattleMapTests.BattleMapFactory.GenerateMap(
            2, 11,
            new SingleTerrainGenerator(2,11, new ClearTerrain()));
         _player = new Player(playerId, "Player1");
        _game = new ClientGame(
            rules,
            Substitute.For<ICommandPublisher>(),
            Substitute.For<IToHitCalculator>(),
            Substitute.For<IBattleMapFactory>());
        
        _game.JoinGameWithUnits(_player,[]);
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
            PlayerId = _player.Id
        });
        _game.HandleCommand(new JoinGameCommand
        {
            PlayerName = "Player2",
            Units = [_unitData],
            Tint = "#FFFF00",
            GameOriginId = Guid.NewGuid(),
            PlayerId = playerId2
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
        _sut.CurrentMovementStep.ShouldBe(MovementStep.SelectingUnit); // Back to initial step
        _battleMapViewModel.Game.BattleMap.GetHexes()
            .Any(h => h.IsHighlighted)
            .ShouldBeFalse(); // No highlighted hexes
    }

    [Fact]
    public void GetAvailableActions_NoSelectedUnit_ReturnsEmpty()
    {
        // Act
        var actions = _sut.GetAvailableActions();

        // Assert
        actions.ShouldBeEmpty();
    }

    [Fact]
    public void GetAvailableActions_NotInMovementTypeSelection_ReturnsEmpty()
    {
        // Arrange
        _sut.HandleUnitSelection(_unit1);
        _sut.HandleMovementTypeSelection(MovementType.Walk); // This moves us past movement type selection

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
        actions.Count.ShouldBe(4); // Stand Still, Walk, Run, Jump (since unit has jump jets)
        actions[0].Label.ShouldBe("Stand Still");
        actions[1].Label.ShouldBe($"Walk | MP: {_unit1.GetMovementPoints(MovementType.Walk)}");
        actions[2].Label.ShouldBe($"Run | MP: {_unit1.GetMovementPoints(MovementType.Run)}");
        actions[3].Label.ShouldBe($"Jump | MP: {_unit1.GetMovementPoints(MovementType.Jump)}");
    }

    [Fact]
    public void GetAvailableActions_NoJumpJets_DoesNotShowJumpOption()
    {
        // Arrange
        var unitData = MechFactoryTests.CreateDummyMechData();
        var rules = new ClassicBattletechRulesProvider();
        var unitWithoutJumpJets = new MechFactory(rules).Create(unitData);
        _sut.HandleUnitSelection(unitWithoutJumpJets);

        // Act
        var actions = _sut.GetAvailableActions().ToList();

        // Assert
        actions.Count.ShouldBe(3); // Stand Still, Walk, Run
        actions.ShouldNotContain(a => a.Label.StartsWith("Jump"));
    }

    [Fact]
    public void GetAvailableActions_ExecutingAction_UpdatesState()
    {
        // Arrange
        _sut.HandleUnitSelection(_unit1);
        var actions = _sut.GetAvailableActions().ToList();
        var walkAction = actions.First(a => a.Label.StartsWith("Walk"));

        // Act
        walkAction.OnExecute();

        // Assert
        _sut.CurrentMovementStep.ShouldBe(MovementStep.SelectingTargetHex);
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
}
