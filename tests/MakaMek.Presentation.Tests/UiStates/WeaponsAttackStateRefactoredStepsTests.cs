using NSubstitute;
using Sanet.MakaMek.Assets.Services;
using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Data.Units.Components;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Mechs.Falling;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Attack;
using Sanet.MakaMek.Core.Models.Game.Mechanics.PhysicalAttack;
using Sanet.MakaMek.Core.Models.Game.Phases;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components.Internal;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons.Energy;
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
/// Regression tests for the per-sub-step selection handlers and the weapon-row projection that the
/// cyclomatic-complexity refactor split out of <c>WeaponsAttackState.SelectedUnit</c>,
/// <c>HandleUnitSelectionFromHex</c> and <c>UpdateWeaponViewModels</c>.
/// </summary>
public class WeaponsAttackStateRefactoredStepsTests
{
    private static readonly IBattleMapFactory BattleMapFactory = new BattleMapFactory();

    private readonly WeaponsAttackState _sut;
    private readonly IClientGame _game;
    private readonly Mech _attacker;
    private readonly Mech _ally;
    private readonly Mech _enemy;
    private readonly Player _player;
    private readonly BattleMapViewModel _battleMapViewModel;
    private readonly IRulesProvider _rulesProvider = new TotalWarfareRulesProvider();
    private readonly IComponentProvider _componentProvider = new ClassicBattletechComponentProvider();
    private readonly ILocalizationService _localizationService = Substitute.For<ILocalizationService>();
    private readonly IToHitCalculator _toHitCalculator = Substitute.For<IToHitCalculator>();

    private readonly HexPosition _attackerPosition = new(new HexCoordinates(1, 1), HexDirection.Bottom);
    private readonly HexPosition _allyPosition = new(new HexCoordinates(3, 3), HexDirection.Bottom);
    private readonly HexPosition _enemyPosition = new(new HexCoordinates(1, 3), HexDirection.Bottom);

    public WeaponsAttackStateRefactoredStepsTests()
    {
        _localizationService.GetString(Arg.Any<string>()).Returns(call => call.Arg<string>());

        _battleMapViewModel = new BattleMapViewModel(
            Substitute.For<IImageService>(),
            Substitute.For<ITerrainAssetService>(),
            _localizationService,
            Substitute.For<IDispatcherService>(),
            _rulesProvider,
            Substitute.For<IPlatformService>());

        var mechFactory = new MechFactory(_rulesProvider, _componentProvider, _localizationService);
        _attacker = mechFactory.Create(CreateMechData());
        _ally = mechFactory.Create(CreateMechData());
        _enemy = mechFactory.Create(CreateMechData());

        _attacker.AssignPilot(CreateConsciousPilot());
        _ally.AssignPilot(CreateConsciousPilot());
        _enemy.AssignPilot(CreateConsciousPilot());

        _attacker.Parts[PartLocation.LeftTorso].TryAddComponent(new MediumLaser(), [1]).ShouldBeTrue();
        _attacker.Parts[PartLocation.RightTorso].TryAddComponent(new MediumLaser(), [1]).ShouldBeTrue();

        _toHitCalculator.GetModifierBreakdown(
                Arg.Any<IUnit>(),
                Arg.Any<IUnit>(),
                Arg.Any<Weapon>(),
                Arg.Any<IBattleMap>(),
                Arg.Any<bool>(),
                Arg.Any<PartLocation?>())
            .Returns(new ToHitBreakdown
            {
                GunneryBase = new GunneryRollModifier { Value = 4 },
                AttackerMovement = new AttackerMovementModifier { Value = 0, MovementType = MovementType.StandingStill },
                TargetMovement = new TargetMovementModifier { Value = 0, HexesMoved = 1 },
                OtherModifiers = [],
                RangeModifier = new RangeRollModifier { Value = 0, Range = RangeBracket.Medium, Distance = 4, WeaponName = "Test" },
                TerrainModifiers = [],
                HasLineOfSight = true,
                FiringArc = FiringArc.Front
            });

        var playerId = Guid.NewGuid();
        _player = new Player(playerId, "Player1", PlayerControlType.Human);
        _player.AddUnit(_attacker);
        _player.AddUnit(_ally);
        var enemyPlayer = new Player(Guid.NewGuid(), "Player2", PlayerControlType.Human);
        enemyPlayer.AddUnit(_enemy);

        _game = Substitute.For<IClientGame>();
        _game.Id.Returns(Guid.NewGuid());
        _game.BattleMap.Returns(BattleMapFactory.GenerateMap(
            11, 11, new SingleTerrainGenerator(11, 11, new ClearTerrain())));
        _game.Players.Returns([_player, enemyPlayer]);
        _game.AlivePlayers.Returns([_player, enemyPlayer]);
        _game.RulesProvider.Returns(_rulesProvider);
        _game.ToHitCalculator.Returns(_toHitCalculator);
        _game.TurnPhase.Returns(PhaseNames.WeaponsAttack);
        _game.PhaseStepState.Returns(new PhaseStepState(PhaseNames.WeaponsAttack, _player, 1));
        _game.CanActivePlayerAct.Returns(true);
        _game.LocalPlayers.Returns([playerId]);

        _battleMapViewModel.Game = _game;
        _sut = new WeaponsAttackState(_battleMapViewModel);
        BindViewModelCurrentStateTo(_sut, _battleMapViewModel);
    }

