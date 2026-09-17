using NSubstitute;
using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Models.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Movement.Actions;
using Sanet.MakaMek.Core.Models.Game.Mechanics.WeaponAttack;
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

public class ApplyFallActionTests : GamePhaseTestsBase
{
    private readonly Mech _mech;
    private readonly Guid _unitId = Guid.NewGuid();

    protected override void SetupSut()
    {
    }

    public ApplyFallActionTests()
    {
        var mechFactory = new MechFactory(
            new TotalWarfareRulesProvider(),
            new ClassicBattletechComponentProvider(),
            Substitute.For<ILocalizationService>());
        var mechData = MechFactoryTests.CreateDummyMechData() with { Id = _unitId };
        _mech = mechFactory.Create(mechData);
    }

    [Fact]
    public void Process_WhenNoDamageData_ShouldNotCalculateHullBreach()
    {
        var command = new MechFallCommand
        {
            UnitId = _unitId,
            DamageData = null,
            GameOriginId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow
        };
        var sut = new ApplyFallAction(_mech, command);

        var result = sut.Process(Game);

        MockCriticalHitsCalculator.DidNotReceive().CalculateAndApplyCriticalHits(Arg.Any<IUnit>(), Arg.Any<List<LocationDamageData>>());
        result.Count.ShouldBe(1);
    }

    [Fact]
    public void Process_WhenDamageDataExists_ShouldCalculateHullBreach()
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
        var command = new MechFallCommand
        {
            UnitId = _unitId,
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
        var mockHullBreach = Substitute.For<IHullBreachCalculator>();
        mockHullBreach.CalculateAndApplyHullBreach(_mech, Arg.Any<List<LocationDamageData>>())
            .Returns(hullBreachCommand);
        Game = CreateGameWith(mockHullBreach);
        var sut = new ApplyFallAction(_mech, command);

        var result = sut.Process(Game);

        result.Count.ShouldBe(2);
        result[0].ShouldBe(command);
        result[1].ShouldBe(hullBreachCommand);
        hullBreachCommand.GameOriginId.ShouldBe(Game.Id);
    }

    [Fact]
    public void Process_WhenNoHullBreach_ShouldNotAddExtraCommand()
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
        var command = new MechFallCommand
        {
            UnitId = _unitId,
            DamageData = damageData,
            GameOriginId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow
        };
        var mockHullBreach = Substitute.For<IHullBreachCalculator>();
        mockHullBreach.CalculateAndApplyHullBreach(_mech, Arg.Any<List<LocationDamageData>>())
            .Returns((HullBreachCommand?)null);
        Game = CreateGameWith(mockHullBreach);
        var sut = new ApplyFallAction(_mech, command);

        var result = sut.Process(Game);

        result.Count.ShouldBe(1);
        result[0].ShouldBe(command);
    }

    [Fact]
    public void Process_ShouldPassDamagedLocationsToHullBreachCalculator()
    {
        var damageData = new FallingDamageData(
            HexDirection.Top,
            new HitLocationsData(
            [
                new LocationHitData(
                    [new LocationDamageData(PartLocation.CenterTorso, 5, 0, false)],
                    [],
                    [],
                    PartLocation.CenterTorso),
                new LocationHitData(
                    [new LocationDamageData(PartLocation.LeftArm, 3, 0, false)],
                    [],
                    [],
                    PartLocation.LeftArm)
            ],
                8),
            new DiceResult(4),
            HitDirection.Front);
        var command = new MechFallCommand
        {
            UnitId = _unitId,
            DamageData = damageData,
            GameOriginId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow
        };
        var mockHullBreach = Substitute.For<IHullBreachCalculator>();
        mockHullBreach.CalculateAndApplyHullBreach(_mech, Arg.Any<List<LocationDamageData>>())
            .Returns((HullBreachCommand?)null);
        Game = CreateGameWith(mockHullBreach);
        var sut = new ApplyFallAction(_mech, command);

        sut.Process(Game);

        mockHullBreach.Received(1).CalculateAndApplyHullBreach(
            _mech,
            Arg.Is<List<LocationDamageData>>(l => l.Count == 2));
    }

    private ServerGame CreateGameWith(IHullBreachCalculator hullBreach)
    {
        return new ServerGame(new TotalWarfareRulesProvider(),
            null!, CommandPublisher, DiceRoller,
            MockToHitCalculator, MockDamageTransferCalculator,
            MockCriticalHitsCalculator, hullBreach,
            MockPilotingSkillCalculator,
            MockConsciousnessCalculator,
            MockHeatEffectsCalculator,
            MockFallProcessor,
            Substitute.For<IWeaponAttackResolver>(),
            null!, MockPhaseManager);
    }
}
