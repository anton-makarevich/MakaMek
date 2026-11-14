using NSubstitute;
using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Game.Mechanics.PhysicalAttacks;
using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Models.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Mechanics.PhysicalAttack;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Map.Models;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Mechanics;

public class DfaAttackResolverTests
{
    private readonly IDiceRoller _diceRoller = Substitute.For<IDiceRoller>();
    private readonly Queue<List<DiceResult>> _roll2D6 = new();
    private readonly IPhysicalAttackCalculator _toHitCalculator = Substitute.For<IPhysicalAttackCalculator>();
    private readonly IDamageTransferCalculator _damageTransferCalculator = Substitute.For<IDamageTransferCalculator>();
    private readonly IRulesProvider _rulesProvider = new TotalWarfareRulesProvider();
    private readonly IBattleMap _map = Substitute.For<IBattleMap>();
    private readonly DfaAttackResolver _sut;

    public DfaAttackResolverTests()
    {
        _damageTransferCalculator
            .CalculateStructureDamage(
                Arg.Any<IUnit>(),
                Arg.Any<PartLocation>(),
                Arg.Any<int>(),
                Arg.Any<HitDirection>(),
                Arg.Any<IReadOnlyList<LocationHitData>?>())
            .Returns(callInfo => [new LocationDamageData(
                callInfo.Arg<PartLocation>(), callInfo.Arg<int>(), 0, false)]);

        _sut = new DfaAttackResolver(
            _toHitCalculator,
            _diceRoller,
            _rulesProvider,
            _damageTransferCalculator);
    }

    [Fact]
    public void ResolveDfa_OnAHit_UsesTheJumpOriginForDirectionAndTheDfaDamageTables()
    {
        // Arrange: the target faces Top, so a jump origin two hexes to the south enters the rear.
        _toHitCalculator.GetDfaToHitNumber(Arg.Any<IUnit>(), Arg.Any<IUnit>(), Arg.Any<IBattleMap>())
            .Returns(7);
        SetHitRoll(3, 4);
        _diceRoller.RollD6().Returns(
            new DiceResult(1),
            new DiceResult(2),
            new DiceResult(3),
            new DiceResult(4));
        var target = CreateUnit(20, TargetPosition());
        // The attacker approaches from the front arc while the jump origin stays in the rear arc,
        // so the test proves the direction comes from StartCoordinates, not the attacker's position.
        var attacker = CreateUnit(35, new HexPosition(new HexCoordinates(0, -1), HexDirection.Top));

        // Act
        var result = _sut.ResolveDfa(new DfaAttackContext
        {
            Attacker = attacker,
            Target = target,
            Map = _map,
            StartCoordinates = new HexCoordinates(0, 2),
            ApproachCoordinates = new HexPosition(new HexCoordinates(0, -1), HexDirection.Top)
        });

        // Assert: 35 tons deal ceil(35 × 3 / 10) = 11 in three clusters (5+5+1).
        result.Dfa!.Outcome.ShouldBe(DfaOutcome.Hit);
        result.AttackResolution.IsHit.ShouldBeTrue();
        result.AttackResolution.AttackDirection.ShouldBe(HitDirection.Rear);
        result.Damage.ShouldBe(11);
        result.Dfa.TargetHitLocations!.TotalDamage.ShouldBe(11);
        result.Dfa.TargetHitLocations.HitLocations.Count.ShouldBe(3);
        // The attacker takes ceil(35 / 5) = 7 in two leg clusters (5+2).
        result.Dfa.AttackerLegHitLocations!.TotalDamage.ShouldBe(7);
        result.Dfa.AttackerLegHitLocations.HitLocations.Count.ShouldBe(2);
        result.Dfa.AttackerLegHitLocations.HitLocations
            .ShouldAllBe(hit => hit.LocationRoll.Length == 1);
    }

    [Fact]
    public void ResolveDfa_OnAMiss_ReturnsNoDamageAndNoPsrRequirements()
    {
        _toHitCalculator.GetDfaToHitNumber(Arg.Any<IUnit>(), Arg.Any<IUnit>(), Arg.Any<IBattleMap>())
            .Returns(7);
        SetHitRoll(2, 2);
        var target = CreateUnit(20, TargetPosition());
        var attacker = CreateUnit(35, new HexPosition(new HexCoordinates(0, 2), HexDirection.Top));

        var result = _sut.ResolveDfa(new DfaAttackContext
        {
            Attacker = attacker,
            Target = target,
            Map = _map,
            StartCoordinates = new HexCoordinates(0, 2),
            ApproachCoordinates = new HexPosition(new HexCoordinates(0, 1), HexDirection.Top)
        });

        result.Dfa!.Outcome.ShouldBe(DfaOutcome.Miss);
        result.AttackResolution.IsHit.ShouldBeFalse();
        result.Damage.ShouldBe(0);
        result.PsrRequirements.ShouldBeEmpty();
        result.Dfa.TargetHitLocations.ShouldBeNull();
        result.Dfa.AttackerLegHitLocations.ShouldBeNull();
    }

