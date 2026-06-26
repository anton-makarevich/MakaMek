using NSubstitute;
using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Movement.Actions;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Core.Tests.Models.Game.Phases;
using Sanet.MakaMek.Core.Utils;
using Sanet.MakaMek.Core.Tests.Utils;
using Sanet.MakaMek.Localization;
using Sanet.MakaMek.Map.Models;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Mechanics.Movement.Actions;

public class ApplySkidActionTests : GamePhaseTestsBase
{
    private readonly Mech _mech;
    private readonly Guid _unitId = Guid.NewGuid();

    protected override void SetupSut()
    {
    }

    public ApplySkidActionTests()
    {
        var mechFactory = new MechFactory(
            new TotalWarfareRulesProvider(),
            new ClassicBattletechComponentProvider(),
            Substitute.For<ILocalizationService>());
        var mechData = MechFactoryTests.CreateDummyMechData() with { Id = _unitId };
        _mech = mechFactory.Create(mechData);
    }

    [Fact]
    public void Process_ShouldReturnCommandWithSkidCommand()
    {
        var command = new MechSkidCommand
        {
            UnitId = _unitId,
            SkidDistance = 2,
            DamageData = null,
            GameOriginId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow
        };
        var sut = new ApplySkidAction(_mech, command);

        var result = sut.Process(Game);

        result.ShouldNotBeNull();
        result.Count.ShouldBe(1);
        result[0].ShouldBe(command);
    }

    [Fact]
    public void Process_WhenNoDamageData_ShouldNotCalculateCriticalHits()
    {
        var command = new MechSkidCommand
        {
            UnitId = _unitId,
            SkidDistance = 1,
            DamageData = null,
            GameOriginId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow
        };
        var sut = new ApplySkidAction(_mech, command);

        var result = sut.Process(Game);

        MockCriticalHitsCalculator.DidNotReceive().CalculateAndApplyCriticalHits(Arg.Any<IUnit>(), Arg.Any<List<LocationDamageData>>());
        result.Count.ShouldBe(1);
    }

    [Fact]
    public void Process_WhenDamageDataWithoutStructureDamage_ShouldNotCalculateCriticalHits()
    {
        var damageData = new FallingDamageData(
            HexDirection.Top,
            new HitLocationsData(
                [new LocationHitData(
                    [new LocationDamageData(PartLocation.CenterTorso, 5, 0, false)],
                    [],
                    [],
                    PartLocation.CenterTorso)],
                5),
            new DiceResult(3),
            HitDirection.Front);
        var command = new MechSkidCommand
        {
            UnitId = _unitId,
            SkidDistance = 1,
            DamageData = damageData,
            GameOriginId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow
        };
        var sut = new ApplySkidAction(_mech, command);

        var result = sut.Process(Game);

        MockCriticalHitsCalculator.DidNotReceive().CalculateAndApplyCriticalHits(Arg.Any<IUnit>(), Arg.Any<List<LocationDamageData>>());
        result.Count.ShouldBe(1);
    }

    [Fact]
    public void Process_WhenStructureDamageExistsAndCritCalculatorReturnsCommand_ShouldAddCritCommand()
    {
        var damageData = new FallingDamageData(
            HexDirection.Top,
            new HitLocationsData(
                [new LocationHitData(
                    [new LocationDamageData(PartLocation.CenterTorso, 0, 3, false)],
                    [],
                    [],
                    PartLocation.CenterTorso)],
                3),
            new DiceResult(4),
            HitDirection.Front);
        var command = new MechSkidCommand
        {
            UnitId = _unitId,
            SkidDistance = 1,
            DamageData = damageData,
            GameOriginId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow
        };
        var critCommand = new CriticalHitsResolutionCommand
        {
            GameOriginId = Guid.NewGuid(),
            TargetId = _unitId,
            CriticalHits = []
        };
        MockCriticalHitsCalculator.CalculateAndApplyCriticalHits(_mech, Arg.Any<List<LocationDamageData>>())
            .Returns(critCommand);
        var sut = new ApplySkidAction(_mech, command);

        var result = sut.Process(Game);

        result.Count.ShouldBe(2);
        result[0].ShouldBe(command);
        result[1].ShouldBe(critCommand);
        critCommand.GameOriginId.ShouldBe(Game.Id);
    }

    [Fact]
    public void Process_WhenStructureDamageExistsAndCritCalculatorReturnsNull_ShouldNotAddCritCommand()
    {
        var damageData = new FallingDamageData(
            HexDirection.Top,
            new HitLocationsData(
                [new LocationHitData(
                    [new LocationDamageData(PartLocation.CenterTorso, 0, 3, false)],
                    [],
                    [],
                    PartLocation.CenterTorso)],
                3),
            new DiceResult(5),
            HitDirection.Front);
        var command = new MechSkidCommand
        {
            UnitId = _unitId,
            SkidDistance = 1,
            DamageData = damageData,
            GameOriginId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow
        };
        MockCriticalHitsCalculator.CalculateAndApplyCriticalHits(_mech, Arg.Any<List<LocationDamageData>>())
            .Returns((CriticalHitsResolutionCommand?)null);
        var sut = new ApplySkidAction(_mech, command);

        var result = sut.Process(Game);

        result.Count.ShouldBe(1);
        result[0].ShouldBe(command);
    }

