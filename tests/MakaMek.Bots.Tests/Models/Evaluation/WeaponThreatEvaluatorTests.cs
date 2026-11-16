using NSubstitute;
using Sanet.MakaMek.Bots.Models.Evaluation;
using Sanet.MakaMek.Core.Data.Units.Components;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;
using Sanet.MakaMek.Map.Models;
using Shouldly;

namespace Sanet.MakaMek.Bots.Tests.Models.Evaluation;

public class WeaponThreatEvaluatorTests
{
    private static readonly HexPosition EnemyPosition =
        new(new HexCoordinates(1, 4), HexDirection.Top);
    private static readonly HexPosition DefenderPosition =
        new(new HexCoordinates(1, 1), HexDirection.Bottom);

    private readonly ILineOfSightEvaluator _lineOfSight = Substitute.For<ILineOfSightEvaluator>();
    private readonly IHitProbabilityCalculator _hitProbability = Substitute.For<IHitProbabilityCalculator>();
    private readonly WeaponThreatEvaluator _sut;

    public WeaponThreatEvaluatorTests()
    {
        _sut = new WeaponThreatEvaluator(_lineOfSight, _hitProbability);
    }

    [Fact]
    public void GetThreats_WhenTheEnemyHasNoMovementPath_ShouldEvaluateFromItsCurrentPosition()
    {
        // Arrange - an enemy that has not moved has no MovementTaken; evaluation still needs a
        // path (TMM 0) instead of throwing on a null movement path.
        var weapon = CreateWeapon();
        var enemy = new UnitSnapshot
        {
            Id = Guid.NewGuid(),
            Position = EnemyPosition,
            MovementPath = null,
            Height = 2,
            IsProne = false,
            Gunnery = 4,
            Weapons = [weapon],
            Configurations = [],
            ConfigurationTypesByPart = new Dictionary<PartLocation, HashSet<WeaponConfigurationType>>()
        };
        var defenderPath = MovementPath.CreateSingleSegmentPath(DefenderPosition);

        _lineOfSight.HasLineOfSight(EnemyPosition, DefenderPosition, 2, 2).Returns(true);
        _hitProbability.Calculate(
                Arg.Any<UnitSnapshot>(),
                Arg.Any<MovementPath>(),
                Arg.Any<HexPosition>(),
                Arg.Any<MovementPath>(),
                Arg.Any<HexPosition>(),
                Arg.Any<WeaponSnapshot>(),
                Arg.Any<HexDirection?>(),
                Arg.Any<int>(),
                Arg.Any<bool>())
            .Returns(0.5);

        // Act
        var threats = _sut.GetThreats(enemy, FiringArc.Front, defenderPath, 2, false).ToList();

        // Assert - probability * damage * front-arc multiplier
        threats.ShouldHaveSingleItem().ShouldBe(2.5);
        _hitProbability.Received(1).Calculate(
            enemy,
            Arg.Is<MovementPath>(path =>
                path.HexesTraveled == 0 && path.Destination == EnemyPosition),
            EnemyPosition,
            defenderPath,
            DefenderPosition,
            weapon,
            null,
            2,
            false);
    }

    private static WeaponSnapshot CreateWeapon() =>
        new()
        {
            Weapon = new TestWeapon(new WeaponDefinition(
                "TestLaser", 5, 1, new WeaponRange(0, 3, 6, 9), WeaponType.Energy, 100)),
            Damage = 5,
            LongRange = 9,
            MountLocation = PartLocation.RightArm,
            MountFacing = HexDirection.Top,
            FiringArcs = [FiringArc.Front],
            AttackModifiers = []
        };

    private sealed class TestWeapon(WeaponDefinition definition) : Weapon(definition);
}
