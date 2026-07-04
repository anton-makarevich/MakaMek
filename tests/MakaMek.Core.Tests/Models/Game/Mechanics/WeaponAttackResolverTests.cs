using NSubstitute;
using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Units.Components;
using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Models.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Mechanics.WeaponAttack;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Core.Tests.Utils;
using Sanet.MakaMek.Core.Utils;
using Sanet.MakaMek.Localization;
using Sanet.MakaMek.Map.Data;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Mechanics;

public class WeaponAttackResolverTests
{
    private readonly IDiceRoller _diceRoller = Substitute.For<IDiceRoller>();
    private readonly IRulesProvider _rulesProvider = new TotalWarfareRulesProvider();
    private readonly IDamageTransferCalculator _damageTransferCalculator = Substitute.For<IDamageTransferCalculator>();
    private readonly IToHitCalculator _toHitCalculator = Substitute.For<IToHitCalculator>();
    private readonly IComponentProvider _componentProvider = new ClassicBattletechComponentProvider();
    private readonly ILocalizationService _localizationService = Substitute.For<ILocalizationService>();
    private readonly WeaponAttackResolver _sut;

    public WeaponAttackResolverTests()
    {
        _damageTransferCalculator.CalculateStructureDamage(
                Arg.Any<Unit>(),
                Arg.Any<PartLocation>(),
                Arg.Any<int>(),
                Arg.Any<HitDirection>(),
                Arg.Any<IReadOnlyList<LocationHitData>?>())
            .Returns(callInfo => [new LocationDamageData(
                callInfo.Arg<PartLocation>(), callInfo.Arg<int>(), 0, false)]);

        _sut = new WeaponAttackResolver(_rulesProvider, _diceRoller, _damageTransferCalculator, _toHitCalculator);
    }

