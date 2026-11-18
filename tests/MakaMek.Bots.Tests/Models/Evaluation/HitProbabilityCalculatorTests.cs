using NSubstitute;
using Sanet.MakaMek.Bots.Models.Evaluation;
using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Data.Units.Components;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;
using Sanet.MakaMek.Core.Utils;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Models.Terrains;
using Shouldly;

namespace Sanet.MakaMek.Bots.Tests.Models.Evaluation;

public class HitProbabilityCalculatorTests
{
    private static readonly HexPosition AttackerPosition =
        new(new HexCoordinates(1, 1), HexDirection.Bottom);
    private static readonly HexPosition TargetPosition =
        new(new HexCoordinates(1, 4), HexDirection.Top);

    private readonly IClientGame _game = Substitute.For<IClientGame>();
    private readonly IBattleMap _battleMap = Substitute.For<IBattleMap>();
    private readonly IToHitCalculator _toHitCalculator = Substitute.For<IToHitCalculator>();
    private readonly HitProbabilityCalculator _sut;

    public HitProbabilityCalculatorTests()
    {
        _game.BattleMap.Returns(_battleMap);
        _game.ToHitCalculator.Returns(_toHitCalculator);
        _sut = new HitProbabilityCalculator(_game);
    }

    [Fact]
    public void Calculate_ShouldBuildTheScenarioFromTheEvaluatedPositions()
    {
        // Arrange - depth is read at both evaluated positions, not at the units' current hexes
        var weapon = CreateWeapon();
        var attacker = CreateAttackerSnapshot(weapon);
        var attackerPath = MovementPath.CreateSingleSegmentPath(AttackerPosition, MovementType.Walk);
        var targetPath = MovementPath.CreateSingleSegmentPath(TargetPosition, MovementType.Walk);

        _battleMap.GetHex(AttackerPosition.Coordinates)
            .Returns(new Hex(AttackerPosition.Coordinates).WithTerrain(new WaterTerrain(-1)));
        _battleMap.GetHex(TargetPosition.Coordinates)
            .Returns(new Hex(TargetPosition.Coordinates).WithTerrain(new WaterTerrain(0)));

        AttackScenario? capturedScenario = null;
        _toHitCalculator
            .GetToHitNumber(
                Arg.Do<AttackScenario>(scenario => capturedScenario = scenario),
                weapon.Weapon,
                _battleMap)
            .Returns(8);

        // Act
        var probability = _sut.Calculate(
            attacker,
            attackerPath,
            AttackerPosition,
            targetPath,
            TargetPosition,
            weapon,
            HexDirection.Bottom,
            targetHeight: 2,
            isTargetProne: false);

        // Assert
        probability.ShouldBe(DiceUtils.Calculate2d6Probability(8));
        capturedScenario.ShouldNotBeNull();
        capturedScenario.AttackerGunnery.ShouldBe(4);
        capturedScenario.AttackerPosition.ShouldBe(AttackerPosition);
        capturedScenario.TargetPosition.ShouldBe(TargetPosition);
        capturedScenario.AttackerMovementType.ShouldBe(MovementType.Walk);
        capturedScenario.AttackerFacing.ShouldBe(HexDirection.Bottom);
        capturedScenario.AttackerHeight.ShouldBe(2);
        capturedScenario.TargetHeight.ShouldBe(2);
        capturedScenario.TargetHexesMoved.ShouldBe(0);
        capturedScenario.AttackerWaterDepth.ShouldBe(1);
        capturedScenario.TargetWaterDepth.ShouldBe(0);
    }

    private static UnitSnapshot CreateAttackerSnapshot(WeaponSnapshot weapon) =>
        new()
        {
            Id = Guid.NewGuid(),
            Position = AttackerPosition,
            MovementPath = MovementPath.CreateSingleSegmentPath(AttackerPosition, MovementType.Walk),
            Height = 2,
            IsProne = false,
            Gunnery = 4,
            Weapons = [weapon],
            Configurations = [],
            ConfigurationTypesByPart = new Dictionary<PartLocation, HashSet<WeaponConfigurationType>>()
        };

    private static WeaponSnapshot CreateWeapon() =>
        new()
        {
            Weapon = new TestWeapon(new WeaponDefinition(
                "TestLaser", 5, 1, new WeaponRange(0, 3, 6, 9), WeaponType.Energy, 100)),
            Damage = 5,
            LongRange = 9,
            MountLocation = PartLocation.RightArm,
            MountFacing = HexDirection.Bottom,
            FiringArcs = [FiringArc.Front],
            AttackModifiers = []
        };

    private sealed class TestWeapon(WeaponDefinition definition) : Weapon(definition);
}
