using NSubstitute;
using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Data.Units.Components;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Models.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Mechanics.PhysicalAttack;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components;
using Sanet.MakaMek.Core.Models.Units.Components.Engines;
using Sanet.MakaMek.Core.Models.Units.Components.Internal.Actuators;
using Sanet.MakaMek.Core.Utils;
using Sanet.MakaMek.Map.Models;
using Sanet.Localization;
using Sanet.MakaMek.Core.Data.Game.Mechanics.PhysicalAttacks;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Mechanics;

public class PhysicalAttackResolverTests
{
    private readonly IDiceRoller _diceRoller = Substitute.For<IDiceRoller>();
    private readonly IPhysicalAttackCalculator _toHitCalculator = Substitute.For<IPhysicalAttackCalculator>();
    private readonly IDamageTransferCalculator _damageTransferCalculator = Substitute.For<IDamageTransferCalculator>();
    private readonly IRulesProvider _rulesProvider = new TotalWarfareRulesProvider();
    private readonly IBattleMap _map = Substitute.For<IBattleMap>();
    private readonly PhysicalAttackResolver _sut;

    public PhysicalAttackResolverTests()
    {
        _toHitCalculator
            .GetToHitNumber(Arg.Any<IUnit>(), Arg.Any<IUnit>(), Arg.Any<PhysicalAttackType>(), Arg.Any<IBattleMap>())
            .Returns(7);

        _damageTransferCalculator
            .CalculateStructureDamage(
                Arg.Any<IUnit>(),
                Arg.Any<PartLocation>(),
                Arg.Any<int>(),
                Arg.Any<HitDirection>(),
                Arg.Any<IReadOnlyList<LocationHitData>?>())
            .Returns(callInfo => [new LocationDamageData(
                callInfo.Arg<PartLocation>(), callInfo.Arg<int>(), 0, false)]);

        _sut = new PhysicalAttackResolver(
            _toHitCalculator,
            _diceRoller,
            _rulesProvider,
            _damageTransferCalculator);
    }

    #region Test data helpers

    private static readonly HexCoordinates TargetCoordinates = new(0, 0);
    private static readonly HexDirection TargetFacing = HexDirection.Top;

    private static HexPosition TargetPosition() => new(TargetCoordinates, TargetFacing);

    /// <summary>
    /// The position of an attacker that enters the target's hex from the given arc.
    /// </summary>
    private static HexPosition AttackerPositionInArc(FiringArc arc)
    {
        var attackerCoordinates = TargetCoordinates
            .GetAllNeighbours()
            .First(neighbour => TargetCoordinates.GetFiringArc(neighbour, TargetFacing) == arc);

        return new HexPosition(attackerCoordinates, TargetFacing);
    }

    private static IUnit CreateUnit(int tonnage, HexPosition position)
    {
        var unit = Substitute.For<IUnit>();
        unit.Tonnage.Returns(tonnage);
        unit.Position.Returns(position);
        unit.Parts.Returns(new Dictionary<PartLocation, UnitPart>());
        unit.GetComponentsAtLocation<UpperArmActuator>(Arg.Any<PartLocation>())
            .Returns([new UpperArmActuator()]);
        unit.GetComponentsAtLocation<LowerArmActuator>(Arg.Any<PartLocation>())
            .Returns([new LowerArmActuator()]);
        unit.GetComponentsAtLocation<UpperLegActuator>(Arg.Any<PartLocation>())
            .Returns([new UpperLegActuator()]);
        unit.GetComponentsAtLocation<LowerLegActuator>(Arg.Any<PartLocation>())
            .Returns([new LowerLegActuator()]);
        return unit;
    }

    private PhysicalAttackContext CreateContext(
        IUnit attacker,
        IUnit target,
        PhysicalAttackType attackType,
        PartLocation limb,
        IBattleMap? map = null) => new()
    {
        Attacker = attacker,
        Target = target,
        AttackType = attackType,
        AttackerLimb = limb,
        Map = map ?? _map
    };

    private void SetHitRoll(params int[] results) =>
        _diceRoller.Roll2D6().Returns(results.Select(result => new DiceResult(result)).ToList());

    private void SetLocationRoll(int result) =>
        _diceRoller.RollD6().Returns(new DiceResult(result));

    private static UnitData CreateMechData(int mass, Dictionary<PartLocation, ArmorLocation> armorValues) => new()
    {
        Chassis = "Test",
        Model = "Test-1",
        Mass = mass,
        EngineRating = 100,
        EngineType = "Fusion",
        ArmorValues = armorValues,
        Equipment =
        [
            new ComponentData
            {
                Type = MakaMekComponent.Engine,
                Assignments =
                [
                    new LocationSlotAssignment(PartLocation.CenterTorso, 0, 3),
                    new LocationSlotAssignment(PartLocation.CenterTorso, 7, 3)
                ],
                SpecificData = new EngineStateData(EngineType.Fusion, 100)
            }
        ],
        AdditionalAttributes = new Dictionary<string, string>(),
        Quirks = new Dictionary<string, string>()
    };