    private static LocationHitData InvokeDetermineHitLocation(WeaponAttackResolver resolver, HitDirection hitDirection, int dmg,
        Unit? target, WeaponTargetData? weaponTargetData = null, bool hasPartialCover = false, HexCoordinateData? coveringHex = null)
    {
        weaponTargetData ??= new WeaponTargetData
        {
            Weapon = new ComponentData
            {
                Name = "Test Weapon",
                Type = MakaMekComponent.MachineGun,
                Assignments = [new LocationSlotAssignment(PartLocation.RightArm, 1, 2)]
            },
            TargetId = target?.Id ?? Guid.NewGuid(),
            IsPrimaryTarget = false
        };
        var weapon = new TestWeapon();
        var method = typeof(WeaponAttackResolver).GetMethod("DetermineHitLocation",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return (LocationHitData)method!.Invoke(resolver, [hitDirection, dmg, target, weapon, weaponTargetData, null, hasPartialCover, coveringHex])!;
    }

    [Fact]
    public void DetermineHitLocation_ShouldTransferToNextLocation_WhenInitialLocationIsDestroyed()
    {
        var mockRulesProvider = Substitute.For<IRulesProvider>();
        var sut = new WeaponAttackResolver(mockRulesProvider, _diceRoller, _damageTransferCalculator, _toHitCalculator);

        var leftArm = new Arm("LeftArm", PartLocation.LeftArm, 5, 5);
        var leftTorso = new SideTorso("LeftTorso", PartLocation.LeftTorso, 10, 5, 10);
        var centerTorso = new CenterTorso("CenterTorso", 15, 10, 15);

        leftArm.ApplyDamage(10, HitDirection.Front);
        leftArm.IsDestroyed.ShouldBeTrue();

        var mech = new Mech("TestChassis", "TestModel", 50, [leftArm, leftTorso, centerTorso]);

        mockRulesProvider.GetHitLocation(Arg.Any<int>(), HitDirection.Front).Returns(PartLocation.LeftArm);

        _diceRoller.Roll2D6().Returns(
            [new DiceResult(5), new DiceResult(5)]
        );

        var weaponTargetData = new WeaponTargetData
        {
            Weapon = new ComponentData
            {
                Name = "Test Weapon",
                Type = MakaMekComponent.MachineGun,
                Assignments = [new LocationSlotAssignment(PartLocation.RightArm, 1, 2)]
            },
            TargetId = mech.Id,
            IsPrimaryTarget = false
        };

        var data = InvokeDetermineHitLocation(sut, HitDirection.Front, 5, mech, weaponTargetData);

        data.InitialLocation.ShouldBe(PartLocation.LeftArm);
        data.Damage[0].Location.ShouldBe(PartLocation.LeftTorso);
    }

    [Fact]
    public void DetermineHitLocation_ShouldTransferMultipleTimes_WhenMultipleLocationsInChainAreDestroyed()
    {
        var mockRulesProvider = Substitute.For<IRulesProvider>();
        var sut = new WeaponAttackResolver(mockRulesProvider, _diceRoller, _damageTransferCalculator, _toHitCalculator);

        var leftArm = new Arm("LeftArm", PartLocation.LeftArm, 5, 5);
        var leftTorso = new SideTorso("LeftTorso", PartLocation.LeftTorso, 10, 5, 10);
        var centerTorso = new CenterTorso("CenterTorso", 15, 10, 15);

        leftArm.ApplyDamage(10, HitDirection.Front);
        leftTorso.ApplyDamage(20, HitDirection.Front);

        leftArm.IsDestroyed.ShouldBeTrue();
        leftTorso.IsDestroyed.ShouldBeTrue();

        var mech = new Mech("TestChassis", "TestModel", 50, [leftArm, leftTorso, centerTorso]);

        mockRulesProvider.GetHitLocation(Arg.Any<int>(), HitDirection.Front).Returns(PartLocation.LeftArm);

        _diceRoller.Roll2D6().Returns(
            [new DiceResult(5), new DiceResult(5)]
        );

        var weaponTargetData = new WeaponTargetData
        {
            Weapon = new ComponentData
            {
                Name = "Test Weapon",
                Type = MakaMekComponent.MachineGun,
                Assignments = [new LocationSlotAssignment(PartLocation.RightArm, 1, 2)]
            },
            TargetId = mech.Id,
            IsPrimaryTarget = false
        };

        var data = InvokeDetermineHitLocation(sut, HitDirection.Front, 5, mech, weaponTargetData);

        data.Damage.Last().Location.ShouldBe(PartLocation.CenterTorso);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void DetermineHitLocation_WithSuccessfulAimedShot_ShouldHitIntendedLocation(int secondD6)
    {
        var mechData = MechFactoryTests.CreateDummyMechData();
        var mech = new MechFactory(
            _rulesProvider,
            _componentProvider,
            _localizationService).Create(mechData);
        var shutdownData = new ShutdownData { Reason = ShutdownReason.Voluntary, Turn = 1 };
        mech.Shutdown(shutdownData);

        _diceRoller.Roll2D6().Returns(
            [new DiceResult(5), new DiceResult(secondD6)]
        );

        var weaponTargetData = new WeaponTargetData
        {
            Weapon = new ComponentData
            {
                Name = "Test Weapon",
                Type = MakaMekComponent.MachineGun,
                Assignments = [new LocationSlotAssignment(PartLocation.RightArm, 1, 2)]
            },
            TargetId = Guid.NewGuid(),
            IsPrimaryTarget = false,
            AimedShotTarget = PartLocation.LeftArm
        };

        var data = InvokeDetermineHitLocation(_sut, HitDirection.Front, 5, mech, weaponTargetData);

        data.InitialLocation.ShouldBe(PartLocation.LeftArm);
        data.Damage[0].Location.ShouldBe(PartLocation.LeftArm);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(6)]
    public void DetermineHitLocation_WithUnsuccessfulAimedShot_ShouldHitLocationByTable(int secondD6)
    {
        var mockRulesProvider = Substitute.For<IRulesProvider>();
        var sut = new WeaponAttackResolver(mockRulesProvider, _diceRoller, _damageTransferCalculator, _toHitCalculator);

        var mechData = MechFactoryTests.CreateDummyMechData();
        var mech = new MechFactory(
            _rulesProvider,
            _componentProvider,
            _localizationService).Create(mechData);
        var shutdownData = new ShutdownData { Reason = ShutdownReason.Voluntary, Turn = 1 };
        mech.Shutdown(shutdownData);

        mockRulesProvider.GetHitLocation(Arg.Any<int>(), HitDirection.Front).Returns(PartLocation.CenterTorso);
        mockRulesProvider.GetAimedShotSuccessValues().Returns([6, 7, 8]);

        _diceRoller.Roll2D6().Returns(
            [new DiceResult(4), new DiceResult(secondD6)]
        );

        var weaponTargetData = new WeaponTargetData
        {
            Weapon = new ComponentData
            {
                Name = "Test Weapon",
                Type = MakaMekComponent.MachineGun,
                Assignments = [new LocationSlotAssignment(PartLocation.RightArm, 1, 2)]
            },
            TargetId = Guid.NewGuid(),
            IsPrimaryTarget = false,
            AimedShotTarget = PartLocation.LeftArm
        };

        var data = InvokeDetermineHitLocation(sut, HitDirection.Front, 5, mech, weaponTargetData);

        data.InitialLocation.ShouldBe(PartLocation.CenterTorso);
        data.Damage[0].Location.ShouldBe(PartLocation.CenterTorso);
    }

    [Fact]
    public void DetermineHitLocation_ShouldAbsorbDamageByCoveringHex_WhenPartialCoverAppliesToLeg()
    {
        var mockRulesProvider = Substitute.For<IRulesProvider>();
        var sut = new WeaponAttackResolver(mockRulesProvider, _diceRoller, _damageTransferCalculator, _toHitCalculator);

        var leftLeg = new Leg("LeftLeg", PartLocation.LeftLeg, 10, 5);
        var centerTorso = new CenterTorso("CenterTorso", 15, 10, 15);

        var mech = new Mech("TestChassis", "TestModel", 50, [leftLeg, centerTorso]);

        mockRulesProvider.GetHitLocation(Arg.Any<int>(), HitDirection.Front).Returns(PartLocation.LeftLeg);
        mockRulesProvider.CanPartBeCovered(PartLocation.LeftLeg).Returns(true);

        _diceRoller.Roll2D6().Returns(
            [new DiceResult(5), new DiceResult(4)]
        );

        var weaponTargetData = new WeaponTargetData
        {
            Weapon = new ComponentData
            {
                Name = "Test Weapon",
                Type = MakaMekComponent.MachineGun,
                Assignments = [new LocationSlotAssignment(PartLocation.RightArm, 1, 2)]
            },
            TargetId = mech.Id,
            IsPrimaryTarget = false
        };

        var data = InvokeDetermineHitLocation(sut, HitDirection.Front, 5, mech, weaponTargetData, true, new HexCoordinateData(1, 1));

        data.Damage.ShouldBeEmpty();
        data.CoveringHexAbsorption.ShouldNotBeNull();
        data.CoveringHexAbsorption!.AbsorbedDamage.ShouldBe(5);
        data.CoveringHexAbsorption.CoveringHex.ShouldBe(new HexCoordinateData(1, 1));
        data.InitialLocation.ShouldBe(PartLocation.LeftLeg);
    }

    [Fact]
    public void DetermineHitLocation_ShouldApplyDamageNormally_WhenPartialCoverButNotCoveredLocation()
    {
        var mockRulesProvider = Substitute.For<IRulesProvider>();
        var sut = new WeaponAttackResolver(mockRulesProvider, _diceRoller, _damageTransferCalculator, _toHitCalculator);

        var centerTorso = new CenterTorso("CenterTorso", 15, 10, 15);

        var mech = new Mech("TestChassis", "TestModel", 50, [centerTorso]);

        mockRulesProvider.GetHitLocation(Arg.Any<int>(), HitDirection.Front).Returns(PartLocation.CenterTorso);
        mockRulesProvider.CanPartBeCovered(PartLocation.CenterTorso).Returns(false);

        _diceRoller.Roll2D6().Returns(
            [new DiceResult(3), new DiceResult(4)]
        );

        var weaponTargetData = new WeaponTargetData
        {
            Weapon = new ComponentData
            {
                Name = "Test Weapon",
                Type = MakaMekComponent.MachineGun,
                Assignments = [new LocationSlotAssignment(PartLocation.RightArm, 1, 2)]
            },
            TargetId = mech.Id,
            IsPrimaryTarget = false
        };

        var data = InvokeDetermineHitLocation(sut, HitDirection.Front, 5, mech, weaponTargetData, true, new HexCoordinateData(1, 1));

        data.Damage.ShouldNotBeEmpty();
        data.CoveringHexAbsorption.ShouldBeNull();
        data.InitialLocation.ShouldBe(PartLocation.CenterTorso);
    }

    [Fact]
    public void ResolveAttack_ShouldThrowArgumentException_WhenWeaponIsNotMounted()
    {
        var unmountedWeapon = new TestWeapon();
        unmountedWeapon.FirstMountPart.ShouldBeNull();

        var weaponTargetData = new WeaponTargetData
        {
            Weapon = new ComponentData
            {
                Name = unmountedWeapon.Name,
                Type = unmountedWeapon.ComponentType,
                Assignments = [new LocationSlotAssignment(PartLocation.LeftArm, 0, 1)]
            },
            TargetId = Guid.NewGuid(),
            IsPrimaryTarget = true
        };

        var attacker = Substitute.For<IUnit>();
        var target = Substitute.For<IUnit>();

        var exception = Should.Throw<ArgumentException>(() =>
            _sut.ResolveAttack(attacker, target, unmountedWeapon, weaponTargetData, Substitute.For<Sanet.MakaMek.Map.Models.IBattleMap>()));

        exception.Message.ShouldBe("Weapon Test Weapon is not mounted (Parameter 'weapon')");
    }

    [Fact]
    public void ResolveAttack_ShouldThrowArgumentNullException_WhenBattleMapIsNull()
    {
        var attacker = Substitute.For<IUnit>();
        var target = Substitute.For<IUnit>();
        var weapon = new TestWeapon();
        var weaponTargetData = new WeaponTargetData
        {
            Weapon = new ComponentData
            {
                Name = weapon.Name,
                Type = weapon.ComponentType,
                Assignments = [new LocationSlotAssignment(PartLocation.LeftArm, 0, 1)]
            },
            TargetId = Guid.NewGuid(),
            IsPrimaryTarget = true
        };

        Should.Throw<ArgumentNullException>(() =>
            _sut.ResolveAttack(attacker, target, weapon, weaponTargetData, null!));
    }

    [Fact]
    public void ResolveAttack_ShouldReturnMiss_WhenToHitRollIsBelowTarget()
    {
        var weapon = CreateMountedWeapon();
        var weaponTargetData = CreateWeaponTargetData(weapon);
        var attacker = CreateAttackerWithPosition();
        var target = CreateTargetWithPosition();
        var battleMap = Substitute.For<Sanet.MakaMek.Map.Models.IBattleMap>();

        _toHitCalculator.GetToHitNumber(attacker, target, weapon, battleMap, true, null).Returns(12);
        _diceRoller.Roll2D6().Returns([new DiceResult(1), new DiceResult(1)]);

        var result = _sut.ResolveAttack(attacker, target, weapon, weaponTargetData, battleMap);

        result.IsHit.ShouldBeFalse();
        result.ToHitNumber.ShouldBe(12);
        result.HitLocationsData.ShouldBeNull();
    }

    [Fact]
    public void ResolveAttack_ShouldReturnHit_WhenToHitRollIsSufficient()
    {
        var weapon = CreateMountedWeapon();
        var weaponTargetData = CreateWeaponTargetData(weapon);
        var attacker = CreateAttackerWithPosition();
        var target = CreateTargetWithPosition();
        var battleMap = Substitute.For<Sanet.MakaMek.Map.Models.IBattleMap>();

        _toHitCalculator.GetToHitNumber(attacker, target, weapon, battleMap, true, null).Returns(2);
        _diceRoller.Roll2D6().Returns(
            [new DiceResult(3), new DiceResult(4)],
            [new DiceResult(5), new DiceResult(5)]
        );

        var losResult = LineOfSightResult.Unblocked(
            attacker.Position!.Coordinates,
            target.Position!.Coordinates,
            weapon.FirstMountPart!.Level,
            target.Height);
        battleMap.GetLineOfSight(
            attacker.Position!.Coordinates,
            target.Position!.Coordinates,
            weapon.FirstMountPart.Level,
            target.Height).Returns(losResult);

        var result = _sut.ResolveAttack(attacker, target, weapon, weaponTargetData, battleMap);

        result.IsHit.ShouldBeTrue();
        result.ToHitNumber.ShouldBe(2);
        result.HitLocationsData.ShouldNotBeNull();
        result.HitLocationsData.HitLocations.ShouldNotBeEmpty();
        result.HitLocationsData.TotalDamage.ShouldBe(weapon.Damage);
    }

    [Fact]
    public void ResolveAttack_WithClusterWeapon_ShouldDistributeDamageAcrossMultipleHitLocations()
    {
        var weapon = CreateMountedWeapon(clusters: 5, clusterSize: 3);
        var weaponTargetData = CreateWeaponTargetData(weapon);
        var attacker = CreateAttackerWithPosition();
        var target = CreateTargetWithPosition();
        var battleMap = Substitute.For<Sanet.MakaMek.Map.Models.IBattleMap>();
        var mockRulesProvider = Substitute.For<IRulesProvider>();
        var sut = new WeaponAttackResolver(mockRulesProvider, _diceRoller, _damageTransferCalculator, _toHitCalculator);

        _toHitCalculator.GetToHitNumber(attacker, target, weapon, battleMap, true, null).Returns(2);
        mockRulesProvider.GetClusterHits(Arg.Any<int>(), 15).Returns(12);
        mockRulesProvider.GetHitLocation(Arg.Any<int>(), HitDirection.Front).Returns(PartLocation.LeftTorso);
        mockRulesProvider.HasPartialCover(target, Arg.Any<LineOfSightResult>()).Returns(false);

        _diceRoller.Roll2D6().Returns(
            [new DiceResult(3), new DiceResult(4)],
            [new DiceResult(6), new DiceResult(5)],
            [new DiceResult(5), new DiceResult(5)],
            [new DiceResult(5), new DiceResult(5)],
            [new DiceResult(5), new DiceResult(5)],
            [new DiceResult(5), new DiceResult(5)]
        );

        var losResult = LineOfSightResult.Unblocked(
            attacker.Position!.Coordinates,
            target.Position!.Coordinates,
            weapon.FirstMountPart!.Level,
            target.Height);
        battleMap.GetLineOfSight(
            attacker.Position!.Coordinates,
            target.Position!.Coordinates,
            weapon.FirstMountPart.Level,
            target.Height).Returns(losResult);

        var result = sut.ResolveAttack(attacker, target, weapon, weaponTargetData, battleMap);

        result.IsHit.ShouldBeTrue();
        result.HitLocationsData.ShouldNotBeNull();
        result.HitLocationsData.HitLocations.Count.ShouldBe(4);
        result.HitLocationsData.TotalDamage.ShouldBe(60);
    }

    [Fact]
    public void DetermineAttackDirection_ShouldReturnFront_WhenAttackerPositionIsNull()
    {
        var attacker = Substitute.For<IUnit>();
        var target = Substitute.For<IUnit>();
        target.Position.Returns(new HexPosition(0, 0, HexDirection.Top));
        attacker.Position.Returns((HexPosition?)null);

        var method = typeof(WeaponAttackResolver).GetMethod("DetermineAttackDirection",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        var result = (HitDirection)method!.Invoke(_sut, [attacker, target])!;

        result.ShouldBe(HitDirection.Front);
    }

    [Fact]
    public void DetermineAttackDirection_ShouldReturnFront_WhenTargetPositionIsNull()
    {
        var attacker = Substitute.For<IUnit>();
        var target = Substitute.For<IUnit>();
        attacker.Position.Returns(new HexPosition(0, 0, HexDirection.Top));
        target.Position.Returns((HexPosition?)null);

        var method = typeof(WeaponAttackResolver).GetMethod("DetermineAttackDirection",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        var result = (HitDirection)method!.Invoke(_sut, [attacker, target])!;

        result.ShouldBe(HitDirection.Front);
    }

    [Fact]
    public void DetermineAttackDirection_ShouldReturnLeft_WhenTargetInLeftFiringArc()
    {
        var attacker = Substitute.For<IUnit>();
        var target = Substitute.For<IUnit>();
        target.Position.Returns(new HexPosition(0, 0, HexDirection.Top));
        attacker.Position.Returns(new HexPosition(-2, 0, HexDirection.Top));

        var method = typeof(WeaponAttackResolver).GetMethod("DetermineAttackDirection",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        var result = (HitDirection)method!.Invoke(_sut, [attacker, target])!;

        result.ShouldBe(HitDirection.Left);
    }

    [Fact]
    public void DetermineAttackDirection_ShouldReturnRight_WhenTargetInRightFiringArc()
    {
        var attacker = Substitute.For<IUnit>();
        var target = Substitute.For<IUnit>();
        target.Position.Returns(new HexPosition(0, 0, HexDirection.Top));
        attacker.Position.Returns(new HexPosition(2, 0, HexDirection.Top));

        var method = typeof(WeaponAttackResolver).GetMethod("DetermineAttackDirection",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        var result = (HitDirection)method!.Invoke(_sut, [attacker, target])!;

        result.ShouldBe(HitDirection.Right);
    }

    private static Weapon CreateMountedWeapon(int clusters = 1, int clusterSize = 1)
    {
        var weapon = new TestWeapon(clusters: clusters, clusterSize: clusterSize);
        var mountPart = new Arm("RightArm", PartLocation.RightArm, 5, 5);
        weapon.Mount(mountPart, [0]);
        return weapon;
    }

    private static WeaponTargetData CreateWeaponTargetData(Weapon weapon)
    {
        return new WeaponTargetData
        {
            Weapon = new ComponentData
            {
                Name = weapon.Name,
                Type = weapon.ComponentType,
                Assignments = [new LocationSlotAssignment(PartLocation.RightArm, 0, 1)]
            },
            TargetId = Guid.NewGuid(),
            IsPrimaryTarget = true
        };
    }

    private static IUnit CreateAttackerWithPosition()
    {
        var attacker = Substitute.For<IUnit>();
        attacker.Position.Returns(new HexPosition(0, 0, HexDirection.Top));
        attacker.Height.Returns(2);
        return attacker;
    }

    private static IUnit CreateTargetWithPosition()
    {
        var target = Substitute.For<IUnit>();
        target.Position.Returns(new HexPosition(3, 3, HexDirection.Top));
        target.Height.Returns(2);
        return target;
    }

    private class TestWeapon(WeaponType type = WeaponType.Energy, MakaMekComponent? ammoType = null,
            int damage = 5, int externalHeat = 2, int clusters = 1, int clusterSize = 1)
        : Weapon(new WeaponDefinition(
            "Test Weapon", damage, 3,
            new WeaponRange(0, 3, 6, 9),
            type, 10, null, clusters, clusterSize, 1, 1, MakaMekComponent.MachineGun, ammoType, externalHeat));
}
