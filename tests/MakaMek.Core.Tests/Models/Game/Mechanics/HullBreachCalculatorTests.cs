using NSubstitute;
using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Units.Components;
using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Models.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components.Engines;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Core.Tests.Utils;
using Sanet.MakaMek.Core.Utils;
using Sanet.MakaMek.Localization;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Models.Terrains;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Mechanics;

public class HullBreachCalculatorTests
{
    private readonly IDiceRoller _mockDiceRoller = Substitute.For<IDiceRoller>();
    private readonly HullBreachCalculator _sut;
    private readonly MechFactory _mechFactory;

    public HullBreachCalculatorTests()
    {
        _sut = new HullBreachCalculator(_mockDiceRoller);

        var rules = new TotalWarfareRulesProvider();
        var localizationService = Substitute.For<ILocalizationService>();
        _mechFactory = new MechFactory(rules, new ClassicBattletechComponentProvider(), localizationService);
    }

    private Mech CreateTestMech()
    {
        return CreateTestMech(true);
    }

    private Mech CreateTestMech(bool withWater)
    {
        var mechData = MechFactoryTests.CreateDummyMechData();
        var mech = _mechFactory.Create(mechData);
        var hex = new Hex(new HexCoordinates(1, 1));
        if (withWater)
        {
            hex.AddTerrain(new WaterTerrain(-2));
        }
        else
        {
            hex.AddTerrain(new ClearTerrain());
        }
        mech.Deploy(new HexPosition(new HexCoordinates(1, 1), HexDirection.Top), hex);
        return mech;
    }

    private static LocationDamageData CreateLocationDamageData(PartLocation location, int armorDamage,
        int structureDamage)
    {
        return new LocationDamageData(location, armorDamage, structureDamage, false);
    }

    private Mech CreateTestMechWithXlEngine()
    {
        var equipment = new List<ComponentData>
        {
            new()
            {
                Type = MakaMekComponent.Engine,
                Assignments =
                [
                    new LocationSlotAssignment(PartLocation.CenterTorso, 7, 4),
                    new LocationSlotAssignment(PartLocation.LeftTorso, 2, 3),
                    new LocationSlotAssignment(PartLocation.RightTorso, 2, 3)
                ],
                SpecificData = new EngineStateData(EngineType.XLFusion, 160)
            }
        };
        var mechData = MechFactoryTests.CreateDummyMechData(equipment, true);
        var mech = _mechFactory.Create(mechData);
        var hex = new Hex(new HexCoordinates(1, 1));
        hex.AddTerrain(new WaterTerrain(-2));
        mech.Deploy(new HexPosition(new HexCoordinates(1, 1), HexDirection.Top), hex);
        return mech;
    }

    [Fact]
    public void CalculateAndApplyHullBreach_ShouldReturnNull_WhenNoDamagedLocations()
    {
        var testUnit = CreateTestMech();
        var result = _sut.CalculateAndApplyHullBreach(testUnit, []);
        result.ShouldBeNull();
    }

    [Fact]
    public void CalculateAndApplyHullBreach_ShouldReturnNull_WhenUnitIsNotSubmerged()
    {
        var testUnit = CreateTestMech(false);

        var result = _sut.CalculateAndApplyHullBreach(testUnit,
            [CreateLocationDamageData(PartLocation.CenterTorso, 3, 0)]);

        result.ShouldBeNull();
    }

    [Fact]
    public void CalculateAndApplyHullBreach_ShouldReturnCommand_WhenUnitIsSubmergedAndLocationDamaged()
    {
        var testUnit = CreateTestMech();
        _mockDiceRoller.Roll2D6().Returns([new DiceResult(5), new DiceResult(6)]);

        var result = _sut.CalculateAndApplyHullBreach(testUnit,
            [CreateLocationDamageData(PartLocation.CenterTorso, 3, 0)]);

        result.ShouldNotBeNull();
        result.UnitId.ShouldBe(testUnit.Id);
        result.BreachedLocations.Count.ShouldBe(1);
        result.BreachedLocations[0].Location.ShouldBe(PartLocation.CenterTorso);
        result.BreachedLocations[0].IsAutomatic.ShouldBeFalse();
        result.BreachedLocations[0].BreachRoll.ShouldBe([5, 6]);
    }

    [Fact]
    public void CalculateAndApplyHullBreach_ShouldApplyBreach_OnUnit()
    {
        var testUnit = CreateTestMech();
        _mockDiceRoller.Roll2D6().Returns([new DiceResult(5), new DiceResult(6)]);

        _sut.CalculateAndApplyHullBreach(testUnit,
            [CreateLocationDamageData(PartLocation.CenterTorso, 3, 0)]);

        var centerTorso = testUnit.Parts[PartLocation.CenterTorso];
        centerTorso.IsBreached.ShouldBeTrue();
    }

