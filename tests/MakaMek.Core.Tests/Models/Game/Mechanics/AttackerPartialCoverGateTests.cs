using Microsoft.Extensions.Logging;
using NSubstitute;
using Sanet.MakaMek.Core.Data.Units.Components;
using Sanet.MakaMek.Core.Models.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Mechanics.WeaponAttack;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;
using Sanet.MakaMek.Map.Models;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Mechanics;

public class AttackerPartialCoverGateTests
{
    private readonly IRulesProvider _rulesProvider = Substitute.For<IRulesProvider>();
    private readonly IBattleMap _battleMap = Substitute.For<IBattleMap>();
    private readonly ILogger _logger = Substitute.For<ILogger>();
    private readonly AttackerPartialCoverGate _sut = new();

    private readonly IUnit _attacker = Substitute.For<IUnit>();
    private readonly IUnit _target = Substitute.For<IUnit>();
    private readonly Weapon _weapon = new TestWeapon();

    public AttackerPartialCoverGateTests()
    {
        _attacker.Position.Returns(new HexPosition(0, 0, HexDirection.Top));
        _attacker.Height.Returns(2);

        _target.Position.Returns(new HexPosition(2, 2, HexDirection.Top));
        _target.Height.Returns(2);
    }

    private LineOfSightResult SetupLos()
    {
        var losResult = LineOfSightResult.Unblocked(
            _target.Position!.Coordinates,
            _attacker.Position!.Coordinates,
            _target.Height,
            _attacker.Height);
        _battleMap.GetLineOfSight(
                _target.Position!.Coordinates,
                _attacker.Position!.Coordinates,
                _target.Height,
                _attacker.Height)
            .Returns(losResult);
        return losResult;
    }

    [Fact]
    public void ShouldSkip_ReturnsTrue_WhenAttackerHasPartialCoverAndWeaponLocationCanBeCovered()
    {
        var losResult = SetupLos();
        _rulesProvider.HasPartialCover(_attacker, losResult).Returns(true);
        _rulesProvider.CanPartBeCovered(PartLocation.LeftLeg).Returns(true);

        var primaryAssignment = new LocationSlotAssignment(PartLocation.LeftLeg, 1, 1);

        var result = _sut.ShouldSkip(_attacker, _target, _weapon, primaryAssignment, _battleMap, _rulesProvider, _logger);

        result.ShouldBeTrue();
    }

    [Fact]
    public void ShouldSkip_LogsMessage_WhenAttackIsSkipped()
    {
        var losResult = SetupLos();
        _rulesProvider.HasPartialCover(_attacker, losResult).Returns(true);
        _rulesProvider.CanPartBeCovered(PartLocation.LeftLeg).Returns(true);

        var primaryAssignment = new LocationSlotAssignment(PartLocation.LeftLeg, 1, 1);

        _sut.ShouldSkip(_attacker, _target, _weapon, primaryAssignment, _battleMap, _rulesProvider, _logger);

        _logger.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Skipping weapon at LeftLeg")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public void ShouldSkip_ReturnsFalse_WhenAttackerDoesNotHavePartialCover()
    {
        var losResult = SetupLos();
        _rulesProvider.HasPartialCover(_attacker, losResult).Returns(false);
        _rulesProvider.CanPartBeCovered(PartLocation.LeftLeg).Returns(true);

        var primaryAssignment = new LocationSlotAssignment(PartLocation.LeftLeg, 1, 1);

        var result = _sut.ShouldSkip(_attacker, _target, _weapon, primaryAssignment, _battleMap, _rulesProvider, _logger);

        result.ShouldBeFalse();
    }

    [Fact]
    public void ShouldSkip_ReturnsFalse_WhenWeaponLocationCannotBeCovered()
    {
        var losResult = SetupLos();
        _rulesProvider.HasPartialCover(_attacker, losResult).Returns(true);
        _rulesProvider.CanPartBeCovered(PartLocation.CenterTorso).Returns(false);

        var primaryAssignment = new LocationSlotAssignment(PartLocation.CenterTorso, 1, 1);

        var result = _sut.ShouldSkip(_attacker, _target, _weapon, primaryAssignment, _battleMap, _rulesProvider, _logger);

        result.ShouldBeFalse();
    }

    [Fact]
    public void ShouldSkip_ReturnsFalse_WhenNoPartialCoverAndLocationCannotBeCovered()
    {
        var losResult = SetupLos();
        _rulesProvider.HasPartialCover(_attacker, losResult).Returns(false);
        _rulesProvider.CanPartBeCovered(PartLocation.CenterTorso).Returns(false);

        var primaryAssignment = new LocationSlotAssignment(PartLocation.CenterTorso, 1, 1);

        var result = _sut.ShouldSkip(_attacker, _target, _weapon, primaryAssignment, _battleMap, _rulesProvider, _logger);

        result.ShouldBeFalse();
    }

    private class TestWeapon(WeaponType type = WeaponType.Energy,
            MakaMekComponent? ammoType = null,
            int damage = 5,
            int externalHeat = 2)
        : Weapon(new WeaponDefinition(
            "Test Weapon", damage, 3,
            new WeaponRange(0, 3, 6, 9),
            type, 10, null, 1, 1, 1, 1, MakaMekComponent.MachineGun, ammoType, externalHeat));
}
