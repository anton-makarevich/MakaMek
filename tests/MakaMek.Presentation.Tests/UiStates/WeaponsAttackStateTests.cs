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
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Attack;
using Sanet.MakaMek.Core.Models.Game.Phases;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Map.Factory;
using Sanet.MakaMek.Core.Models.Map.Terrains;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components.Internal;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons.Energy;
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

public class WeaponsAttackStateTests
{
    private readonly WeaponsAttackState _sut;
    private readonly ClientGame _game;
    private readonly UnitData _unitData;
    private readonly Unit _unit1;
    private readonly Unit _unit2;
    private readonly Player _player;
    private readonly BattleMapViewModel _battleMapViewModel;
    private readonly IToHitCalculator _toHitCalculator = Substitute.For<IToHitCalculator>();
    private readonly ICommandPublisher _commandPublisher = Substitute.For<ICommandPublisher>(); 
    private readonly ILocalizationService _localizationService = Substitute.For<ILocalizationService>();
    private readonly MechFactory _mechFactory;
    private readonly IPilot _pilot = Substitute.For<IPilot>();

    public WeaponsAttackStateTests()
    {
        var imageService = Substitute.For<IImageService>();
        
        // Mock localization service responses
        _localizationService.GetString("Action_SelectUnitToFire").Returns("Select unit to fire weapons");
        _localizationService.GetString("Action_SelectAction").Returns("Select action");
        _localizationService.GetString("Action_ConfigureWeapons").Returns("Configure weapons");
        _localizationService.GetString("Action_SelectTarget").Returns("Select Target");
        _localizationService.GetString("Action_TurnTorso").Returns("Turn Torso");
        _localizationService.GetString("Action_SkipAttack").Returns("Skip Attack");
        _localizationService.GetString("Action_DeclareAttack").Returns("Declare Attack");
        
        _battleMapViewModel = new BattleMapViewModel(imageService, _localizationService,Substitute.For<IDispatcherService>());
        var playerId = Guid.NewGuid();

        var rules = new ClassicBattletechRulesProvider();
        _mechFactory = new MechFactory(
            rules,
            new ClassicBattletechComponentProvider(),
            _localizationService);
        
        _unitData = MechFactoryTests.CreateDummyMechData();
        _unit1 = _mechFactory.Create(_unitData);
        _unit2 = _mechFactory.Create(_unitData);
        
        _pilot.IsConscious.Returns(true);
        _unit1.AssignPilot(_pilot);
        
        var battleMap = BattleMapTests.BattleMapFactory.GenerateMap(
            11, 11,
            new SingleTerrainGenerator(11, 11, new ClearTerrain()));
        _player = new Player(playerId, "Player1");
        _game = new ClientGame(
            rules,
            _mechFactory,
            _commandPublisher,
            _toHitCalculator,
            Substitute.For<IPilotingSkillCalculator>(),
            Substitute.For<IConsciousnessCalculator>(),
            Substitute.For<IHeatEffectsCalculator>(),
            Substitute.For<IBattleMapFactory>());
        _game.JoinGameWithUnits(_player,[],[]);
        _game.SetBattleMap(battleMap);

        var expectedModifiers = new ToHitBreakdown
        {
            GunneryBase = new GunneryRollModifier { Value = 4 },
            AttackerMovement = new AttackerMovementModifier { Value = 0, MovementType = MovementType.StandingStill },
            TargetMovement = new TargetMovementModifier { Value = 0, HexesMoved = 1 },
            OtherModifiers = [],
            RangeModifier = new RangeRollModifier
                { Value = 0, Range = WeaponRange.Medium, Distance = 5, WeaponName = "Test" },
            TerrainModifiers = [],
            HasLineOfSight = true
        };
        
        _toHitCalculator.GetModifierBreakdown(
                Arg.Any<Unit>(), 
                Arg.Any<Unit>(), 
                Arg.Any<Weapon>(), 
                Arg.Any<BattleMap>(),
                Arg.Any<bool>())
            .Returns(expectedModifiers);
        
        _battleMapViewModel.Game = _game;
        AddPlayerUnits();
        SetActivePlayer();
        _sut = new WeaponsAttackState(_battleMapViewModel);
    }

    private void AddPlayerUnits()
    {
        var playerId2 = Guid.NewGuid();
        _game.HandleCommand(new JoinGameCommand
        {
            PlayerName = "Player1",
            Units = [_unitData, _unitData],
            Tint = "#FF0000",
            GameOriginId = Guid.NewGuid(),
            PlayerId = _player.Id,
            PilotAssignments = []
        });
        _game.HandleCommand(new JoinGameCommand
        {
            PlayerName = "Player2",
            Units = [_unitData,_unitData],
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
    public void InitialState_HasSelectUnitAction()
    {
        // Assert
        _sut.ActionLabel.ShouldBe("Select unit to fire weapons");
        _sut.IsActionRequired.ShouldBeTrue();
    }

    [Fact]
    public void HandleUnitSelection_TransitionsToActionSelection()
    {
        // Act
        _sut.HandleUnitSelection(_unit1);

        // Assert
        _sut.ActionLabel.ShouldBe("Select action");
        _sut.CurrentStep.ShouldBe(WeaponsAttackStep.ActionSelection);
    }

    [Fact]
    public void HandleHexSelection_SelectsUnit_WhenUnitIsOnHex()
    {
        // Arrange
        SetPhase(PhaseNames.WeaponsAttack);
        SetActivePlayer();
        var position = new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom);
        var unit = _battleMapViewModel.Units.First(u => u.Owner!.Id == _player.Id);
        unit.Deploy(position);
        var hex = new Hex(position.Coordinates);

        // Act
        _sut.HandleHexSelection(hex);

        // Assert
        _battleMapViewModel.SelectedUnit.ShouldBe(unit);
    }

    [Fact]
    public void HandleHexSelection_SelectsUnit_WhenUnitIsOnHex_AndOtherUnitIsSelected()
    {
        // Arrange
        SetPhase(PhaseNames.WeaponsAttack);
        SetActivePlayer();
        var position = new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom);
        var unit = _battleMapViewModel.Units.First();
        unit.Deploy(position);
        var hex = new Hex(position.Coordinates);
        _battleMapViewModel.SelectedUnit = _unit2;

        // Act
        _sut.HandleHexSelection(hex);

        // Assert
        _battleMapViewModel.SelectedUnit.ShouldBe(unit);
    }

    [Fact]
    public void HandleHexSelection_DoesNotSelectUnit_WhenUnitHasFiredWeapons()
    {
        // Arrange
        SetPhase(PhaseNames.WeaponsAttack);
        SetActivePlayer();
        var state = _battleMapViewModel.CurrentState;
        var position = new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom);
        var unit = _battleMapViewModel.Units.First();
        unit.Deploy(position);
        unit.DeclareWeaponAttack([]);
        var hex = new Hex(position.Coordinates);

        // Act
        state.HandleHexSelection(hex);

        // Assert
        _battleMapViewModel.SelectedUnit.ShouldBeNull();
    }

    [Fact]
    public void HandleHexSelection_SelectsEnemyTarget_WhenInTargetSelectionStep_AndInRange()
    {
        // Arrange
        SetPhase(PhaseNames.WeaponsAttack);
        SetActivePlayer();
        
        // Setup attacker (player's unit)
        var attackerPosition = new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom);
        var attacker = _battleMapViewModel.Units.First(u => u.Owner!.Id == _player.Id);
        attacker.Deploy(attackerPosition);
        attacker.Parts[PartLocation.LeftTorso].TryAddComponent(new MediumLaser(),[1]).ShouldBeTrue();
        attacker.AssignPilot(_pilot);
        _sut.HandleUnitSelection(attacker);
        
        // Setup target (enemy unit)
        var targetPosition = new HexPosition(new HexCoordinates(2, 1), HexDirection.Bottom);
        var target = _battleMapViewModel.Units.First(u => u.Owner!.Id != _player.Id);
        target.Deploy(targetPosition);
        
        // Activate target selection
        var targetSelectionAction = _sut.GetAvailableActions()
            .First(a => a.Label == "Select Target");
        targetSelectionAction.OnExecute();
        
        // Create hex with an enemy unit
        var targetHex = new Hex(targetPosition.Coordinates);

        // Act
        _sut.HandleHexSelection(targetHex);
        _sut.HandleUnitSelection(target);