    private static UnitData CreateTargetMechData() => CreateMechData(20, new Dictionary<PartLocation, ArmorLocation>
    {
        { PartLocation.Head, new ArmorLocation { FrontArmor = 9 } },
        { PartLocation.CenterTorso, new ArmorLocation { FrontArmor = 5, RearArmor = 3 } },
        { PartLocation.LeftTorso, new ArmorLocation { FrontArmor = 8, RearArmor = 4 } },
        { PartLocation.RightTorso, new ArmorLocation { FrontArmor = 8, RearArmor = 4 } },
        { PartLocation.LeftArm, new ArmorLocation { FrontArmor = 4 } },
        { PartLocation.RightArm, new ArmorLocation { FrontArmor = 4 } },
        { PartLocation.LeftLeg, new ArmorLocation { FrontArmor = 8 } },
        { PartLocation.RightLeg, new ArmorLocation { FrontArmor = 8 } }
    });

    private static UnitData CreateAttackerMechData(int mass)
    {
        var data = CreateMechData(mass, new Dictionary<PartLocation, ArmorLocation>
        {
            { PartLocation.Head, new ArmorLocation { FrontArmor = 9 } },
            { PartLocation.CenterTorso, new ArmorLocation { FrontArmor = 10, RearArmor = 5 } },
            { PartLocation.LeftTorso, new ArmorLocation { FrontArmor = 8, RearArmor = 4 } },
            { PartLocation.RightTorso, new ArmorLocation { FrontArmor = 8, RearArmor = 4 } },
            { PartLocation.LeftArm, new ArmorLocation { FrontArmor = 4 } },
            { PartLocation.RightArm, new ArmorLocation { FrontArmor = 4 } },
            { PartLocation.LeftLeg, new ArmorLocation { FrontArmor = 8 } },
            { PartLocation.RightLeg, new ArmorLocation { FrontArmor = 8 } }
        });

        return data with
        {
            Equipment =
            [
                .. data.Equipment,
                new ComponentData
                {
                    Type = MakaMekComponent.UpperArmActuator,
                    Assignments = [new LocationSlotAssignment(PartLocation.LeftArm, 1, 1)]
                },
                new ComponentData
                {
                    Type = MakaMekComponent.LowerArmActuator,
                    Assignments = [new LocationSlotAssignment(PartLocation.LeftArm, 2, 1)]
                },
                new ComponentData
                {
                    Type = MakaMekComponent.UpperArmActuator,
                    Assignments = [new LocationSlotAssignment(PartLocation.RightArm, 1, 1)]
                },
                new ComponentData
                {
                    Type = MakaMekComponent.LowerArmActuator,
                    Assignments = [new LocationSlotAssignment(PartLocation.RightArm, 2, 1)]
                }
            ]
        };
    }

    private (MechFactory MechFactory, DamageTransferCalculator DamageTransferCalculator, PhysicalAttackResolver Resolver)
        CreateRealPipeline()
    {
        var rules = new TotalWarfareRulesProvider();
        var mechFactory = new MechFactory(
            rules,
            new ClassicBattletechComponentProvider(),
            Substitute.For<ILocalizationService>());
        var damageTransferCalculator = new DamageTransferCalculator(mechFactory);
        var resolver = new PhysicalAttackResolver(
            _toHitCalculator,
            _diceRoller,
            rules,
            damageTransferCalculator);

        return (mechFactory, damageTransferCalculator, resolver);
    }

    #endregion

    #region Damage math

    [Theory]
    [InlineData(20, 2)]
    [InlineData(25, 3)]
    [InlineData(55, 6)]
    [InlineData(100, 10)]
    [InlineData(21, 3)]
    [InlineData(51, 6)]
    public void ResolveAttack_WithPunch_ShouldDoOnePointOfDamagePerTenTons(int tonnage, int expectedDamage)
    {
        var attacker = CreateUnit(tonnage, AttackerPositionInArc(FiringArc.Front));
        var target = CreateUnit(20, TargetPosition());
        SetHitRoll(5, 5);
        SetLocationRoll(1);

        var result = _sut.ResolveAttack(
            CreateContext(attacker, target, PhysicalAttackType.Punch, PartLocation.LeftArm));

        result.AttackResolution.IsHit.ShouldBeTrue();
        result.Damage.ShouldBe(expectedDamage);
    }