    [Fact]
    public void HandleUnitSelectionFromList_WhenSelectingUnit_MakesTheUnitTheAttacker()
    {
        // Arrange
        DeployUnits();

        // Act
        _sut.HandleUnitSelectionFromList(_attacker);

        // Assert
        _sut.Attacker.ShouldBe(_attacker);
        _sut.SelectedUnit.ShouldBe(_attacker);
        _sut.CurrentStep.ShouldBe(WeaponsAttackStep.ActionSelection);
        _sut.WeaponSelectionItems.ShouldNotBeEmpty();
    }

    [Fact]
    public void HandleUnitSelectionFromList_WhenActionSelection_ReplacesTheAttackerWithTheOtherFriendlyUnit()
    {
        // Arrange
        DeployUnits();
        _sut.HandleUnitSelectionFromList(_attacker);

        // Act
        _sut.HandleUnitSelectionFromList(_ally);

        // Assert
        _sut.Attacker.ShouldBe(_ally);
        _sut.CurrentStep.ShouldBe(WeaponsAttackStep.ActionSelection);
    }

    [Fact]
    public void HandleUnitSelectionFromList_WhenWeaponsConfiguration_KeepsTheCurrentAttacker()
    {
        // Arrange - torso rotation owns the selection until it is confirmed or cancelled
        DeployUnits();
        _sut.HandleUnitSelectionFromList(_attacker);
        _sut.GetAvailableActions().First(a => a.Label == "Action_TurnTorso").OnExecute();
        _sut.CurrentStep.ShouldBe(WeaponsAttackStep.WeaponsConfiguration);

        // Act
        _sut.HandleUnitSelectionFromList(_ally);

        // Assert
        _sut.Attacker.ShouldBe(_attacker);
        _sut.CurrentStep.ShouldBe(WeaponsAttackStep.WeaponsConfiguration);
    }

    [Fact]
    public void HandleUnitSelectionFromList_WhenTargetSelection_SelectsAnEnemyInsideTheWeaponRanges()
    {
        // Arrange
        DeployUnits();
        EnterTargetSelection();

        // Act
        _sut.HandleUnitSelectionFromList(_enemy);

        // Assert
        _sut.SelectedTarget.ShouldBe(_enemy);
        _sut.SelectedUnit.ShouldBe(_enemy);
        _sut.CurrentStep.ShouldBe(WeaponsAttackStep.TargetSelection);
    }

    [Fact]
    public void HandleUnitSelectionFromList_WhenTargetSelection_IgnoresAnEnemyOutsideTheWeaponRanges()
    {
        // Arrange - the enemy is deployed far outside every weapon's reach
        _attacker.Deploy(_attackerPosition, null);
        _ally.Deploy(_allyPosition, null);
        _enemy.Deploy(new HexPosition(new HexCoordinates(9, 9), HexDirection.Bottom), null);
        EnterTargetSelection();

        // Act
        _sut.HandleUnitSelectionFromList(_enemy);

        // Assert
        _sut.SelectedTarget.ShouldBeNull();
    }

    [Fact]
    public void HandleUnitSelectionFromList_WhenTargetSelection_IgnoresAFriendlyUnit()
    {
        // Arrange
        DeployUnits();
        EnterTargetSelection();

        // Act
        _sut.HandleUnitSelectionFromList(_ally);

        // Assert
        _sut.SelectedTarget.ShouldBeNull();
        _sut.Attacker.ShouldBe(_attacker);
    }

    [Fact]
    public void HandleHexSelection_WhenSelectingUnit_SelectsTheFriendlyUnitUnderTheCursor()
    {
        // Arrange
        DeployUnits();

        // Act
        _sut.HandleHexSelection(new Hex(_attackerPosition.Coordinates));

        // Assert
        _sut.Attacker.ShouldBe(_attacker);
    }