        // Assert
        _sut.CurrentStep.ShouldBe(WeaponsAttackStep.TargetSelection);
        _battleMapViewModel.SelectedUnit.ShouldBe(target);
        _sut.Attacker.ShouldBe(attacker);
        _sut.SelectedTarget.ShouldBe(target);
    }

    [Fact]
    public void HandleHexSelection_DoesNotSelectFriendlyUnit_WhenInTargetSelectionStep()
    {
        // Arrange
        SetPhase(PhaseNames.WeaponsAttack);
        SetActivePlayer();

        // Setup attacker (player's unit) with a weapon
        var attackerPosition = new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom);
        var attacker = _battleMapViewModel.Units.First(u => u.Owner!.Id == _player.Id);
        attacker.Deploy(attackerPosition);
        attacker.Parts[PartLocation.LeftTorso].TryAddComponent(new MediumLaser(),[1]).ShouldBeTrue();
        attacker.AssignPilot(_pilot);
        _sut.HandleUnitSelection(attacker);

        // Set up another friendly unit within weapon range
        var friendlyPosition = new HexPosition(new HexCoordinates(1, 2), HexDirection.Bottom);
        var friendlyUnit = _battleMapViewModel.Units.First(u => u.Owner!.Id == _player.Id && u != attacker);
        friendlyUnit.Deploy(friendlyPosition);

        // Activate target selection
        var targetSelectionAction = _sut.GetAvailableActions()
            .First(a => a.Label == "Select Target");
        targetSelectionAction.OnExecute();

        // Create hex with a friendly unit (within weapon range, so it's highlighted)
        var friendlyHex = new Hex(friendlyPosition.Coordinates);

        // Act
        _sut.HandleHexSelection(friendlyHex);

        // Assert - should stay in target selection but not select the friendly unit
        _sut.CurrentStep.ShouldBe(WeaponsAttackStep.TargetSelection);
        _battleMapViewModel.SelectedUnit.ShouldBeNull();
        _sut.Attacker.ShouldBe(attacker);
        _sut.SelectedTarget.ShouldBeNull();
    }

    [Fact]
    public void HandleHexSelection_DoesNotSelectEnemyUnit_WhenNotInTargetSelectionStep()
    {
        // Arrange
        SetPhase(PhaseNames.WeaponsAttack);
        SetActivePlayer();
        
        // Set up an enemy unit
        var enemyPosition = new HexPosition(new HexCoordinates(2, 1), HexDirection.Bottom);
        var enemyUnit = _battleMapViewModel.Units.First(u => u.Owner!.Id != _player.Id);
        enemyUnit.Deploy(enemyPosition);
        
        // Create hex with an enemy unit
        var enemyHex = new Hex(enemyPosition.Coordinates);

        // Act
        _sut.HandleHexSelection(enemyHex);

        // Assert
        _sut.CurrentStep.ShouldBe(WeaponsAttackStep.SelectingUnit);
        _battleMapViewModel.SelectedUnit.ShouldBeNull();
        _sut.SelectedTarget.ShouldBeNull();
    }

    [Fact]
    public void HandleHexSelection_CancelsTargetSelection_WhenClickingOutOfWeaponRange()
    {
        // Arrange
        SetPhase(PhaseNames.WeaponsAttack);
        SetActivePlayer();

        // Setup attacker (player's unit)
        var attackerPosition = new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom);
        var attacker = _battleMapViewModel.Units.First(u => u.Owner!.Id == _player.Id);
        attacker.Deploy(attackerPosition);
        attacker.AssignPilot(_pilot);
        _sut.HandleUnitSelection(attacker);

        // Set up an enemy unit far away (out of weapon range)
        var enemyPosition = new HexPosition(new HexCoordinates(10, 10), HexDirection.Bottom);
        var enemyUnit = _battleMapViewModel.Units.First(u => u.Owner!.Id != _player.Id);
        enemyUnit.Deploy(enemyPosition);

        // Activate target selection
        var targetSelectionAction = _sut.GetAvailableActions()
            .First(a => a.Label == "Select Target");
        targetSelectionAction.OnExecute();

        // Create hex with an enemy unit
        var enemyHex = new Hex(enemyPosition.Coordinates);

        // Act
        _sut.HandleHexSelection(enemyHex);

        // Assert - should cancel target selection when clicking outside weapon range
        _sut.CurrentStep.ShouldBe(WeaponsAttackStep.ActionSelection);
        _battleMapViewModel.SelectedUnit.ShouldBeNull();
        _sut.Attacker.ShouldBe(attacker);
        _sut.SelectedTarget.ShouldBeNull();
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
    public void GetAvailableActions_NotInActionSelectionStep_ReturnsEmpty()
    {
        // Arrange
        _unit1.Deploy(new HexPosition(1, 1, HexDirection.Bottom));
        _sut.HandleUnitSelection(_unit1);
        IEnumerable<StateAction> actions = _sut.GetAvailableActions().ToList();
        var torsoAction = actions.First(a => a.Label == "Turn Torso");
        torsoAction.OnExecute(); // This puts us in the WeaponsConfiguration step

        // Act
        actions = _sut.GetAvailableActions();

        // Assert
        actions.ShouldBeEmpty();
    }

    [Fact]
    public void GetAvailableActions_InActionSelection_ReturnsTorsoAndTargetOptions()
    {
        // Arrange
        _sut.HandleUnitSelection(_unit1);

        // Act
        var actions = _sut.GetAvailableActions().ToList();

        // Assert
        actions.Count.ShouldBe(3); // Turn Torso, Select Target, Skip Attack
        actions[0].Label.ShouldBe("Skip Attack");
        actions[1].Label.ShouldBe("Turn Torso");
        actions[2].Label.ShouldBe("Select Target");
    }
    
    [Fact]
    public void GetAvailableActions_InActionSelection_ReturnsSkipOnly_WhenUnitIsImmobile()
    {
        // Arrange
        _sut.HandleUnitSelection(_unit1);
        var shutdownData = new ShutdownData { Reason = ShutdownReason.Voluntary, Turn = 1 };
        _unit1.Shutdown(shutdownData);

        // Act
        var actions = _sut.GetAvailableActions().ToList();

        // Assert
        actions.Count.ShouldBe(1); // Turn Torso, Select Target, Skip Attack
        actions[0].Label.ShouldBe("Skip Attack");
    }
    
    [Fact]
    public void GetAvailableActions_InActionSelection_ShouldNotReturnTarget_WhenCanNotFire()
    {
        // Arrange
        _sut.HandleUnitSelection(_unit1);
        // Destroy sensors
        var sensors = _unit1.GetAllComponents<Sensors>().First();
        sensors.Hit(); 
        sensors.Hit();

        // Act
        var actions = _sut.GetAvailableActions().ToList();

        // Assert
        actions.Count.ShouldBe(2); // Turn Torso, Skip Attack (Select Target excluded when sensors destroyed)
        actions[0].Label.ShouldBe("Skip Attack");
        actions[1].Label.ShouldBe("Turn Torso");
    }

    [Fact]
    public void GetAvailableActions_TorsoRotationAction_TransitionsToWeaponsConfiguration()
    {
        // Arrange
        _unit1.Deploy(new HexPosition(1, 1, HexDirection.Bottom));
        _sut.HandleUnitSelection(_unit1);
        var actions = _sut.GetAvailableActions().ToList();
        var torsoAction = actions.First(a => a.Label == "Turn Torso");

        // Act
        torsoAction.OnExecute();

        // Assert
        _sut.CurrentStep.ShouldBe(WeaponsAttackStep.WeaponsConfiguration);
        _sut.ActionLabel.ShouldBe("Configure weapons");
    }

    [Fact]
    public void GetAvailableActions_SelectTargetAction_TransitionsToTargetSelection()
    {
        // Arrange
        _sut.HandleUnitSelection(_unit1);
        var actions = _sut.GetAvailableActions().ToList();
        var selectTargetAction = actions.First(a => a.Label == "Select Target");

        // Act
        selectTargetAction.OnExecute();

        // Assert
        _sut.CurrentStep.ShouldBe(WeaponsAttackStep.TargetSelection);
        _sut.ActionLabel.ShouldBe("Select Target");
    }

    [Fact]
    public void HandleFacingSelection_HidesDirectionSelector()
    {
        // Arrange
        _unit1.Deploy(new HexPosition(1, 1, HexDirection.Bottom));
        _sut.HandleUnitSelection(_unit1);
        var actions = _sut.GetAvailableActions().ToList();
        var torsoAction = actions.First(a => a.Label == "Turn Torso");
        torsoAction.OnExecute();

        // Act
        _sut.HandleFacingSelection(HexDirection.BottomLeft);

        // Assert
        _battleMapViewModel.IsDirectionSelectorVisible.ShouldBeFalse();
        _sut.ActionLabel.ShouldBe("Select action");
    }

    [Fact]
    public void HandleUnitSelection_HighlightsWeaponRanges_WhenUnitIsSelected()
    {
        // Arrange
        var position = new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom);
        _unit1.Deploy(position);
        _unit1.Parts[PartLocation.LeftTorso].TryAddComponent(new MediumLaser(),[1]).ShouldBeTrue();
        
        // Act
        _sut.HandleUnitSelection(_unit1);

        // Assert
        var highlightedHexes = _game.BattleMap!.GetHexes().Where(h => h.IsHighlighted).ToList();
        highlightedHexes.ShouldNotBeEmpty();
    }
    
    [Fact]
    public void HandleUnitSelection_ShouldNotHighlightHexes_WhenUnitIsImmobile()
    {
        // Arrange
        var position = new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom);
        _unit1.Deploy(position);
        _unit1.Shutdown(new ShutdownData{Reason = ShutdownReason.Voluntary, Turn = 0}); // Make the unit immobile
        
        // Act
        _sut.HandleUnitSelection(_unit1);

        // Assert
        var highlightedHexes = _game.BattleMap!.GetHexes().Where(h => h.IsHighlighted).ToList();
        highlightedHexes.ShouldBeEmpty("No hexes should be highlighted for an immobile unit");
    }

    [Fact]
    public void HandleUnitSelection_ClearsPreviousHighlights_WhenSelectingNewUnit()
    {
        // Arrange
        var position1 = new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom);
        var position2 = new HexPosition(new HexCoordinates(2, 2), HexDirection.Bottom);
        var unit1= _battleMapViewModel.Units.First(u => u.Owner!.Id == _player.Id);
        var unit2 = _battleMapViewModel.Units.Last(u => u.Owner!.Id == _player.Id);
        unit1.Deploy(position1);
        unit1.Parts[PartLocation.LeftTorso].TryAddComponent(new MediumLaser(),[1]).ShouldBeTrue();
        unit2.AssignPilot(_pilot);
        unit2.Parts[PartLocation.LeftTorso].TryAddComponent(new MediumLaser(),[1]).ShouldBeTrue();
        unit2.Deploy(position2);
        _sut.HandleHexSelection(_game.BattleMap!.GetHexes().First(h=>h.Coordinates==position1.Coordinates));
        _sut.HandleUnitSelection(unit1);
        var firstHighlightedHexes = _game.BattleMap.GetHexes().Where(h => h.IsHighlighted).ToList();

        // Act
        _sut.HandleHexSelection(_game.BattleMap.GetHexes().First(h=>h.Coordinates==position2.Coordinates));
        _sut.HandleUnitSelection(unit2);

        // Assert
        var secondHighlightedHexes = _game.BattleMap.GetHexes().Where(h => h.IsHighlighted).ToList();
        secondHighlightedHexes.ShouldNotBeEmpty();
        // Check that at least some hexes are different (since units are in different positions)
        secondHighlightedHexes.ShouldNotBe(firstHighlightedHexes);
    }

    [Fact]
    public void ResetUnitSelection_ClearsWeaponRangeHighlights()
    {
        // Arrange
        var position1 = new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom);
        var position2 = new HexPosition(new HexCoordinates(2, 2), HexDirection.Bottom);
        _unit1.Deploy(position1);
        _unit1.Parts[PartLocation.LeftTorso].TryAddComponent(new MediumLaser(),[1]).ShouldBeTrue();
        _unit2.Deploy(position2);
        _unit2.Parts[PartLocation.LeftTorso].TryAddComponent(new MediumLaser(),[1]).ShouldBeTrue();
        _unit1.AssignPilot(_pilot);
        _unit2.AssignPilot(new MechWarrior("Test", "Pilot"));
        
        // Act
        _sut.HandleUnitSelection(_unit1);
        var firstUnitHighlightedHexes = _game.BattleMap!.GetHexes().Where(h => h.IsHighlighted).ToList();
        _sut.HandleUnitSelection(_unit2);
        var secondUnitHighlightedHexes = _game.BattleMap.GetHexes().Where(h => h.IsHighlighted).ToList();

        // Assert
        firstUnitHighlightedHexes.ShouldNotBeEmpty();
        secondUnitHighlightedHexes.ShouldNotBeEmpty();
        secondUnitHighlightedHexes.ShouldNotBe(firstUnitHighlightedHexes);
    }

    [Fact]
    public void HandleTorsoRotation_UpdatesWeaponRangeHighlights()
    {
        // Arrange
        var position = new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom);
        _unit1.Deploy(position);
        _unit1.Parts[PartLocation.LeftTorso].TryAddComponent(new MediumLaser(),[1]).ShouldBeTrue();
        _sut.HandleUnitSelection(_unit1);
        var firstHighlightedHexes = _game.BattleMap!.GetHexes().Where(h => h.IsHighlighted).ToList();
        
        // Act
        ((Mech)_unit1).RotateTorso(HexDirection.BottomLeft);
        _sut.HandleTorsoRotation(_unit1.Id);
        var secondHighlightedHexes = _game.BattleMap.GetHexes().Where(h => h.IsHighlighted).ToList();

        // Assert
        firstHighlightedHexes.ShouldNotBeEmpty();
        secondHighlightedHexes.ShouldNotBeEmpty();
        // Since we rotated the torso, the highlighted hexes should be different
        secondHighlightedHexes.ShouldNotBe(firstHighlightedHexes);
    }

    [Fact]
    public void HandleTorsoRotation_DoesNotUpdateHighlights_WhenDifferentUnit()
    {
        // Arrange
        var position = new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom);
        _unit1.Deploy(position);
        _unit1.Parts[PartLocation.LeftTorso].TryAddComponent(new MediumLaser(),[1]).ShouldBeTrue();
        _sut.HandleUnitSelection(_unit1);
        var firstHighlightedHexes = _game.BattleMap!.GetHexes().Where(h => h.IsHighlighted).ToList();
        
        // Act
        ((Mech)_unit1).RotateTorso(HexDirection.BottomLeft);
        _sut.HandleTorsoRotation(Guid.NewGuid()); // Different unit ID
        var secondHighlightedHexes = _game.BattleMap.GetHexes().Where(h => h.IsHighlighted).ToList();

        // Assert
        firstHighlightedHexes.ShouldNotBeEmpty();
        secondHighlightedHexes.ShouldNotBeEmpty();
        // Since we tried to rotate a different unit's torso, the highlighted hexes should remain the same
        secondHighlightedHexes.ShouldBe(firstHighlightedHexes);
    }

    [Fact]
    public void HandleUnitSelection_DoesNotHighlightRanges_WhenUnitHasNoWeapons()
    {
        // Arrange
        var position = new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom);
        var unitDataNoWeapons = MechFactoryTests.CreateDummyMechData();
        // Remove all weapons from the unit
        unitDataNoWeapons = unitDataNoWeapons with
        {
           Equipment = []
        };
        var unitNoWeapons = _mechFactory.Create(unitDataNoWeapons);
        unitNoWeapons.Deploy(position);

        // Act
        _sut.HandleUnitSelection(unitNoWeapons);

        // Assert
        var highlightedHexes = _game.BattleMap!.GetHexes().Where(h => h.IsHighlighted).ToList();
        highlightedHexes.ShouldBeEmpty();
    }

    [Fact]
    public void HandleUnitSelection_HighlightsWeaponRanges_FromDifferentLocations()
    {
        // Arrange
        var position = new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom);
        // Overwrite unit data with weapons in different locations
        var unitData = MechFactoryTests.CreateDummyMechData() with
        {
            Equipment = [
                new ComponentData
                {
                    Type = MakaMekComponent.LRM5,
                    Assignments = [new LocationSlotAssignment(PartLocation.LeftTorso, 1, 1)]
                },
                new ComponentData
                {
                    Type = MakaMekComponent.MediumLaser,
                    Assignments = [new LocationSlotAssignment(PartLocation.RightTorso, 1, 1)]
                },
                new ComponentData
                {
                    Type = MakaMekComponent.MediumLaser,
                    Assignments = [new LocationSlotAssignment(PartLocation.CenterTorso, 1, 1)]
                },
                new ComponentData
                {
                    Type = MakaMekComponent.MediumLaser,
                    Assignments = [new LocationSlotAssignment(PartLocation.LeftLeg, 1, 1)]
                },
                new ComponentData
                {
                    Type = MakaMekComponent.MediumLaser,
                    Assignments = [new LocationSlotAssignment(PartLocation.RightLeg, 1, 1)]
                }
            ]
        };

        var unit = _mechFactory.Create(unitData);
        unit.AssignPilot(_pilot);
        unit.Deploy(position);

        // Act
        _sut.HandleUnitSelection(unit);

        // Assert
        var highlightedHexes = _game.BattleMap!.GetHexes().Where(h => h.IsHighlighted).ToList();
        highlightedHexes.ShouldNotBeEmpty();
    }

    [Fact]
    public void GetWeaponsInRange_ReturnsEmptyList_WhenNoUnitSelected()
    {
        // Arrange
        var targetCoordinates = new HexCoordinates(2, 2);

        // Act
        var weapons = _sut.GetWeaponsInRange(targetCoordinates);

        // Assert
        weapons.ShouldBeEmpty();
    }

    [Fact]
    public void GetWeaponsInRange_ReturnsWeaponsInRange_WhenTargetInForwardArc()
    {
        // Arrange
        var position = new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom);
        _unit1.Deploy(position);
        _unit1.Parts[PartLocation.LeftTorso].TryAddComponent(new MediumLaser(),[1]).ShouldBeTrue();
        _sut.HandleUnitSelection(_unit1);
        var targetCoordinates = new HexCoordinates(1, 3); // Two hexes directly in front

        // Act
        var weapons = _sut.GetWeaponsInRange(targetCoordinates);

        // Assert
        weapons.ShouldNotBeEmpty();
        // All weapons from torso and arms should be able to fire forward
        weapons.Count.ShouldBe(_unit1.Parts
            .Where(p => p.Value.Location 
                is PartLocation.LeftArm 
                or PartLocation.RightArm 
                or PartLocation.LeftTorso 
                or PartLocation.RightTorso
                or PartLocation.CenterTorso)
            .SelectMany(p => p.Value.GetComponents<Weapon>())
            .Count());
    }

    [Fact]
    public void GetWeaponsInRange_ReturnsNoWeapons_WhenTargetOutOfRange()
    {
        // Arrange
        var position = new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom);
        _unit1.Deploy(position);
        _unit1.Parts[PartLocation.LeftTorso].TryAddComponent(new MediumLaser(),[1]).ShouldBeTrue();
        _sut.HandleUnitSelection(_unit1);
        // Get maximum weapon range
        var maxRange = _unit1.Parts.Values
            .SelectMany(p => p.GetComponents<Weapon>())
            .Max(w => w.LongRange);
        var targetCoordinates = new HexCoordinates(1, maxRange + 2); // Beyond maximum range

        // Act
        var weapons = _sut.GetWeaponsInRange(targetCoordinates);

        // Assert
        weapons.ShouldBeEmpty();
    }

    [Fact]
    public void GetWeaponSelectionItems_WhenNoAttackerOrTarget_ReturnsEmptyList()
    {
        // Act
        var items = _sut.WeaponSelectionItems;

        // Assert
        items.ShouldBeEmpty();
    }

    [Fact]
    public void GetWeaponSelectionItems_WhenAttackerSelected_CreatesViewModelsForAllWeapons()
    {
        // Arrange
        var unit = _battleMapViewModel.Units.First(u => u.Owner!.Id == _player.Id);
        unit.Parts[PartLocation.LeftTorso].TryAddComponent(new MediumLaser(),[1]).ShouldBeTrue();
        var position = new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom);
        unit.Deploy(position);
        
        _sut.HandleHexSelection(_game.BattleMap!.GetHexes().First(h=>h.Coordinates==position.Coordinates));
        _sut.HandleUnitSelection(unit);

        // Act
        var items = _sut.WeaponSelectionItems.ToList();

        // Assert
        items.ShouldNotBeEmpty();
        items.Count.ShouldBe(unit.Parts.Values.Sum(p => p.GetComponents<Weapon>().Count()));
        items.All(i => !i.IsEnabled).ShouldBeTrue();
        items.All(i => !i.IsSelected).ShouldBeTrue();
        items.All(i => i.Target == null).ShouldBeTrue();
    }
    
    [Fact]
    public void GetWeaponSelectionItems_WhenTargetIsNotSelected_UpdatesAvailabilityBasedOnRange()
    {
        // Arrange
        var attacker = _battleMapViewModel.Units.First(u => u.Owner!.Id == _player.Id);
        
        // Place units next to each other
        var attackerPosition = new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom);
        attacker.Deploy(attackerPosition);
        attacker.Parts[PartLocation.LeftTorso].TryAddComponent(new MediumLaser(),[1]).ShouldBeTrue();
        
        _sut.HandleHexSelection(_game.BattleMap!.GetHexes().First(h=>h.Coordinates==attackerPosition.Coordinates));
        _sut.HandleUnitSelection(attacker);

        // Act
        var item = _sut.WeaponSelectionItems.First();
        item.IsSelected = true;

        // Assert
        item.IsSelected.ShouldBeFalse();
        item.Target.ShouldBeNull();
    }

    [Fact]
    public void GetWeaponSelectionItems_WhenTargetSelected_UpdatesAvailabilityBasedOnRange()
    {
        // Arrange
        var attacker = _battleMapViewModel.Units.First(u => u.Owner!.Id == _player.Id);
        var target = _battleMapViewModel.Units.First(u => u.Owner!.Id != _player.Id);
        
        // Place units next to each other
        var attackerPosition = new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom);
        attacker.Deploy(attackerPosition);
        attacker.Parts[PartLocation.LeftTorso].TryAddComponent(new MediumLaser(),[1]).ShouldBeTrue();
        attacker.AssignPilot(_pilot);
        var targetPosition = new HexPosition(new HexCoordinates(1, 2), HexDirection.Bottom);
        target.Deploy(targetPosition);
        
        _sut.HandleHexSelection(_game.BattleMap!.GetHexes().First(h=>h.Coordinates==attackerPosition.Coordinates));
        _sut.HandleUnitSelection(attacker);
        var selectTargetAction = _sut.GetAvailableActions().First(a => a.Label == "Select Target");
        selectTargetAction.OnExecute();
        _sut.HandleHexSelection(_game.BattleMap.GetHexes().First(h=>h.Coordinates==targetPosition.Coordinates));
        _sut.HandleUnitSelection(target);

        // Act
        var items = _sut.WeaponSelectionItems.ToList();

        // Assert
        items.ShouldNotBeEmpty();
        items.Any(i => i.IsEnabled).ShouldBeTrue(); // At least one weapon should be in range
        items.All(i => i.IsSelected == false).ShouldBeTrue();
        items.All(i => i.Target == null).ShouldBeTrue();
    }

    [Fact]
    public void HandleWeaponSelection_WhenWeaponSelected_AssignsTargetAndUpdatesViewModel()
    {
        // Arrange
        var attacker = _battleMapViewModel.Units.First(u => u.Owner!.Id == _player.Id);
        var target = _battleMapViewModel.Units.First(u => u.Owner!.Id != _player.Id);
        
        // Place units next to each other
        var attackerPosition = new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom);
        attacker.Deploy(attackerPosition);
        attacker.Parts[PartLocation.LeftTorso].TryAddComponent(new MediumLaser(),[1]).ShouldBeTrue();
        attacker.AssignPilot(_pilot);
        var targetPosition = new HexPosition(new HexCoordinates(1, 2), HexDirection.Bottom);
        target.Deploy(targetPosition);
        
        _sut.HandleHexSelection(_game.BattleMap!.GetHexes().First(h=>h.Coordinates==attackerPosition.Coordinates));
        _sut.HandleUnitSelection(attacker);
        var selectTargetAction = _sut.GetAvailableActions().First(a => a.Label == "Select Target");
        selectTargetAction.OnExecute();
        _sut.HandleHexSelection(_game.BattleMap.GetHexes().First(h=>h.Coordinates==targetPosition.Coordinates));
        _sut.HandleUnitSelection(target);

        var weapon = attacker.Parts.Values
            .SelectMany(p => p.GetComponents<Weapon>())
            .First();
        var weaponSelection = _sut.WeaponSelectionItems.First(ws => ws.Weapon == weapon);

        // Act
        weaponSelection.IsSelected = true;

        // Assert
        weaponSelection.IsSelected.ShouldBeTrue();
        weaponSelection.Target.ShouldBe(target);
    }

    [Fact]
    public void HandleWeaponSelection_WhenWeaponDeselected_RemovesTargetAndUpdatesViewModel()
    {
        // Arrange
        var attacker = _battleMapViewModel.Units.First(u => u.Owner!.Id == _player.Id);
        var target = _battleMapViewModel.Units.First(u => u.Owner!.Id != _player.Id);
        
        // Place units next to each other
        var attackerPosition = new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom);
        attacker.Deploy(attackerPosition);
        attacker.Parts[PartLocation.LeftTorso].TryAddComponent(new MediumLaser(),[1]).ShouldBeTrue();
        attacker.AssignPilot(_pilot);
        var targetPosition = new HexPosition(new HexCoordinates(1, 2), HexDirection.Bottom);
        target.Deploy(targetPosition);
        
        _sut.HandleHexSelection(_game.BattleMap!.GetHexes().First(h=>h.Coordinates==attackerPosition.Coordinates));
        _sut.HandleUnitSelection(attacker);
        var selectTargetAction = _sut.GetAvailableActions().First(a => a.Label == "Select Target");
        selectTargetAction.OnExecute();
        _sut.HandleHexSelection(_game.BattleMap.GetHexes().First(h=>h.Coordinates==targetPosition.Coordinates));
        _sut.HandleUnitSelection(target);

        var weapon = attacker.Parts.Values
            .SelectMany(p => p.GetComponents<Weapon>())
            .First();
        var weaponSelection = _sut.WeaponSelectionItems.First(i => i.Weapon == weapon);

        // Select a weapon first
        weaponSelection.IsSelected=true;

        // Act
        weaponSelection.IsSelected=false;

        // Assert
        weaponSelection.IsSelected.ShouldBeFalse();
        weaponSelection.Target.ShouldBeNull();
    }

    [Fact]
    public void HandleWeaponSelection_WhenWeaponSelectedForDifferentTarget_DisablesWeaponForCurrentTarget()
    {
        // Arrange
        var attacker = _battleMapViewModel.Units.First(u => u.Owner!.Id == _player.Id);
        var target1 = _battleMapViewModel.Units.First(u => u.Owner!.Id != _player.Id);
        var target2 = _battleMapViewModel.Units.Last(u => u.Owner!.Id != _player.Id);
        
        // Place units in a triangle
        var attackerPosition = new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom);
        attacker.Deploy(attackerPosition);
        attacker.Parts[PartLocation.LeftTorso].TryAddComponent(new MediumLaser(),[1]).ShouldBeTrue();
        attacker.AssignPilot(_pilot);
        var target1Position = new HexPosition(new HexCoordinates(1, 2), HexDirection.Bottom);
        target1.Deploy(target1Position);
        var target2Position = new HexPosition(new HexCoordinates(1, 3), HexDirection.Bottom);
        target2.Deploy(target2Position);
        
        _sut.HandleHexSelection(_game.BattleMap!.GetHexes().First(h=>h.Coordinates==attackerPosition.Coordinates));
        _sut.HandleUnitSelection(attacker);
        var selectTargetAction = _sut.GetAvailableActions().First(a => a.Label == "Select Target");
        selectTargetAction.OnExecute();
        _sut.HandleHexSelection(_game.BattleMap.GetHexes().First(h=>h.Coordinates==target1Position.Coordinates));
        _sut.HandleUnitSelection(target1);

        var weapon = attacker.Parts.Values
            .SelectMany(p => p.GetComponents<Weapon>())
            .First();

        // Select a weapon for the first target
        var weaponSelection = _sut.WeaponSelectionItems.First(ws => ws.Weapon == weapon);
        weaponSelection.IsSelected = true;

        // Act - select a second target
        _sut.HandleHexSelection(_game.BattleMap.GetHexes().First(h=>h.Coordinates==target2Position.Coordinates));
        _sut.HandleUnitSelection(target2);

        // Assert
        weaponSelection.IsEnabled.ShouldBeFalse(); // Should be disabled because it's targeting target1
        weaponSelection.Target.ShouldBe(target1);
    }

    [Fact]
    public void UpdateWeaponViewModels_CalculatesHitProbability_ForWeaponsInRange()
    {
        // Arrange
        // Set up attacker and target units
        var attacker = _battleMapViewModel.Units.First(u => u.Owner!.Id == _player.Id);
        attacker.Parts[PartLocation.LeftTorso].TryAddComponent(new MediumLaser(),[1]).ShouldBeTrue();
        var target = _battleMapViewModel.Units.First(u => u.Owner!.Id != _player.Id);
        
        // Position units on the map
        var attackerPosition = new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom);
        var targetPosition = new HexPosition(new HexCoordinates(1, 2), HexDirection.Top);
        attacker.Deploy(attackerPosition);
        attacker.AssignPilot(_pilot);
        target.Deploy(targetPosition);
        
        // Select attacker and target
        _sut.HandleHexSelection(_game.BattleMap!.GetHexes().First(h => h.Coordinates == attackerPosition.Coordinates));
        _sut.HandleUnitSelection(attacker);
        var selectTargetAction = _sut.GetAvailableActions().First(a => a.Label == "Select Target");
        selectTargetAction.OnExecute();
        _sut.HandleHexSelection(_game.BattleMap.GetHexes().First(h => h.Coordinates == targetPosition.Coordinates));
        _sut.HandleUnitSelection(target);
        
        // Act
        var weaponItems = _sut.WeaponSelectionItems.ToList();
        
        // Assert
        weaponItems.ShouldNotBeEmpty();
        foreach (var item in weaponItems.Where(i => i.IsInRange))
        {
            // The expected probability for target number 4 is 92%
            var expectedProbability = DiceUtils.Calculate2d6Probability(item.ModifiersBreakdown!.Total);
            item.HitProbability.ShouldBeEquivalentTo(expectedProbability);
            item.HitProbabilityText.ShouldBe($"{expectedProbability:F0}%");
        }
    }
    
    [Fact]
    public void HandleHexSelection_CancelsSelection_WhenTargetOutOfRange()
    {
        // Arrange
        // Set up attacker and target units
        var attacker = _battleMapViewModel.Units.First(u => u.Owner!.Id == _player.Id);
        attacker.Parts[PartLocation.LeftTorso].TryAddComponent(new MediumLaser(),[1]).ShouldBeTrue();
        var target = _battleMapViewModel.Units.First(u => u.Owner!.Id != _player.Id);

        // Position units on the map - far apart to ensure weapons are out of range
        var attackerPosition = new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom);
        var targetPosition = new HexPosition(new HexCoordinates(10, 10), HexDirection.Top);
        attacker.Deploy(attackerPosition);
        attacker.AssignPilot(_pilot);
        target.Deploy(targetPosition);

        // Select attacker and target
        _sut.HandleHexSelection(_game.BattleMap!.GetHexes().First(h => h.Coordinates == attackerPosition.Coordinates));
        _sut.HandleUnitSelection(attacker);
        var selectTargetAction = _sut.GetAvailableActions().First(a => a.Label == "Select Target");
        selectTargetAction.OnExecute();

        // Act - try to select a target out of range
        _sut.HandleHexSelection(_game.BattleMap.GetHexes().First(h => h.Coordinates == targetPosition.Coordinates));

        // Assert - selection should be cancelled when clicking outside weapon range
        _sut.CurrentStep.ShouldBe(WeaponsAttackStep.ActionSelection);
        _sut.SelectedTarget.ShouldBeNull();
        _sut.Attacker.ShouldBe(attacker);
    }
    
    [Fact]
    public void UpdateWeaponViewModels_AppliesCorrectModifiersForPrimaryAndSecondaryTargets()
    {
        // Arrange
        var attacker = _battleMapViewModel.Units.First(u => u.Owner!.Id == _player.Id);
        var primaryTarget = _battleMapViewModel.Units.First(u => u.Owner!.Id != _player.Id);
        var secondaryTarget = _battleMapViewModel.Units.Last(u => u.Owner!.Id != _player.Id);
        
        // Position units on the map
        var attackerPosition = new HexPosition(new HexCoordinates(5, 5), HexDirection.Top);
        var primaryTargetPosition = new HexPosition(new HexCoordinates(5, 4), HexDirection.Bottom);
        var secondaryTargetPosition = new HexPosition(new HexCoordinates(7, 4), HexDirection.Bottom);
        
        attacker.Deploy(attackerPosition);
        attacker.Parts[PartLocation.LeftTorso].TryAddComponent(new MediumLaser(),[1]).ShouldBeTrue();
        attacker.Parts[PartLocation.RightTorso].TryAddComponent(new MediumLaser(),[1]).ShouldBeTrue();
        attacker.AssignPilot(_pilot);
        primaryTarget.Deploy(primaryTargetPosition);
        secondaryTarget.Deploy(secondaryTargetPosition);
        
        // Set up the calculator mock to return different breakdowns based on parameters
        var primaryBreakdown = new ToHitBreakdown
        {
            GunneryBase = new GunneryRollModifier
            {
                Value = 4
            },
            OtherModifiers = [],
            HasLineOfSight = true,
            AttackerMovement = new AttackerMovementModifier{MovementType = MovementType.Walk, Value = 1},
            TargetMovement = new TargetMovementModifier{HexesMoved = 3, Value = 1},
            RangeModifier = new RangeRollModifier
                { Value = 0, Range = WeaponRange.Medium, Distance = 5, WeaponName = "Test" },
            TerrainModifiers = []
        };
        
        var secondaryBreakdown = new ToHitBreakdown
        {
            GunneryBase = new GunneryRollModifier { Value = 4 },
            OtherModifiers = [new SecondaryTargetModifier { Value = 2, IsInFrontArc = false }],
            HasLineOfSight = true,
            AttackerMovement = new AttackerMovementModifier{MovementType = MovementType.Walk, Value = 1},
            TargetMovement = new TargetMovementModifier{HexesMoved = 3, Value = 1},
            RangeModifier = new RangeRollModifier
                { Value = 0, Range = WeaponRange.Medium, Distance = 5, WeaponName = "Test" },
            TerrainModifiers = []
        };
        
        _toHitCalculator.GetModifierBreakdown(
                Arg.Any<Unit>(), 
                Arg.Is<Unit>(t => t == primaryTarget), 
                Arg.Any<Weapon>(), 
                Arg.Any<BattleMap>(),
                Arg.Is<bool>(isPrimary => isPrimary))
            .Returns(primaryBreakdown);
            
        _toHitCalculator.GetModifierBreakdown(
                Arg.Any<Unit>(), 
                Arg.Is<Unit>(t => t == secondaryTarget), 
                Arg.Any<Weapon>(), 
                Arg.Any<BattleMap>(),
                Arg.Is<bool>(isPrimary => !isPrimary))
            .Returns(secondaryBreakdown);
        
        // Set up the state
        _sut.HandleHexSelection(_game.BattleMap!.GetHexes().First(h => h.Coordinates == attackerPosition.Coordinates));
        _sut.HandleUnitSelection(attacker);
        var selectTargetAction = _sut.GetAvailableActions().First(a => a.Label == "Select Target");
        selectTargetAction.OnExecute();
        
        // Get two different weapons to target different enemies
        var weapons = attacker.Parts.Values.SelectMany(p => p.GetComponents<Weapon>()).Take(2).ToList();
        
        // Select the primary target and first weapon
        _sut.HandleHexSelection(_game.BattleMap.GetHexes().First(h => h.Coordinates == primaryTargetPosition.Coordinates));
        _sut.HandleUnitSelection(primaryTarget);
        var weaponSelection1 = _sut.WeaponSelectionItems.First(ws => ws.Weapon == weapons[0]);
        weaponSelection1.IsSelected = true;
        // Assert
        var primaryWeaponBreakdown = weaponSelection1.ModifiersBreakdown;
        primaryWeaponBreakdown.ShouldNotBeNull();
        // A primary target should NOT have a secondary target modifier
        primaryWeaponBreakdown.AllModifiers.Any(m => m is SecondaryTargetModifier).ShouldBeFalse();
        
        // Select a secondary target and second weapon
        _sut.HandleHexSelection(_game.BattleMap.GetHexes().First(h => h.Coordinates == secondaryTargetPosition.Coordinates));
        _sut.HandleUnitSelection(secondaryTarget);
        var weaponSelection2 = _sut.WeaponSelectionItems.First(ws => ws.Weapon == weapons[1]);
        weaponSelection2.IsSelected = true;
        
        var secondaryWeaponBreakdown = weaponSelection2.ModifiersBreakdown;
        secondaryWeaponBreakdown.ShouldNotBeNull();
        
        // Secondary target SHOULD have a secondary target modifier
        var secondaryModifier = secondaryWeaponBreakdown.AllModifiers.FirstOrDefault(m => m is SecondaryTargetModifier);
        secondaryModifier.ShouldNotBeNull();
        secondaryModifier.Value.ShouldBe(2);
    }
    
    [Fact]
    public void ConfirmWeaponSelections_PublishesWeaponAttackDeclarationCommand_WhenWeaponsAreSelected()
    {
        // Arrange
        var attackingPlayer = _game.Players[0];
        var targetPlayer = _game.Players[1];
        var attacker = _battleMapViewModel.Units.First(u => u.Owner!.Id == attackingPlayer.Id);
        var target = _battleMapViewModel.Units.First(u => u.Owner!.Id == targetPlayer.Id);
        
        // Deploy units
        var attackerPosition = new HexPosition(new HexCoordinates(5, 5), HexDirection.Top);
        var targetPosition = new HexPosition(new HexCoordinates(5, 4), HexDirection.Bottom);
        attacker.Deploy(attackerPosition);
        attacker.AssignPilot(_pilot);
        attacker.Parts[PartLocation.LeftTorso].TryAddComponent(new MediumLaser(),[1]).ShouldBeTrue();
        target.Deploy(targetPosition);
        
        // Set active player
        _game.HandleCommand(new ChangeActivePlayerCommand
        {
            GameOriginId = Guid.NewGuid(),
            PlayerId = attackingPlayer.Id,
            UnitsToPlay = 1
        });
        
        // Select attacker
        _sut.HandleHexSelection(_game.BattleMap!.GetHexes().First(h => h.Coordinates == attackerPosition.Coordinates));
        _sut.HandleUnitSelection(attacker);
        
        // Select target
        var selectTargetAction = _sut.GetAvailableActions().First(a => a.Label == "Select Target");
        selectTargetAction.OnExecute();
        _sut.HandleHexSelection(_game.BattleMap.GetHexes().First(h => h.Coordinates == targetPosition.Coordinates));
        _sut.HandleUnitSelection(target);
        
        // Select a weapon
        var weapon = attacker.Parts.Values.SelectMany(p => p.GetComponents<Weapon>()).First();
        var weaponSelection = _sut.WeaponSelectionItems.First(ws => ws.Weapon == weapon);
        weaponSelection.IsSelected = true;
        
        // Act
        _sut.ConfirmWeaponSelections();
        
        // Assert
        _commandPublisher.Received(1).PublishCommand(Arg.Is<WeaponAttackDeclarationCommand>(cmd =>
            cmd.PlayerId == attackingPlayer.Id &&
            cmd.AttackerId == attacker.Id &&
            cmd.WeaponTargets.Count == 1 &&
            cmd.WeaponTargets[0].TargetId == target.Id &&
            cmd.WeaponTargets[0].IsPrimaryTarget == true &&
            cmd.WeaponTargets[0].Weapon.Name == weapon.Name
        ));
    }

    [Fact]
    public void ConfirmWeaponSelections_IncludesAimedShotTarget_WhenAimedShotSelected()
    {
        // Arrange
        var attackingPlayer = _game.Players[0];
        var targetPlayer = _game.Players[1];
        var attacker = _battleMapViewModel.Units.First(u => u.Owner!.Id == attackingPlayer.Id);
        var target = _battleMapViewModel.Units.First(u => u.Owner!.Id == targetPlayer.Id);
        
        // Deploy units
        var attackerPosition = new HexPosition(new HexCoordinates(5, 5), HexDirection.Top);
        var targetPosition = new HexPosition(new HexCoordinates(5, 4), HexDirection.Bottom);
        attacker.Deploy(attackerPosition);
        attacker.AssignPilot(_pilot);
        attacker.Parts[PartLocation.LeftTorso].TryAddComponent(new MediumLaser(),[1]).ShouldBeTrue();
        target.Deploy(targetPosition);

        // Set active player
        _game.HandleCommand(new ChangeActivePlayerCommand
        {
            GameOriginId = Guid.NewGuid(),
            PlayerId = attackingPlayer.Id,
            UnitsToPlay = 1
        });

        // Select attacker and target
        _sut.HandleHexSelection(_game.BattleMap!.GetHexes().First(h => h.Coordinates == attackerPosition.Coordinates));
        _sut.HandleUnitSelection(attacker);
        var selectTargetAction = _sut.GetAvailableActions().First(a => a.Label == "Select Target");
        selectTargetAction.OnExecute();
        _sut.HandleHexSelection(_game.BattleMap.GetHexes().First(h => h.Coordinates == targetPosition.Coordinates));
        _sut.HandleUnitSelection(target);

        // Select a weapon and set aimed shot
        var weapon = attacker.Parts.Values.SelectMany(p => p.GetComponents<Weapon>()).First();
        var weaponSelection = _sut.WeaponSelectionItems.First(ws => ws.Weapon == weapon);
        weaponSelection.IsSelected = true;
        weaponSelection.AimedShotTarget = PartLocation.Head;

        // Act
        _sut.ConfirmWeaponSelections();

        // Assert
        _commandPublisher.Received(1).PublishCommand(Arg.Is<WeaponAttackDeclarationCommand>(cmd =>
            cmd.WeaponTargets.Count == 1 &&
            cmd.WeaponTargets[0].AimedShotTarget == PartLocation.Head
        ));
    }

    [Fact]
    public void ConfirmWeaponSelections_DoesNotIncludeAimedShotTarget_WhenNormalShotSelected()
    {
        // Arrange
        var attackingPlayer = _game.Players[0];
        var targetPlayer = _game.Players[1];
        var attacker = _battleMapViewModel.Units.First(u => u.Owner!.Id == attackingPlayer.Id);
        var target = _battleMapViewModel.Units.First(u => u.Owner!.Id == targetPlayer.Id);
        
        // Deploy units
        var attackerPosition = new HexPosition(new HexCoordinates(5, 5), HexDirection.Top);
        var targetPosition = new HexPosition(new HexCoordinates(5, 4), HexDirection.Bottom);
        attacker.Deploy(attackerPosition);
        attacker.Parts[PartLocation.LeftTorso].TryAddComponent(new MediumLaser(),[1]).ShouldBeTrue();
        attacker.AssignPilot(_pilot);
        target.Deploy(targetPosition);

        // Set active player
        _game.HandleCommand(new ChangeActivePlayerCommand
        {
            GameOriginId = Guid.NewGuid(),
            PlayerId = attackingPlayer.Id,
            UnitsToPlay = 1
        });

        // Select attacker and target
        _sut.HandleHexSelection(_game.BattleMap!.GetHexes().First(h => h.Coordinates == attackerPosition.Coordinates));
        _sut.HandleUnitSelection(attacker);
        var selectTargetAction = _sut.GetAvailableActions().First(a => a.Label == "Select Target");
        selectTargetAction.OnExecute();
        _sut.HandleHexSelection(_game.BattleMap.GetHexes().First(h => h.Coordinates == targetPosition.Coordinates));
        _sut.HandleUnitSelection(target);

        // Select a weapon without aimed shot
        var weapon = attacker.Parts.Values.SelectMany(p => p.GetComponents<Weapon>()).First();
        var weaponSelection = _sut.WeaponSelectionItems.First(ws => ws.Weapon == weapon);
        weaponSelection.IsSelected = true;
        // Don't set AimedShotTarget

        // Act
        _sut.ConfirmWeaponSelections();

        // Assert
        _commandPublisher.Received(1).PublishCommand(Arg.Is<WeaponAttackDeclarationCommand>(cmd =>
            cmd.WeaponTargets.Count == 1 &&
            cmd.WeaponTargets[0].AimedShotTarget == null
        ));
    }

    [Fact]
    public void GetAvailableActions_IncludesConfirmWeaponSelectionsAction_WhenWeaponsAreSelected()
    {
        // Arrange
        var attackingPlayer = _game.Players[0];
        var targetPlayer = _game.Players[1];
        var attacker = _battleMapViewModel.Units.First(u => u.Owner!.Id == attackingPlayer.Id);
        var target = _battleMapViewModel.Units.First(u => u.Owner!.Id == targetPlayer.Id);
        
        // Position units on the map
        var attackerPosition = new HexPosition(new HexCoordinates(5, 5), HexDirection.Top);
        var targetPosition = new HexPosition(new HexCoordinates(5, 4), HexDirection.Bottom);
        
        attacker.Deploy(attackerPosition);
        attacker.AssignPilot(_pilot);
        attacker.Parts[PartLocation.LeftTorso].TryAddComponent(new MediumLaser(),[1]).ShouldBeTrue();
        target.Deploy(targetPosition);
        
        // Set up the state
        _sut.HandleHexSelection(_game.BattleMap!.GetHexes().First(h => h.Coordinates == attackerPosition.Coordinates));
        _sut.HandleUnitSelection(attacker);
        var selectTargetAction = _sut.GetAvailableActions().First(a => a.Label == "Select Target");
        selectTargetAction.OnExecute();
        
        // Select target
        _sut.HandleHexSelection(_game.BattleMap.GetHexes().First(h => h.Coordinates == targetPosition.Coordinates));
        _sut.HandleUnitSelection(target);
        
        // Select a weapon
        var weapon = attacker.Parts.Values.SelectMany(p => p.GetComponents<Weapon>()).First();
        var weaponSelection = _sut.WeaponSelectionItems.First(ws => ws.Weapon == weapon);
        weaponSelection.IsSelected = true;
        
        // Act
        var actions = _sut.GetAvailableActions().ToList();
        
        // Assert
        actions.ShouldContain(a => a.Label == "Declare Attack");
    }
    
    [Fact]
    public void GetAvailableActions_DoesNotIncludeConfirmWeaponSelectionsAction_WhenNoWeaponsSelected()
    {
        // Arrange
        var attackingPlayer = _game.Players[0];
        var targetPlayer = _game.Players[1];
        var attacker = _battleMapViewModel.Units.First(u => u.Owner!.Id == attackingPlayer.Id);
        var target = _battleMapViewModel.Units.First(u => u.Owner!.Id == targetPlayer.Id);
        
        // Position units on the map
        var attackerPosition = new HexPosition(new HexCoordinates(5, 5), HexDirection.Top);
        var targetPosition = new HexPosition(new HexCoordinates(5, 4), HexDirection.Bottom);
        
        attacker.Deploy(attackerPosition);
        attacker.AssignPilot(_pilot);
        target.Deploy(targetPosition);
        
        // Set up the state
        _sut.HandleHexSelection(_game.BattleMap!.GetHexes().First(h => h.Coordinates == attackerPosition.Coordinates));
        _sut.HandleUnitSelection(attacker);
        var selectTargetAction = _sut.GetAvailableActions().First(a => a.Label == "Select Target");
        selectTargetAction.OnExecute();
        
        // Select target but don't select any weapons
        _sut.HandleHexSelection(_game.BattleMap.GetHexes().First(h => h.Coordinates == targetPosition.Coordinates));
        _sut.HandleUnitSelection(target);
        
        // Act
        var actions = _sut.GetAvailableActions().ToList();
        
        // Assert
        actions.ShouldNotContain(a => a.Label == "Declare Attack");
    }

    [Fact]
    public void GetAvailableActions_InActionSelection_IncludesSkipAttackOption()
    {
        // Arrange
        _sut.HandleUnitSelection(_unit1);

        // Act
        var actions = _sut.GetAvailableActions().ToList();

        // Assert
        actions.Count.ShouldBe(3); // Turn Torso, Select Target, Skip Attack
        actions[0].Label.ShouldBe("Skip Attack");
        actions[1].Label.ShouldBe("Turn Torso");
        actions[2].Label.ShouldBe("Select Target");
    }

    [Fact]
    public void ConfirmWeaponSelections_PublishesEmptyWeaponAttackDeclarationCommand_WhenSkippingAttack()
    {
        // Arrange
        var attackingPlayer = _game.Players[0];
        var attacker = _battleMapViewModel.Units.First(u => u.Owner!.Id == attackingPlayer.Id);
        
        // Deploy unit
        var attackerPosition = new HexPosition(new HexCoordinates(5, 5), HexDirection.Top);
        attacker.Deploy(attackerPosition);
        
        // Set active player
        _game.HandleCommand(new ChangeActivePlayerCommand
        {
            GameOriginId = Guid.NewGuid(),
            PlayerId = attackingPlayer.Id,
            UnitsToPlay = 1
        });
        
        // Select attacker
        _sut.HandleHexSelection(_game.BattleMap!.GetHexes().First(h => h.Coordinates == attackerPosition.Coordinates));
        _sut.HandleUnitSelection(attacker);
        
        // Get the Skip Attack action
        var skipAttackAction = _sut.GetAvailableActions().First(a => a.Label == "Skip Attack");
        
        // Act
        skipAttackAction.OnExecute();
        
        // Assert
        _commandPublisher.Received(1).PublishCommand(Arg.Is<WeaponAttackDeclarationCommand>(cmd => 
            cmd.PlayerId == attackingPlayer.Id &&
            cmd.AttackerId == attacker.Id &&
            cmd.WeaponTargets.Count == 0
        ));
    }

    [Fact]
    public void PlayerActionLabel_ReturnsSkipAttack_WhenInActionSelectionStep()
    {
        // Arrange 
        var attacker = _battleMapViewModel.Units.First(u => u.Owner!.Id == _player.Id);
        var attackerPosition = new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom);
        attacker.Deploy(attackerPosition);
        _sut.HandleHexSelection(_game.BattleMap!.GetHexes().First(h=>h.Coordinates==attackerPosition.Coordinates));
        _sut.HandleUnitSelection(attacker);
        
        // Act
        var result = _sut.PlayerActionLabel;
        
        // Assert
        result.ShouldBe("Skip Attack");
    }
    
    [Fact]
    public void PlayerActionLabel_ReturnsDeclareAttack_WhenInTargetSelectionStepWithWeaponTargets()
    {
        // Arrange
        var attacker = _battleMapViewModel.Units.First(u => u.Owner!.Id == _player.Id);
        var target = _battleMapViewModel.Units.First(u => u.Owner!.Id != _player.Id);
        
        // Place units next to each other
        var attackerPosition = new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom);
        attacker.Deploy(attackerPosition);
        attacker.Parts[PartLocation.LeftTorso].TryAddComponent(new MediumLaser(),[1]).ShouldBeTrue();
        attacker.AssignPilot(_pilot);
        var targetPosition = new HexPosition(new HexCoordinates(1, 2), HexDirection.Bottom);
        target.Deploy(targetPosition);
        
        _sut.HandleHexSelection(_game.BattleMap!.GetHexes().First(h=>h.Coordinates==attackerPosition.Coordinates));
        _sut.HandleUnitSelection(attacker);
        var selectTargetAction = _sut.GetAvailableActions().First(a => a.Label == "Select Target");
        selectTargetAction.OnExecute();
        _sut.HandleHexSelection(_game.BattleMap.GetHexes().First(h=>h.Coordinates==targetPosition.Coordinates));
        _sut.HandleUnitSelection(target);

        var weapon = attacker.Parts.Values
            .SelectMany(p => p.GetComponents<Weapon>())
            .First();
        var weaponSelection = _sut.WeaponSelectionItems.First(ws => ws.Weapon == weapon);
        weaponSelection.IsSelected = true;
        
        // Act
        var result = _sut.PlayerActionLabel;
        
        // Assert
        result.ShouldBe("Declare Attack");
    }
    
    [Fact]
    public void PlayerActionLabel_ReturnsSkipAttack_WhenInTargetSelectionStepWithoutWeaponTargets()
    {
        // Arrange
        var attacker = _battleMapViewModel.Units.First(u => u.Owner!.Id == _player.Id);
        var target = _battleMapViewModel.Units.First(u => u.Owner!.Id != _player.Id);
        
        // Place units next to each other
        var attackerPosition = new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom);
        attacker.Deploy(attackerPosition);
        attacker.AssignPilot(_pilot);
        var targetPosition = new HexPosition(new HexCoordinates(1, 2), HexDirection.Bottom);
        target.Deploy(targetPosition);
        
        _sut.HandleHexSelection(_game.BattleMap!.GetHexes().First(h=>h.Coordinates==attackerPosition.Coordinates));
        _sut.HandleUnitSelection(attacker);
        var selectTargetAction = _sut.GetAvailableActions().First(a => a.Label == "Select Target");
        selectTargetAction.OnExecute();
        _sut.HandleHexSelection(_game.BattleMap.GetHexes().First(h=>h.Coordinates==targetPosition.Coordinates));
        _sut.HandleUnitSelection(target);
        
        // Act
        var result = _sut.PlayerActionLabel;
        
        // Assert
        result.ShouldBe("Skip Attack");
    }
    
    [Fact]
    public void PlayerActionLabel_ReturnsEmptyString_WhenInWeaponsConfigurationStep()
    {
        // Arrange
        var attacker = _battleMapViewModel.Units.First(u => u.Owner!.Id == _player.Id);
        
        // Place unit
        var attackerPosition = new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom);
        attacker.Deploy(attackerPosition);
        attacker.AssignPilot(_pilot);
        
        _sut.HandleHexSelection(_game.BattleMap!.GetHexes().First(h=>h.Coordinates==attackerPosition.Coordinates));
        _sut.HandleUnitSelection(attacker);
        var torsoRotationAction = _sut.GetAvailableActions().First(a => a.Label == "Turn Torso");
        torsoRotationAction.OnExecute();
        
        // Act
        var result = _sut.PlayerActionLabel;
        
        // Assert
        result.ShouldBe(string.Empty);
    }

    [Fact]
    public void UpdateWeaponViewModels_SkipsUnavailableWeapons_ForToHitCalculation()
    {
        // Arrange
        var attacker = _battleMapViewModel.Units.First(u => u.Owner!.Id == _player.Id);
        attacker.Parts[PartLocation.LeftTorso].TryAddComponent(new MediumLaser(),[1]).ShouldBeTrue();
        attacker.Parts[PartLocation.RightTorso].TryAddComponent(new MediumLaser(),[1]).ShouldBeTrue();
        var target = _battleMapViewModel.Units.First(u => u.Owner!.Id != _player.Id);

        // Position units on the map
        var attackerPosition = new HexPosition(new HexCoordinates(5, 5), HexDirection.Top);
        var targetPosition = new HexPosition(new HexCoordinates(5, 4), HexDirection.Bottom);
        attacker.Deploy(attackerPosition);
        attacker.AssignPilot(_pilot);
        target.Deploy(targetPosition);

        // Set up the state (mirroring the reference test)
        _sut.HandleHexSelection(_game.BattleMap!.GetHexes().First(h => h.Coordinates == attackerPosition.Coordinates));
        _sut.HandleUnitSelection(attacker);
        var selectTargetAction = _sut.GetAvailableActions().First(a => a.Label == "Select Target");
        selectTargetAction.OnExecute();

        // Get two different weapons
        var weapons = attacker.Parts.Values.SelectMany(p => p.GetComponents<Weapon>()).Take(2).ToList();
        // Make one weapon unavailable
        weapons[1].Hit(); 

        // Select target and both weapons
        _sut.HandleHexSelection(_game.BattleMap.GetHexes().First(h => h.Coordinates == targetPosition.Coordinates));
        _sut.HandleUnitSelection(target);
        var weaponSelection1 = _sut.WeaponSelectionItems.First(ws => ws.Weapon == weapons[0]);
        var weaponSelection2 = _sut.WeaponSelectionItems.First(ws => ws.Weapon == weapons[1]);
        weaponSelection1.IsSelected = true;
        weaponSelection2.IsSelected = true;

        // Assert
        weaponSelection1.Weapon.IsAvailable.ShouldBeTrue();
        weaponSelection1.ModifiersBreakdown.ShouldNotBeNull();

        weaponSelection2.Weapon.IsAvailable.ShouldBeFalse();
        weaponSelection2.ModifiersBreakdown.ShouldBeNull();
    }

    [Fact]
    public void ClearWeaponRangeHighlights_DoesNotCrash_WhenAttackerHasNoWeapons()
    {
        // Arrange
        SetPhase(PhaseNames.WeaponsAttack);
        SetActivePlayer();
        
        // Create a unit with no weapons
        var unitWithNoWeapons = _battleMapViewModel.Units.First(u => u.Owner!.Id == _player.Id);
        var weapons = unitWithNoWeapons.Parts.Values.SelectMany(p => p.GetComponents<Weapon>()).ToList();
        foreach (var weapon in weapons)
        {
            weapon.FirstMountPart?.RemoveComponent(weapon);
        }
        
        var position = new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom);
        unitWithNoWeapons.Deploy(position);

        // Set the unit as the attacker in the state
        _sut.HandleUnitSelection(unitWithNoWeapons);
        _sut.HandleHexSelection(_game.BattleMap!.GetHexes().First(h=>h.Coordinates==position.Coordinates));
    }

    [Fact]
    public void HandleHexSelection_CancelsTorsoRotation_WhenInWeaponsConfigurationStep()
    {
        // Arrange
        SetPhase(PhaseNames.WeaponsAttack);
        SetActivePlayer();
        var position = new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom);
        var attacker = _battleMapViewModel.Units.First(u => u.Owner!.Id == _player.Id);
        attacker.Deploy(position);
        attacker.AssignPilot(_pilot);

        // Select unit and start torso rotation
        _sut.HandleUnitSelection(attacker);
        var actions = _sut.GetAvailableActions().ToList();
        var torsoAction = actions.First(a => a.Label == "Turn Torso");
        torsoAction.OnExecute();

        // Verify we're in WeaponsConfiguration step
        _sut.CurrentStep.ShouldBe(WeaponsAttackStep.WeaponsConfiguration);
        _battleMapViewModel.IsDirectionSelectorVisible.ShouldBeTrue();

        // Act - click on a hex (outside direction selector)
        var hex = new Hex(new HexCoordinates(5, 5));
        _sut.HandleHexSelection(hex);

        // Assert
        _sut.CurrentStep.ShouldBe(WeaponsAttackStep.ActionSelection);
        _battleMapViewModel.IsDirectionSelectorVisible.ShouldBeFalse();
        _sut.Attacker.ShouldBe(attacker); // Attacker should still be selected
    }

    [Fact]
    public void HandleHexSelection_CancelsTargetSelection_WhenClickingOutsideWeaponRange()
    {
        // Arrange
        SetPhase(PhaseNames.WeaponsAttack);
        SetActivePlayer();

        var attackerPosition = new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom);
        var attacker = _battleMapViewModel.Units.First(u => u.Owner!.Id == _player.Id);
        attacker.Deploy(attackerPosition);
        attacker.Parts[PartLocation.LeftTorso].TryAddComponent(new MediumLaser(),[1]).ShouldBeTrue();
        attacker.AssignPilot(_pilot);

        var targetPosition = new HexPosition(new HexCoordinates(2, 1), HexDirection.Bottom);
        var target = _battleMapViewModel.Units.First(u => u.Owner!.Id != _player.Id);
        target.Deploy(targetPosition);

        // Select attacker and enter target selection
        _sut.HandleUnitSelection(attacker);
        var selectTargetAction = _sut.GetAvailableActions().First(a => a.Label == "Select Target");
        selectTargetAction.OnExecute();

        // Select a target
        _sut.HandleHexSelection(new Hex(targetPosition.Coordinates));
        _sut.HandleUnitSelection(target);

        // Verify we're in target selection with a target
        _sut.CurrentStep.ShouldBe(WeaponsAttackStep.TargetSelection);
        _sut.SelectedTarget.ShouldBe(target);

        // Act - click on a hex outside weapon range
        var farAwayHex = new Hex(new HexCoordinates(10, 10));
        _sut.HandleHexSelection(farAwayHex);

        // Assert
        _sut.CurrentStep.ShouldBe(WeaponsAttackStep.ActionSelection);
        _sut.SelectedTarget.ShouldBeNull();
        _sut.Attacker.ShouldBe(attacker); // Attacker should still be selected
        attacker.WeaponAttackState.WeaponTargets.ShouldBeEmpty();
        _battleMapViewModel.IsWeaponSelectionVisible.ShouldBeFalse();
    }

    [Fact]
    public void HandleHexSelection_DoesNotCancelTargetSelection_WhenClickingOnValidTarget()
    {
        // Arrange
        SetPhase(PhaseNames.WeaponsAttack);
        SetActivePlayer();

        var attackerPosition = new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom);
        var attacker = _battleMapViewModel.Units.First(u => u.Owner!.Id == _player.Id);
        attacker.Deploy(attackerPosition);
        attacker.Parts[PartLocation.LeftTorso].TryAddComponent(new MediumLaser(),[1]).ShouldBeTrue();
        attacker.AssignPilot(_pilot);

        var target1Position = new HexPosition(new HexCoordinates(2, 1), HexDirection.Bottom);
        var target1 = _battleMapViewModel.Units.First(u => u.Owner!.Id != _player.Id);
        target1.Deploy(target1Position);

        var target2Position = new HexPosition(new HexCoordinates(1, 2), HexDirection.Bottom);
        var target2 = _battleMapViewModel.Units.Last(u => u.Owner!.Id != _player.Id);
        target2.Deploy(target2Position);

        // Select attacker and enter target selection
        _sut.HandleUnitSelection(attacker);
        var selectTargetAction = _sut.GetAvailableActions().First(a => a.Label == "Select Target");
        selectTargetAction.OnExecute();

        // Select first target
        _sut.HandleHexSelection(new Hex(target1Position.Coordinates));
        _sut.HandleUnitSelection(target1);

        // Act - click on a different valid target
        _sut.HandleHexSelection(new Hex(target2Position.Coordinates));
        _sut.HandleUnitSelection(target2);

        // Assert - should switch to the new target, not cancel
        _sut.CurrentStep.ShouldBe(WeaponsAttackStep.TargetSelection);
        _sut.SelectedTarget.ShouldBe(target2);
        _sut.Attacker.ShouldBe(attacker);
    }
}