    [Theory]
    [InlineData(20, 4)]
    [InlineData(25, 5)]
    [InlineData(55, 11)]
    [InlineData(100, 20)]
    [InlineData(21, 5)]
    [InlineData(51, 11)]
    public void ResolveAttack_WithKick_ShouldDoOnePointOfDamagePerFiveTons(int tonnage, int expectedDamage)
    {
        var attacker = CreateUnit(tonnage, AttackerPositionInArc(FiringArc.Front));
        var target = CreateUnit(20, TargetPosition());
        SetHitRoll(5, 5);
        SetLocationRoll(1);

        var result = _sut.ResolveAttack(
            CreateContext(attacker, target, PhysicalAttackType.Kick, PartLocation.RightLeg));

        result.AttackResolution.IsHit.ShouldBeTrue();
        result.Damage.ShouldBe(expectedDamage);
    }

    #endregion

    #region Actuator damage

    [Fact]
    public void ResolveAttack_WithADamagedUpperArmActuator_ShouldHalveThePunchDamage()
    {
        var attacker = CreateUnit(55, AttackerPositionInArc(FiringArc.Front));
        var target = CreateUnit(20, TargetPosition());
        SetArmActuators(
            attacker,
            PartLocation.LeftArm,
            DestroyedActuator(new UpperArmActuator()),
            new LowerArmActuator());
        SetHitRoll(5, 5);
        SetLocationRoll(1);

        var result = _sut.ResolveAttack(
            CreateContext(attacker, target, PhysicalAttackType.Punch, PartLocation.LeftArm));

        result.Damage.ShouldBe(3);
    }

    [Fact]
    public void ResolveAttack_WithDamagedUpperAndLowerArmActuators_ShouldQuarterThePunchDamage()
    {
        var attacker = CreateUnit(55, AttackerPositionInArc(FiringArc.Front));
        var target = CreateUnit(20, TargetPosition());
        SetArmActuators(
            attacker,
            PartLocation.LeftArm,
            DestroyedActuator(new UpperArmActuator()),
            DestroyedActuator(new LowerArmActuator()));
        SetHitRoll(5, 5);
        SetLocationRoll(1);

        var result = _sut.ResolveAttack(
            CreateContext(attacker, target, PhysicalAttackType.Punch, PartLocation.LeftArm));

        result.Damage.ShouldBe(1);
    }

    [Fact]
    public void ResolveAttack_WhenActuatorDamageWouldReduceThePunchBelowOne_ShouldInflictOnePoint()
    {
        var attacker = CreateUnit(20, AttackerPositionInArc(FiringArc.Front));
        var target = CreateUnit(20, TargetPosition());
        SetArmActuators(
            attacker,
            PartLocation.LeftArm,
            DestroyedActuator(new UpperArmActuator()),
            DestroyedActuator(new LowerArmActuator()));
        SetHitRoll(5, 5);
        SetLocationRoll(1);

        var result = _sut.ResolveAttack(
            CreateContext(attacker, target, PhysicalAttackType.Punch, PartLocation.LeftArm));

        result.Damage.ShouldBe(1);
    }

    [Theory]
    [InlineData(false, true, 3)]
    [InlineData(true, false, 3)]
    [InlineData(true, true, 1)]
    public void ResolveAttack_WithMissingArmActuators_ShouldHalveThePunchDamageForEach(
        bool missingUpper,
        bool missingLower,
        int expectedDamage)
    {
        var attacker = CreateUnit(70, AttackerPositionInArc(FiringArc.Front));
        var target = CreateUnit(20, TargetPosition());
        var actuators = new List<Component>();
        if (!missingUpper) actuators.Add(new UpperArmActuator());
        if (!missingLower) actuators.Add(new LowerArmActuator());
        SetArmActuators(attacker, PartLocation.LeftArm, actuators.ToArray());
        SetHitRoll(5, 5);
        SetLocationRoll(1);

        var result = _sut.ResolveAttack(
            CreateContext(attacker, target, PhysicalAttackType.Punch, PartLocation.LeftArm));

        result.Damage.ShouldBe(expectedDamage);
    }

    [Fact]
    public void ResolveAttack_WithDamagedActuatorsInTheOtherArm_ShouldNotReduceThePunchDamage()
    {
        var attacker = CreateUnit(55, AttackerPositionInArc(FiringArc.Front));
        var target = CreateUnit(20, TargetPosition());
        SetArmActuators(
            attacker,
            PartLocation.RightArm,
            DestroyedActuator(new UpperArmActuator()),
            DestroyedActuator(new LowerArmActuator()));
        SetHitRoll(5, 5);
        SetLocationRoll(1);

        var result = _sut.ResolveAttack(
            CreateContext(attacker, target, PhysicalAttackType.Punch, PartLocation.LeftArm));

        result.Damage.ShouldBe(6);
    }