    [Fact]
    public void HandleHexSelection_WhenTargetSelection_SelectsTheEnemyUnderTheCursor()
    {
        // Arrange
        DeployUnits();
        EnterTargetSelection();

        // Act
        _sut.HandleHexSelection(new Hex(_enemyPosition.Coordinates));

        // Assert
        _sut.SelectedTarget.ShouldBe(_enemy);
    }

    [Fact]
    public void HandleHexSelection_WhenTheHexHoldsNoUnit_LeavesTheSelectionAlone()
    {
        // Arrange
        DeployUnits();

        // Act
        _sut.HandleHexSelection(new Hex(new HexCoordinates(6, 6)));

        // Assert
        _sut.SelectedUnit.ShouldBeNull();
        _sut.CurrentStep.ShouldBe(WeaponsAttackStep.SelectingUnit);
    }

    [Fact]
    public void HandleUnitSelectionFromList_WhenTargetSelected_ProjectsRangeAndBreakdownOntoEveryWeaponRow()
    {
        // Arrange
        DeployUnits();
        EnterTargetSelection();
        var weaponsInRange = _sut.GetWeaponsInRange(_enemyPosition.Coordinates);

        // Act
        _sut.HandleUnitSelectionFromList(_enemy);

        // Assert - each row reflects the same per-weapon range and modifier projection
        foreach (var row in _sut.WeaponSelectionItems)
        {
            row.IsInRange.ShouldBe(weaponsInRange.Contains(row.Weapon));
            if (row.IsInRange && row.Weapon.IsAvailable)
            {
                row.ModifiersBreakdown.ShouldNotBeNull();
                row.IsEnabled.ShouldBeTrue();
                continue;
            }

            row.ModifiersBreakdown.ShouldBeNull();
        }
    }

    [Fact]
    public void HandleWeaponRowSelection_WhenTheRowIsEnabled_AssignsTheSelectedTargetToThatWeapon()
    {
        // Arrange
        DeployUnits();
        EnterTargetSelection();
        _sut.HandleUnitSelectionFromList(_enemy);
        var row = _sut.WeaponSelectionItems.First(vm => vm.IsInRange && vm.IsEnabled);

        // Act
        row.IsSelected = true;

        // Assert
        row.IsSelected.ShouldBeTrue();
        row.Target.ShouldBe(_enemy);
        _sut.WeaponSelectionItems
            .Where(vm => vm.Weapon != row.Weapon)
            .ShouldAllBe(vm => vm.Target == null);
    }

    [Fact]
    public void HandleUnitSelectionFromList_WhenSelectingAttacker_GroupsTheReachableHighlightPerWeaponName()
    {
        // Arrange - two weapons with the same name cover the same hexes
        DeployUnits();

        // Act
        _sut.HandleUnitSelectionFromList(_attacker);

        // Assert - the highlight lists each distinct weapon name once, while the range query keeps both
        var enemyHex = _game.BattleMap!.GetHex(_enemyPosition.Coordinates)!;
        var highlight = enemyHex.Highlights.OfType<AttackReachableHighlight>().ShouldHaveSingleItem();
        _sut.GetWeaponsInRange(_enemyPosition.Coordinates).Count.ShouldBe(2);
        highlight.WeaponNames.ShouldBe(["Medium Laser"]);
    }

    private void DeployUnits()
    {
        _attacker.Deploy(_attackerPosition, null);
        _ally.Deploy(_allyPosition, null);
        _enemy.Deploy(_enemyPosition, null);
    }

    private void EnterTargetSelection()
    {
        _sut.HandleUnitSelectionFromList(_attacker);
        _sut.GetAvailableActions().First(a => a.Label == "Action_SelectTarget").OnExecute();
        _sut.CurrentStep.ShouldBe(WeaponsAttackStep.TargetSelection);
    }

    private static UnitData CreateMechData()
    {
        var data = MechFactoryTests.CreateDummyMechData();
        data.Id = Guid.NewGuid();
        return data;
    }

    private static IPilot CreateConsciousPilot()
    {
        var pilot = Substitute.For<IPilot>();
        pilot.IsConscious.Returns(true);
        return pilot;
    }

    private static void BindViewModelCurrentStateTo(WeaponsAttackState state, BattleMapViewModel viewModel)
    {
        typeof(BattleMapViewModel).GetProperty(nameof(BattleMapViewModel.CurrentState))!
            .GetSetMethod(true)!.Invoke(viewModel, [state]);
    }
}
