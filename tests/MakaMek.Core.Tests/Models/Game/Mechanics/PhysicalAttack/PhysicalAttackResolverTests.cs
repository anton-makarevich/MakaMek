using NSubstitute;
using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Mechanics.PhysicalAttack;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Pilots;
using Sanet.MakaMek.Map.Models;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Mechanics.PhysicalAttack;

/// <summary>Tests the simplified punch, kick, and push resolution contract.</summary>
public class PhysicalAttackResolverTests
{
    private readonly IRulesProvider _rules = Substitute.For<IRulesProvider>();
    private readonly IDiceRoller _dice = Substitute.For<IDiceRoller>();
    private readonly IDamageTransferCalculator _damage = Substitute.For<IDamageTransferCalculator>();
    private readonly IUnit _attacker = Substitute.For<IUnit>();
    private readonly IUnit _target = Substitute.For<IUnit>();
    private readonly PhysicalAttackResolver _sut;

    public PhysicalAttackResolverTests()
    {
        _sut = new PhysicalAttackResolver(_rules, _dice, _damage);
        _attacker.Tonnage.Returns(50);
        _attacker.Pilot.Returns(Substitute.For<IPilot>());
        _attacker.Pilot!.Piloting.Returns(5);
        _attacker.Position.Returns(new HexPosition(new HexCoordinates(1, 1), HexDirection.Top));
        _target.Position.Returns(new HexPosition(new HexCoordinates(1, 2), HexDirection.Top));
        _rules.GetHitLocation(Arg.Any<int>(), Arg.Any<HitDirection>()).Returns(PartLocation.CenterTorso);
        _damage.CalculateStructureDamage(
                Arg.Any<IUnit>(), Arg.Any<PartLocation>(), Arg.Any<int>(), Arg.Any<HitDirection>(), Arg.Any<IReadOnlyList<LocationHitData>?>())
            .Returns([new LocationDamageData(PartLocation.CenterTorso, 10, 0, false)]);
    }

    [Fact]
    public void Resolve_WhenPunchHits_ShouldUseTenPercentTonnageDamage()
    {
        _dice.Roll2D6().Returns([new DiceResult(3), new DiceResult(3)], [new DiceResult(4), new DiceResult(4)]);

        var result = _sut.Resolve(_attacker, _target, PhysicalAttackType.Punch);

        result.IsHit.ShouldBeTrue();
        result.ToHitNumber.ShouldBe(5);
        result.HitLocationsData!.TotalDamage.ShouldBe(5);
        result.HitLocationsData.HitLocations[0].LocationRoll.Sum().ShouldBe(8);
        _damage.Received(1).CalculateStructureDamage(
            _target, PartLocation.CenterTorso, 5, Arg.Any<HitDirection>(), Arg.Any<IReadOnlyList<LocationHitData>?>());
    }

    [Fact]
    public void Resolve_WhenKickHits_ShouldUseTwentyPercentTonnageDamage()
    {
        _dice.Roll2D6().Returns([new DiceResult(6), new DiceResult(6)], [new DiceResult(4), new DiceResult(4)]);

        var result = _sut.Resolve(_attacker, _target, PhysicalAttackType.Kick);

        result.IsHit.ShouldBeTrue();
        result.HitLocationsData!.TotalDamage.ShouldBe(10);
    }

    [Fact]
    public void Resolve_WhenPushHits_ShouldReturnDisplacementWithoutDamage()
    {
        _dice.Roll2D6().Returns([new DiceResult(6), new DiceResult(6)]);

        var result = _sut.Resolve(_attacker, _target, PhysicalAttackType.Push);

        result.IsHit.ShouldBeTrue();
        result.HitLocationsData.ShouldBeNull();
        result.DisplacementTarget.ShouldBe(new Sanet.MakaMek.Map.Data.HexCoordinateData(1, 3));
        _damage.DidNotReceiveWithAnyArgs().CalculateStructureDamage(default!, default, default, default, default);
    }

    [Fact]
    public void Resolve_WhenPushMisses_ShouldNotReturnDisplacement()
    {
        _dice.Roll2D6().Returns([new DiceResult(1), new DiceResult(1)]);

        var result = _sut.Resolve(_attacker, _target, PhysicalAttackType.Push);

        result.IsHit.ShouldBeFalse();
        result.DisplacementTarget.ShouldBeNull();
        result.HitLocationsData.ShouldBeNull();
    }

    [Fact]
    public void Resolve_WhenAttackRollMisses_ShouldNotCalculateDamage()
    {
        _dice.Roll2D6().Returns([new DiceResult(1), new DiceResult(1)]);

        var result = _sut.Resolve(_attacker, _target, PhysicalAttackType.Punch);

        result.IsHit.ShouldBeFalse();
        result.HitLocationsData.ShouldBeNull();
        _damage.DidNotReceiveWithAnyArgs().CalculateStructureDamage(default!, default, default, default, default);
    }
}