    [Fact]
    public void ResolveAttack_WithMissingActuatorsInTheOtherArm_ShouldNotReduceThePunchDamage()
    {
        var attacker = CreateUnit(55, AttackerPositionInArc(FiringArc.Front));
        var target = CreateUnit(20, TargetPosition());
        SetArmActuators(attacker, PartLocation.RightArm);
        SetHitRoll(5, 5);
        SetLocationRoll(1);

        var result = _sut.ResolveAttack(
            CreateContext(attacker, target, PhysicalAttackType.Punch, PartLocation.LeftArm));

        result.Damage.ShouldBe(6);
    }

    [Fact]
    public void ResolveAttack_WithADamagedUpperLegActuator_ShouldHalveTheKickDamage()
    {
        var attacker = CreateUnit(55, AttackerPositionInArc(FiringArc.Front));
        var target = CreateUnit(20, TargetPosition());
        SetLegActuators(attacker, PartLocation.RightLeg, DestroyedActuator(new UpperLegActuator()));
        SetHitRoll(5, 5);
        SetLocationRoll(1);

        var result = _sut.ResolveAttack(
            CreateContext(attacker, target, PhysicalAttackType.Kick, PartLocation.RightLeg));

        result.Damage.ShouldBe(5);
    }

    [Fact]
    public void ResolveAttack_WithDamagedActuatorsInTheKickingLeg_ShouldHalveTheKickDamageForEach()
    {
        var attacker = CreateUnit(55, AttackerPositionInArc(FiringArc.Front));
        var target = CreateUnit(20, TargetPosition());
        SetLegActuators(
            attacker,
            PartLocation.RightLeg,
            DestroyedActuator(new UpperLegActuator()),
            DestroyedActuator(new LowerLegActuator()));
        SetHitRoll(5, 5);
        SetLocationRoll(1);

        var result = _sut.ResolveAttack(
            CreateContext(attacker, target, PhysicalAttackType.Kick, PartLocation.RightLeg));

        result.Damage.ShouldBe(2);
    }

    [Fact]
    public void ResolveAttack_WithDamagedActuatorsInTheOtherLeg_ShouldNotReduceTheKickDamage()
    {
        var attacker = CreateUnit(55, AttackerPositionInArc(FiringArc.Front));
        var target = CreateUnit(20, TargetPosition());
        SetLegActuators(
            attacker,
            PartLocation.LeftLeg,
            DestroyedActuator(new UpperLegActuator()),
            DestroyedActuator(new LowerLegActuator()));
        SetHitRoll(5, 5);
        SetLocationRoll(1);

        var result = _sut.ResolveAttack(
            CreateContext(attacker, target, PhysicalAttackType.Kick, PartLocation.RightLeg));

        result.Damage.ShouldBe(11);
    }

    private static TActuator DestroyedActuator<TActuator>(TActuator actuator) where TActuator : Component
    {
        actuator.Hit();
        actuator.IsDestroyed.ShouldBeTrue();
        return actuator;
    }

    private static void SetArmActuators(IUnit attacker, PartLocation arm, params Component[] actuators)
    {
        attacker.GetComponentsAtLocation<UpperArmActuator>(arm)
            .Returns(actuators.OfType<UpperArmActuator>().ToList());
        attacker.GetComponentsAtLocation<LowerArmActuator>(arm)
            .Returns(actuators.OfType<LowerArmActuator>().ToList());
    }

    private static void SetLegActuators(IUnit attacker, PartLocation leg, params Component[] actuators)
    {
        attacker.GetComponentsAtLocation<UpperLegActuator>(leg)
            .Returns(actuators.OfType<UpperLegActuator>().ToList());
        attacker.GetComponentsAtLocation<LowerLegActuator>(leg)
            .Returns(actuators.OfType<LowerLegActuator>().ToList());
    }

    #endregion

    #region Hit locations

    [Theory]
    [InlineData(1, PartLocation.LeftArm)]
    [InlineData(2, PartLocation.LeftTorso)]
    [InlineData(3, PartLocation.CenterTorso)]
    [InlineData(4, PartLocation.RightTorso)]
    [InlineData(5, PartLocation.RightArm)]
    [InlineData(6, PartLocation.Head)]
    public void ResolveAttack_WithPunchFromTheFront_ShouldUseTheFrontColumnOfThePunchTable(
        int locationRoll, PartLocation expectedLocation)
    {
        var attacker = CreateUnit(50, AttackerPositionInArc(FiringArc.Front));
        var target = CreateUnit(20, TargetPosition());
        SetHitRoll(5, 5);
        SetLocationRoll(locationRoll);

        var result = _sut.ResolveAttack(
            CreateContext(attacker, target, PhysicalAttackType.Punch, PartLocation.LeftArm));

        result.AttackResolution.IsHit.ShouldBeTrue();
        result.AttackResolution.AttackDirection.ShouldBe(HitDirection.Front);
        GetInitialLocation(result).ShouldBe(expectedLocation);
    }