    [Fact]
    public void ResolveDfa_AgainstAProneTarget_RollsTheRearColumnOfTheStandardTable()
    {
        _toHitCalculator.GetDfaToHitNumber(Arg.Any<IUnit>(), Arg.Any<IUnit>(), Arg.Any<IBattleMap>())
            .Returns(7);
        QueueRoll2D6(3, 4);
        QueueRoll2D6(3, 4);
        QueueRoll2D6(3, 4);
        QueueRoll2D6(3, 4);
        UseQueuedRoll2D6();
        _diceRoller.RollD6().Returns(new DiceResult(1), new DiceResult(2));
        var target = CreateUnit(20, TargetPosition());
        target.Status.Returns(UnitStatus.Prone);
        var attacker = CreateUnit(35, new HexPosition(new HexCoordinates(0, 2), HexDirection.Top));

        var result = _sut.ResolveDfa(new DfaAttackContext
        {
            Attacker = attacker,
            Target = target,
            Map = _map,
            StartCoordinates = new HexCoordinates(0, 2),
            ApproachCoordinates = new HexPosition(new HexCoordinates(0, 1), HexDirection.Top)
        });

        var expected = _rulesProvider.GetHitLocation(7, HitDirection.Rear);
        result.Dfa!.Outcome.ShouldBe(DfaOutcome.Hit);
        result.Dfa.TargetHitLocations!.HitLocations
            .ShouldAllBe(hit => hit.InitialLocation == expected);
        result.Dfa.TargetHitLocations.HitLocations
            .ShouldAllBe(hit => hit.LocationRoll.Sum() == 7);
    }

    [Fact]
    public void ResolveDfa_AgainstAProneTarget_KeepsTheAttackDirectionButDealsRearDamage()
    {
        _toHitCalculator.GetDfaToHitNumber(Arg.Any<IUnit>(), Arg.Any<IUnit>(), Arg.Any<IBattleMap>())
            .Returns(7);
        QueueRoll2D6(3, 4);
        QueueRoll2D6(3, 4);
        QueueRoll2D6(3, 4);
        QueueRoll2D6(3, 4);
        UseQueuedRoll2D6();
        _diceRoller.RollD6().Returns(new DiceResult(1), new DiceResult(2));
        var target = CreateUnit(20, TargetPosition());
        target.Status.Returns(UnitStatus.Prone);
        var attacker = CreateUnit(35, new HexPosition(new HexCoordinates(0, -2), HexDirection.Top));

        var result = _sut.ResolveDfa(new DfaAttackContext
        {
            Attacker = attacker,
            Target = target,
            Map = _map,
            StartCoordinates = new HexCoordinates(0, -2),
            ApproachCoordinates = new HexPosition(new HexCoordinates(0, -1), HexDirection.Top)
        });

        // The jump origin is in front of the target, so the rendered direction stays Front...
        result.AttackResolution.AttackDirection.ShouldBe(HitDirection.Front);
        // ...but a prone target always takes damage on the rear column.
        result.AttackResolution.DamageDirection.ShouldBe(HitDirection.Rear);
    }

    private static readonly HexCoordinates TargetCoordinates = new(0, 0);
    private static readonly HexDirection TargetFacing = HexDirection.Top;

    private static HexPosition TargetPosition() => new(TargetCoordinates, TargetFacing);

    private static IUnit CreateUnit(int tonnage, HexPosition position)
    {
        var unit = Substitute.For<IUnit>();
        unit.Tonnage.Returns(tonnage);
        unit.Position.Returns(position);
        unit.Parts.Returns(new Dictionary<PartLocation, UnitPart>());
        return unit;
    }

    private void QueueRoll2D6(params int[] results) =>
        _roll2D6.Enqueue(results.Select(result => new DiceResult(result)).ToList());

    private void UseQueuedRoll2D6() =>
        _diceRoller.Roll2D6().Returns(_ =>
            _roll2D6.Count > 0
                ? _roll2D6.Dequeue()
                : [new DiceResult(3), new DiceResult(4)]);

    private void SetHitRoll(params int[] results) =>
        _diceRoller.Roll2D6().Returns(results.Select(result => new DiceResult(result)).ToList());
}