    [Fact]
    public void CalculateAndApplyHullBreach_ShouldAutomaticBreach_WhenZeroArmor()
    {
        var testUnit = CreateTestMech();
        var centerTorso = testUnit.Parts[PartLocation.CenterTorso];
        centerTorso.ApplyDamage(centerTorso.CurrentArmor, HitDirection.Front);
        centerTorso.CurrentArmor.ShouldBe(0);

        var result = _sut.CalculateAndApplyHullBreach(testUnit,
            [CreateLocationDamageData(PartLocation.CenterTorso, 1, 0)]);

        result.ShouldNotBeNull();
        result.BreachedLocations[0].IsAutomatic.ShouldBeTrue();
        result.BreachedLocations[0].BreachRoll.ShouldBeNull();
        _mockDiceRoller.DidNotReceive().Roll2D6();
    }

    [Fact]
    public void CalculateAndApplyHullBreach_ShouldNotBreach_WhenRollBelowThreshold()
    {
        var testUnit = CreateTestMech();
        _mockDiceRoller.Roll2D6().Returns([new DiceResult(2), new DiceResult(2)]);

        var result = _sut.CalculateAndApplyHullBreach(testUnit,
            [CreateLocationDamageData(PartLocation.CenterTorso, 3, 0)]);

        result.ShouldBeNull();
    }

    [Fact]
    public void CalculateAndApplyHullBreach_ShouldSkipAlreadyBreachedLocation()
    {
        var testUnit = CreateTestMech();
        var centerTorso = testUnit.Parts[PartLocation.CenterTorso];
        centerTorso.ApplyBreach();

        _mockDiceRoller.Roll2D6().Returns([new DiceResult(5), new DiceResult(6)]);

        var result = _sut.CalculateAndApplyHullBreach(testUnit,
            [CreateLocationDamageData(PartLocation.CenterTorso, 3, 0)]);

        result.ShouldBeNull();
    }

    [Fact]
    public void CalculateAndApplyHullBreach_ShouldHandleMultipleDamagedLocations()
    {
        var testUnit = CreateTestMech();
        _mockDiceRoller.Roll2D6().Returns(
            [new DiceResult(5), new DiceResult(6)],
            [new DiceResult(6), new DiceResult(5)]);

        var result = _sut.CalculateAndApplyHullBreach(testUnit,
        [
            CreateLocationDamageData(PartLocation.CenterTorso, 3, 0),
            CreateLocationDamageData(PartLocation.LeftArm, 2, 0)
        ]);

        result.ShouldNotBeNull();
        result.BreachedLocations.Count.ShouldBe(2);
        result.BreachedLocations.ShouldContain(b => b.Location == PartLocation.CenterTorso);
        result.BreachedLocations.ShouldContain(b => b.Location == PartLocation.LeftArm);
    }

    [Fact]
    public void CalculateAndApplyHullBreach_ShouldReturnNull_WhenRollBelowThresholdForAllLocations()
    {
        var testUnit = CreateTestMech();
        _mockDiceRoller.Roll2D6().Returns(
            [new DiceResult(2), new DiceResult(2)],
            [new DiceResult(1), new DiceResult(3)]);

        var result = _sut.CalculateAndApplyHullBreach(testUnit,
        [
            CreateLocationDamageData(PartLocation.CenterTorso, 3, 0),
            CreateLocationDamageData(PartLocation.LeftArm, 2, 0)
        ]);

        result.ShouldBeNull();
    }

    [Fact]
    public void CalculateAndApplyHullBreach_ShouldIncludeFloodedComponents_WhenPartHasComponents()
    {
        var testUnit = CreateTestMech();
        _mockDiceRoller.Roll2D6().Returns([new DiceResult(5), new DiceResult(6)]);

        var result = _sut.CalculateAndApplyHullBreach(testUnit,
            [CreateLocationDamageData(PartLocation.CenterTorso, 3, 0)]);

        result.ShouldNotBeNull();
        var floodedComponents = result.BreachedLocations[0].FloodedComponents;
        floodedComponents.ShouldNotBeNull();
        floodedComponents.Length.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void CalculateAndApplyHullBreach_ShouldApplyBreach_WhenLegLocationDamaged()
    {
        var testUnit = CreateTestMech();
        _mockDiceRoller.Roll2D6().Returns([new DiceResult(5), new DiceResult(6)]);

        var result = _sut.CalculateAndApplyHullBreach(testUnit,
            [CreateLocationDamageData(PartLocation.LeftLeg, 3, 0)]);

        result.ShouldNotBeNull();
        result.BreachedLocations.Count.ShouldBe(1);
        result.BreachedLocations[0].Location.ShouldBe(PartLocation.LeftLeg);
    }

    [Fact]
    public void CalculateAndApplyHullBreach_ShouldUsePerLocationEngineSlotCount_ForSplitEngine()
    {
        var testUnit = CreateTestMechWithXlEngine();
        _mockDiceRoller.Roll2D6().Returns([new DiceResult(5), new DiceResult(6)]);

        var result = _sut.CalculateAndApplyHullBreach(testUnit,
            [CreateLocationDamageData(PartLocation.LeftTorso, 3, 0)]);

        result.ShouldNotBeNull();
        result.BreachedLocations.Count.ShouldBe(1);
        result.BreachedLocations[0].EngineHitsApplied.ShouldBe(3);
    }
}