    [Theory]
    [InlineData(1, PartLocation.LeftTorso)]
    [InlineData(2, PartLocation.LeftTorso)]
    [InlineData(3, PartLocation.CenterTorso)]
    [InlineData(4, PartLocation.LeftArm)]
    [InlineData(5, PartLocation.LeftArm)]
    [InlineData(6, PartLocation.Head)]
    public void ResolveAttack_WithPunchFromTheLeftSide_ShouldUseTheLeftColumnOfThePunchTable(
        int locationRoll, PartLocation expectedLocation)
    {
        var attacker = CreateUnit(50, AttackerPositionInArc(FiringArc.Left));
        var target = CreateUnit(20, TargetPosition());
        SetHitRoll(5, 5);
        SetLocationRoll(locationRoll);

        var result = _sut.ResolveAttack(
            CreateContext(attacker, target, PhysicalAttackType.Punch, PartLocation.LeftArm));

        result.AttackResolution.IsHit.ShouldBeTrue();
        result.AttackResolution.AttackDirection.ShouldBe(HitDirection.Left);
        GetInitialLocation(result).ShouldBe(expectedLocation);
    }

    [Theory]
    [InlineData(1, PartLocation.RightTorso)]
    [InlineData(2, PartLocation.RightTorso)]
    [InlineData(3, PartLocation.CenterTorso)]
    [InlineData(4, PartLocation.RightArm)]
    [InlineData(5, PartLocation.RightArm)]
    [InlineData(6, PartLocation.Head)]
    public void ResolveAttack_WithPunchFromTheRightSide_ShouldUseTheRightColumnOfThePunchTable(
        int locationRoll, PartLocation expectedLocation)
    {
        var attacker = CreateUnit(50, AttackerPositionInArc(FiringArc.Right));
        var target = CreateUnit(20, TargetPosition());
        SetHitRoll(5, 5);
        SetLocationRoll(locationRoll);

        var result = _sut.ResolveAttack(
            CreateContext(attacker, target, PhysicalAttackType.Punch, PartLocation.RightArm));

        result.AttackResolution.IsHit.ShouldBeTrue();
        result.AttackResolution.AttackDirection.ShouldBe(HitDirection.Right);
        GetInitialLocation(result).ShouldBe(expectedLocation);
    }

    [Theory]
    [InlineData(1, PartLocation.RightLeg)]
    [InlineData(3, PartLocation.RightLeg)]
    [InlineData(4, PartLocation.LeftLeg)]
    [InlineData(6, PartLocation.LeftLeg)]
    public void ResolveAttack_WithKickFromTheFront_ShouldUseTheFrontColumnOfTheKickTable(
        int locationRoll, PartLocation expectedLocation)
    {
        var attacker = CreateUnit(50, AttackerPositionInArc(FiringArc.Front));
        var target = CreateUnit(20, TargetPosition());
        SetHitRoll(5, 5);
        SetLocationRoll(locationRoll);

        var result = _sut.ResolveAttack(
            CreateContext(attacker, target, PhysicalAttackType.Kick, PartLocation.RightLeg));

        result.AttackResolution.IsHit.ShouldBeTrue();
        result.AttackResolution.AttackDirection.ShouldBe(HitDirection.Front);
        GetInitialLocation(result).ShouldBe(expectedLocation);
    }

    [Theory]
    [InlineData(FiringArc.Left, PartLocation.LeftLeg)]
    [InlineData(FiringArc.Right, PartLocation.RightLeg)]
    public void ResolveAttack_WithKickFromTheSide_ShouldHitTheLegOnThatSide(
        FiringArc arc, PartLocation expectedLocation)
    {
        var attacker = CreateUnit(50, AttackerPositionInArc(arc));
        var target = CreateUnit(20, TargetPosition());
        SetHitRoll(5, 5);
        SetLocationRoll(2);

        var result = _sut.ResolveAttack(
            CreateContext(attacker, target, PhysicalAttackType.Kick, PartLocation.RightLeg));

        result.AttackResolution.IsHit.ShouldBeTrue();
        GetInitialLocation(result).ShouldBe(expectedLocation);
    }

    #endregion

    #region Misses