    [Fact]
    public void Process_WithMultipleLocationsHavingStructureDamage_ShouldPassAllToCritCalculator()
    {
        var damageData = new FallingDamageData(
            HexDirection.Top,
            new HitLocationsData(
            [
                new LocationHitData(
                    [new LocationDamageData(PartLocation.CenterTorso, 0, 3, false)],
                    [],
                    [],
                    PartLocation.CenterTorso),
                new LocationHitData(
                    [new LocationDamageData(PartLocation.LeftTorso, 0, 2, false)],
                    [],
                    [],
                    PartLocation.LeftTorso)
            ],
                5),
            new DiceResult(6),
            HitDirection.Front);
        var command = new MechSkidCommand
        {
            UnitId = _unitId,
            SkidDistance = 2,
            DamageData = damageData,
            GameOriginId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow
        };
        MockCriticalHitsCalculator.CalculateAndApplyCriticalHits(_mech, Arg.Any<List<LocationDamageData>>())
            .Returns((CriticalHitsResolutionCommand?)null);
        var sut = new ApplySkidAction(_mech, command);

        sut.Process(Game);

        MockCriticalHitsCalculator.Received(1).CalculateAndApplyCriticalHits(
            _mech,
            Arg.Is<List<LocationDamageData>>(l => l.Count == 2));
    }

    [Fact]
    public void Process_WhenDamageDataExists_ShouldCallHullBreachCalculator()
    {
        var damageData = new FallingDamageData(
            HexDirection.Top,
            new HitLocationsData(
                [new LocationHitData(
                    [new LocationDamageData(PartLocation.CenterTorso, 5, 0, false)],
                    [],
                    [],
                    PartLocation.CenterTorso)],
                5),
            new DiceResult(3),
            HitDirection.Front);
        var command = new MechSkidCommand
        {
            UnitId = _unitId,
            SkidDistance = 1,
            DamageData = damageData,
            GameOriginId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow
        };
        var sut = new ApplySkidAction(_mech, command);

        sut.Process(Game);

        Game.HullBreachCalculator.Received(1).CalculateAndApplyHullBreach(
            _mech,
            Arg.Is<List<LocationDamageData>>(l => l.Count == 1));
    }

    [Fact]
    public void Process_WhenHullBreachCalculatorReturnsCommand_ShouldAddHullBreachCommandWithGameOriginId()
    {
        var damageData = new FallingDamageData(
            HexDirection.Top,
            new HitLocationsData(
                [new LocationHitData(
                    [new LocationDamageData(PartLocation.CenterTorso, 5, 0, false)],
                    [],
                    [],
                    PartLocation.CenterTorso)],
                5),
            new DiceResult(3),
            HitDirection.Front);
        var command = new MechSkidCommand
        {
            UnitId = _unitId,
            SkidDistance = 1,
            DamageData = damageData,
            GameOriginId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow
        };
        var hullBreachCommand = new HullBreachCommand
        {
            GameOriginId = Guid.NewGuid(),
            UnitId = _unitId,
            BreachedLocations = []
        };
        Game.HullBreachCalculator.CalculateAndApplyHullBreach(_mech, Arg.Any<List<LocationDamageData>>())
            .Returns(hullBreachCommand);
        var sut = new ApplySkidAction(_mech, command);

        var result = sut.Process(Game);

        result.Count.ShouldBe(2);
        result[0].ShouldBe(command);
        result[1].ShouldBe(hullBreachCommand);
        hullBreachCommand.GameOriginId.ShouldBe(Game.Id);
    }

    [Fact]
    public void Process_WhenDamageDataExistsAndHullBreachReturnsNull_ShouldNotAddBreachCommand()
    {
        var damageData = new FallingDamageData(
            HexDirection.Top,
            new HitLocationsData(
                [new LocationHitData(
                    [new LocationDamageData(PartLocation.CenterTorso, 5, 0, false)],
                    [],
                    [],
                    PartLocation.CenterTorso)],
                5),
            new DiceResult(3),
            HitDirection.Front);
        var command = new MechSkidCommand
        {
            UnitId = _unitId,
            SkidDistance = 1,
            DamageData = damageData,
            GameOriginId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow
        };
        Game.HullBreachCalculator.CalculateAndApplyHullBreach(_mech, Arg.Any<List<LocationDamageData>>())
            .Returns((HullBreachCommand?)null);
        var sut = new ApplySkidAction(_mech, command);

        var result = sut.Process(Game);

        result.Count.ShouldBe(1);
        result[0].ShouldBe(command);
    }
}