    [Fact]
    public void ResolveAttack_WhenTheRollIsBelowTheToHitNumber_ShouldMissWithoutDamage()
    {
        var attacker = CreateUnit(50, AttackerPositionInArc(FiringArc.Front));
        var target = CreateUnit(20, TargetPosition());
        SetHitRoll(2, 2);

        var result = _sut.ResolveAttack(
            CreateContext(attacker, target, PhysicalAttackType.Punch, PartLocation.LeftArm));

        result.AttackResolution.IsHit.ShouldBeFalse();
        result.AttackResolution.HitLocationsData.ShouldBeNull();
        result.RequiresTargetPsr.ShouldBeFalse();
        result.RequiresAttackerPsr.ShouldBeFalse();

        _damageTransferCalculator
            .DidNotReceive()
            .CalculateStructureDamage(
                Arg.Any<IUnit>(),
                Arg.Any<PartLocation>(),
                Arg.Any<int>(),
                Arg.Any<HitDirection>(),
                Arg.Any<IReadOnlyList<LocationHitData>?>());
    }

    [Fact]
    public void ResolveAttack_WhenAKickMisses_ShouldRequireAnAttackerPsr()
    {
        var attacker = CreateUnit(50, AttackerPositionInArc(FiringArc.Front));
        var target = CreateUnit(20, TargetPosition());
        SetHitRoll(2, 2);

        var result = _sut.ResolveAttack(
            CreateContext(attacker, target, PhysicalAttackType.Kick, PartLocation.RightLeg));

        result.AttackResolution.IsHit.ShouldBeFalse();
        result.AttackResolution.HitLocationsData.ShouldBeNull();
        result.RequiresTargetPsr.ShouldBeFalse();
        result.RequiresAttackerPsr.ShouldBeTrue();
    }

    [Fact]
    public void ResolveAttack_WhenAKickHits_ShouldRequireATargetPsr()
    {
        var attacker = CreateUnit(50, AttackerPositionInArc(FiringArc.Front));
        var target = CreateUnit(20, TargetPosition());
        SetHitRoll(5, 5);
        SetLocationRoll(1);

        var result = _sut.ResolveAttack(
            CreateContext(attacker, target, PhysicalAttackType.Kick, PartLocation.RightLeg));

        result.AttackResolution.IsHit.ShouldBeTrue();
        result.RequiresTargetPsr.ShouldBeTrue();
        result.RequiresAttackerPsr.ShouldBeFalse();
    }

    #endregion

    #region Independent arms

    [Fact]
    public void ResolveAttack_WithTwoArms_ShouldRollEachArmIndependently()
    {
        var attacker = CreateUnit(50, AttackerPositionInArc(FiringArc.Front));
        var target = CreateUnit(20, TargetPosition());

        _diceRoller.Roll2D6().Returns(
            [new DiceResult(5), new DiceResult(5)],
            [new DiceResult(4), new DiceResult(4)]);
        _diceRoller.RollD6().Returns(
            new DiceResult(1),
            new DiceResult(4));

        var leftArmResult = _sut.ResolveAttack(
            CreateContext(attacker, target, PhysicalAttackType.Punch, PartLocation.LeftArm));
        var rightArmResult = _sut.ResolveAttack(
            CreateContext(attacker, target, PhysicalAttackType.Punch, PartLocation.RightArm));

        leftArmResult.AttackerLimb.ShouldBe(PartLocation.LeftArm);
        leftArmResult.AttackResolution.IsHit.ShouldBeTrue();
        GetInitialLocation(leftArmResult).ShouldBe(PartLocation.LeftArm);

        rightArmResult.AttackerLimb.ShouldBe(PartLocation.RightArm);
        rightArmResult.AttackResolution.IsHit.ShouldBeTrue();
        GetInitialLocation(rightArmResult).ShouldBe(PartLocation.RightTorso);

        _diceRoller.Received(2).Roll2D6();
        _diceRoller.Received(2).RollD6();
    }

    [Fact]
    public void ResolveAttack_WithTwoArms_ShouldLetOneArmHitWhileTheOtherMisses()
    {
        var attacker = CreateUnit(50, AttackerPositionInArc(FiringArc.Front));
        var target = CreateUnit(20, TargetPosition());

        _diceRoller.Roll2D6().Returns(
            [new DiceResult(5), new DiceResult(5)],
            [new DiceResult(2), new DiceResult(2)]);
        _diceRoller.RollD6().Returns(new DiceResult(1));

        var leftArmResult = _sut.ResolveAttack(
            CreateContext(attacker, target, PhysicalAttackType.Punch, PartLocation.LeftArm));
        var rightArmResult = _sut.ResolveAttack(
            CreateContext(attacker, target, PhysicalAttackType.Punch, PartLocation.RightArm));

        leftArmResult.AttackResolution.IsHit.ShouldBeTrue();
        leftArmResult.AttackerLimb.ShouldBe(PartLocation.LeftArm);

        rightArmResult.AttackResolution.IsHit.ShouldBeFalse();
        rightArmResult.AttackerLimb.ShouldBe(PartLocation.RightArm);
        rightArmResult.AttackResolution.HitLocationsData.ShouldBeNull();
    }

    #endregion

    #region Validation

    [Fact]
    public void ResolveAttack_WithANullMap_ShouldThrow()
    {
        var attacker = CreateUnit(50, AttackerPositionInArc(FiringArc.Front));
        var target = CreateUnit(20, TargetPosition());
        var context = CreateContext(attacker, target, PhysicalAttackType.Punch, PartLocation.LeftArm) with { Map = null! };

        Should.Throw<ArgumentNullException>(() => _sut.ResolveAttack(context));
    }

    [Theory]
    [InlineData(PhysicalAttackType.Push)]
    [InlineData(PhysicalAttackType.Charge)]
    [InlineData(PhysicalAttackType.DFA)]
    public void ResolveAttack_WithAnUnsupportedAttackType_ShouldThrow(PhysicalAttackType attackType)
    {
        var attacker = CreateUnit(50, AttackerPositionInArc(FiringArc.Front));
        var target = CreateUnit(20, TargetPosition());

        Should.Throw<NotSupportedException>(() => _sut.ResolveAttack(
            CreateContext(attacker, target, attackType, PartLocation.LeftArm)));
    }

    [Fact]
    public void ResolveAttack_WithAPunchThatUsesALeg_ShouldThrow()
    {
        var attacker = CreateUnit(50, AttackerPositionInArc(FiringArc.Front));
        var target = CreateUnit(20, TargetPosition());

        Should.Throw<ArgumentException>(() => _sut.ResolveAttack(
            CreateContext(attacker, target, PhysicalAttackType.Punch, PartLocation.LeftLeg)));
    }

    [Fact]
    public void ResolveAttack_WithAKickThatUsesAnArm_ShouldThrow()
    {
        var attacker = CreateUnit(50, AttackerPositionInArc(FiringArc.Front));
        var target = CreateUnit(20, TargetPosition());

        Should.Throw<ArgumentException>(() => _sut.ResolveAttack(
            CreateContext(attacker, target, PhysicalAttackType.Kick, PartLocation.RightArm)));
    }

    [Fact]
    public void ResolveAttack_WithoutAPosition_ShouldThrow()
    {
        var attacker = CreateUnit(50, null!);
        var target = CreateUnit(20, TargetPosition());

        Should.Throw<InvalidOperationException>(() => _sut.ResolveAttack(
            CreateContext(attacker, target, PhysicalAttackType.Punch, PartLocation.LeftArm)));
    }

    [Fact]
    public void ResolveAttack_WithoutATargetPosition_ShouldThrow()
    {
        var attacker = CreateUnit(50, AttackerPositionInArc(FiringArc.Front));
        var target = CreateUnit(20, null!);

        Should.Throw<InvalidOperationException>(() => _sut.ResolveAttack(
            CreateContext(attacker, target, PhysicalAttackType.Punch, PartLocation.LeftArm)));
    }

    #endregion

    #region Damage pipeline compatibility

    [Fact]
    public void ResolveAttack_ShouldNotMutateTheTarget()
    {
        var (_, _, resolver) = CreateRealPipeline();
        var target = CreateTargetUnit();
        var attacker = CreateAttackerUnit(100);
        SetHitRoll(5, 5);
        SetLocationRoll(3);

        var armorBefore = target.TotalCurrentArmor;
        var structureBefore = target.TotalCurrentStructure;

        var result = resolver.ResolveAttack(
            CreateContext(attacker, target, PhysicalAttackType.Punch, PartLocation.LeftArm));

        result.AttackResolution.IsHit.ShouldBeTrue();
        target.TotalCurrentArmor.ShouldBe(armorBefore);
        target.TotalCurrentStructure.ShouldBe(structureBefore);
        target.Parts[PartLocation.CenterTorso].CurrentArmor.ShouldBe(5);
        target.Parts[PartLocation.CenterTorso].CurrentStructure.ShouldBe(6);
    }

    [Fact]
    public void ResolveAttack_ShouldConsumeArmorBeforeInternalStructure()
    {
        var (_, _, resolver) = CreateRealPipeline();
        var target = CreateTargetUnit();
        var attacker = CreateAttackerUnit(100);
        SetHitRoll(5, 5);
        SetLocationRoll(3);

        var result = resolver.ResolveAttack(
            CreateContext(attacker, target, PhysicalAttackType.Punch, PartLocation.LeftArm));

        result.Damage.ShouldBe(10);
        var hitLocation = result.AttackResolution.HitLocationsData!.HitLocations.ShouldHaveSingleItem();
        var damageData = hitLocation.Damage.ShouldHaveSingleItem();
        damageData.Location.ShouldBe(PartLocation.CenterTorso);
        damageData.ArmorDamage.ShouldBe(5);
        damageData.StructureDamage.ShouldBe(5);
        damageData.IsLocationDestroyed.ShouldBeFalse();
    }

    [Fact]
    public void ResolveAttack_ShouldTransferToTheSideTorso_WhenThePunchedArmIsAlreadyDestroyed()
    {
        var (_, _, resolver) = CreateRealPipeline();
        var target = CreateTargetUnit();
        var attacker = CreateAttackerUnit(100);
        SetHitRoll(5, 5);
        SetLocationRoll(1);

        var arm = target.Parts[PartLocation.LeftArm];
        arm.ApplyDamage(arm.CurrentArmor + arm.CurrentStructure, HitDirection.Front);
        arm.IsDestroyed.ShouldBeTrue();

        var result = resolver.ResolveAttack(
            CreateContext(attacker, target, PhysicalAttackType.Punch, PartLocation.LeftArm));

        var hitLocation = result.AttackResolution.HitLocationsData!.HitLocations.ShouldHaveSingleItem();
        hitLocation.InitialLocation.ShouldBe(PartLocation.LeftArm);
        var damageData = hitLocation.Damage.ShouldHaveSingleItem();
        damageData.Location.ShouldBe(PartLocation.LeftTorso);
    }

    [Fact]
    public void ResolveAttack_ShouldReportADestroyedLocation()
    {
        var (_, _, resolver) = CreateRealPipeline();
        var target = CreateTargetUnit();
        var attacker = CreateAttackerUnit(100);
        SetHitRoll(5, 5);
        SetLocationRoll(1);

        var result = resolver.ResolveAttack(
            CreateContext(attacker, target, PhysicalAttackType.Punch, PartLocation.LeftArm));

        var hitLocation = result.AttackResolution.HitLocationsData!.HitLocations.ShouldHaveSingleItem();
        var damageData = hitLocation.Damage;
        damageData[0].Location.ShouldBe(PartLocation.LeftArm);
        damageData[0].ArmorDamage.ShouldBe(4);
        damageData[0].StructureDamage.ShouldBe(3);
        damageData[0].IsLocationDestroyed.ShouldBeTrue();
        damageData[1].Location.ShouldBe(PartLocation.LeftTorso);
    }

    [Fact]
    public void ResolveAttack_ShouldProduceCriticalHitsInTheStandardPipeline()
    {
        var (mechFactory, damageTransferCalculator, resolver) = CreateRealPipeline();
        var target = CreateTargetUnit();
        // A 55 ton attacker punches for 6 points, which damages the arm without blowing it off.
        var attacker = CreateAttackerUnit(55);
        SetHitRoll(5, 5);
        SetLocationRoll(1);

        var result = resolver.ResolveAttack(
            CreateContext(attacker, target, PhysicalAttackType.Punch, PartLocation.LeftArm));

        var hitLocations = result.AttackResolution.HitLocationsData!.HitLocations;
        var damageData = hitLocations.SelectMany(hit => hit.Damage).ToList();
        damageData[0].ArmorDamage.ShouldBe(4);
        damageData[0].StructureDamage.ShouldBe(2);

        target.ApplyDamage(hitLocations, result.AttackResolution.AttackDirection);
        target.Parts[PartLocation.LeftArm].CurrentArmor.ShouldBe(0);
        target.Parts[PartLocation.LeftArm].CurrentStructure.ShouldBe(1);

        var criticalDiceRoller = Substitute.For<IDiceRoller>();
        criticalDiceRoller.Roll2D6().Returns([new DiceResult(5), new DiceResult(5)]);
        var criticalHitsCalculator = new CriticalHitsCalculator(
            criticalDiceRoller,
            damageTransferCalculator,
            mechFactory);

        var criticalHits = criticalHitsCalculator.CalculateCriticalHits(target, damageData);

        criticalHits.ShouldNotBeNull();
        criticalHits.CriticalHits.ShouldNotBeEmpty();
        criticalHits.CriticalHits[0].Location.ShouldBe(PartLocation.LeftArm);
        criticalHits.CriticalHits[0].NumCriticalHits.ShouldBe(2);
    }

    private Unit CreateTargetUnit()
    {
        var (mechFactory, _, _) = CreateRealPipeline();
        var target = mechFactory.Create(CreateTargetMechData());
        target.Deploy(TargetPosition(), null);
        return target;
    }

    private Unit CreateAttackerUnit(int mass)
    {
        var (mechFactory, _, _) = CreateRealPipeline();
        var attacker = mechFactory.Create(CreateAttackerMechData(mass));
        attacker.Deploy(AttackerPositionInArc(FiringArc.Front), null);
        return attacker;
    }

    #endregion

    private static PartLocation GetInitialLocation(PhysicalAttackResolutionData result) =>
        result.AttackResolution.HitLocationsData!.HitLocations.ShouldHaveSingleItem().InitialLocation;
}
