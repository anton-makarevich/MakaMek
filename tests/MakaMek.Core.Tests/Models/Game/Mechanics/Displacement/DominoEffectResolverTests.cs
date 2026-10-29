using NSubstitute;
using Sanet.Localization;
using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Displacement;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Core.Tests.Utils;
using Sanet.MakaMek.Core.Utils;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Models.Terrains;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Mechanics.Displacement;

public class DominoEffectResolverTests
{
    // Bottom keeps a chain on a tall map, so the iteration-limit test can build a long line.
    private static readonly HexDirection ChainDirection = HexDirection.Bottom;
    private static readonly HexCoordinates DestinationHex = new(5, 5);
    private static readonly HexCoordinates SourceHex = new(5, 4);

    private static readonly HexCoordinates FirstChainHex = DestinationHex.GetNeighbour(ChainDirection);
    private static readonly HexCoordinates SecondChainHex = FirstChainHex.GetNeighbour(ChainDirection);
    private static readonly HexCoordinates ThirdChainHex = SecondChainHex.GetNeighbour(ChainDirection);

    private readonly IDiceRoller _diceRoller = Substitute.For<IDiceRoller>();
    private readonly DominoEffectResolver _sut;
    private readonly Guid _gameId = Guid.NewGuid();

    public DominoEffectResolverTests()
    {
        _sut = new DominoEffectResolver(_diceRoller);
    }

    [Fact]
    public void Resolve_EnteringUnitWithoutPosition_IsBlocked()
    {
        var map = CreateFlatMap();
        var entering = Substitute.For<IUnit>();
        entering.Id.Returns(Guid.NewGuid());
        entering.Position.Returns((HexPosition?)null);
        entering.IsDeployed.Returns(false);
        var occupant = UnitAt(DestinationHex);

        var result = _sut.Resolve(Request(entering), map, [entering, occupant]);

        result.Outcome.ShouldBe(DominoEffectOutcome.Blocked);
        result.Failure.ShouldBe(DisplacementFailureReason.OffMap);
    }

    [Fact]
    public void Resolve_EnteringUnitDeepDropIntoAnOccupiedHex_ReturnsAccidentalFallFromAbove()
    {
        var map = CreateFlatMap();
        map.AddHex(new Hex(SourceHex, 2));
        var entering = UnitAt(SourceHex);
        var occupant = UnitAt(DestinationHex);

        var result = _sut.Resolve(Request(entering), map, [entering, occupant]);

        result.Outcome.ShouldBe(DominoEffectOutcome.AccidentalFallFromAbove);
        result.Commands.ShouldBeEmpty();
    }

    [Fact]
    public void Resolve_EnteringUnitAscentTooHigh_BlocksTheChain()
    {
        var map = CreateFlatMap();
        map.AddHex(new Hex(DestinationHex, 2));
        var entering = UnitAt(SourceHex);
        var occupant = UnitAt(DestinationHex);

        var result = _sut.Resolve(Request(entering), map, [entering, occupant]);

        result.Outcome.ShouldBe(DominoEffectOutcome.Blocked);
        result.Failure.ShouldBe(DisplacementFailureReason.TooHigh);
        result.Commands.ShouldBeEmpty();
    }

    [Fact]
    public void Resolve_UnoccupiedDestination_ReturnsNotRequired()
    {
        var map = CreateFlatMap();
        var entering = UnitAt(SourceHex);

        var result = _sut.Resolve(Request(entering), map, [entering]);

        result.Outcome.ShouldBe(DominoEffectOutcome.NotRequired);
        result.Commands.ShouldBeEmpty();
        result.PsrCandidates.ShouldBeEmpty();
    }

    [Fact]
    public void Resolve_SingleLink_DisplacesTheOccupantOneHexAndKeepsTheFacing()
    {
        var map = CreateFlatMap();
        var entering = UnitAt(SourceHex);
        var occupant = UnitAt(DestinationHex, HexDirection.BottomRight);

        var result = _sut.Resolve(Request(entering), map, [entering, occupant]);

        result.Outcome.ShouldBe(DominoEffectOutcome.Resolved);
        var command = result.Commands.ShouldHaveSingleItem();
        command.UnitId.ShouldBe(occupant.Id);
        command.FromCoordinates.ShouldBe(DestinationHex.ToData());
        command.ToCoordinates.ShouldBe(FirstChainHex.ToData());
        command.NewFacing.ShouldBe((int)HexDirection.BottomRight);
        command.Surface.ShouldBe((int)HexSurface.Ground);
        command.DisplacementReason.ShouldBe(DisplacementReason.DominoEffect);
        command.GameOriginId.ShouldBe(_gameId);
        result.PsrCandidates.Select(candidate => candidate.Id).ShouldBe([entering.Id, occupant.Id]);
    }

    [Fact]
    public void Resolve_ChainOfThreeUnits_ReturnsCommandsFarToNear()
    {
        var map = CreateFlatMap();
        var entering = UnitAt(SourceHex);
        var first = UnitAt(DestinationHex);
        var second = UnitAt(FirstChainHex);
        var third = UnitAt(SecondChainHex);

        var result = _sut.Resolve(Request(entering), map, [entering, first, second, third]);

        result.Outcome.ShouldBe(DominoEffectOutcome.Resolved);
        result.Commands.Select(command => command.UnitId)
            .ShouldBe([third.Id, second.Id, first.Id]);
        result.Commands.Select(command => command.ToCoordinates)
            .ShouldBe([ThirdChainHex.ToData(), SecondChainHex.ToData(), FirstChainHex.ToData()]);
        result.PsrCandidates.Select(candidate => candidate.Id)
            .ShouldBe([entering.Id, first.Id, second.Id, third.Id]);
    }

    [Fact]
    public void Resolve_OffMapChainEnd_BlocksTheWholeChain()
    {
        var map = new BattleMap(30, 30);
        var destination = new HexCoordinates(5, 30);
        var source = new HexCoordinates(5, 29);
        AddHex(map, new Hex(source));
        AddHex(map, new Hex(destination));
        var entering = UnitAt(source);
        var occupant = UnitAt(destination);

        var result = _sut.Resolve(
            Request(entering, destination: destination), map, [entering, occupant]);

        result.Outcome.ShouldBe(DominoEffectOutcome.Blocked);
        result.Failure.ShouldBe(DisplacementFailureReason.OffMap);
        result.BlockedAt.ShouldBe(new HexCoordinates(5, 31));
        result.Commands.ShouldBeEmpty();
    }

    [Fact]
    public void Resolve_ChainHexWithoutHexData_BlocksTheWholeChainAsProhibitedTerrain()
    {
        var map = new BattleMap(30, 30);
        var destination = new HexCoordinates(5, 10);
        var source = new HexCoordinates(5, 9);
        AddHex(map, new Hex(source));
        AddHex(map, new Hex(destination));
        var entering = UnitAt(source);
        var occupant = UnitAt(destination);

        var result = _sut.Resolve(
            Request(entering, destination: destination), map, [entering, occupant]);

        result.Outcome.ShouldBe(DominoEffectOutcome.Blocked);
        result.Failure.ShouldBe(DisplacementFailureReason.ProhibitedTerrain);
        result.Commands.ShouldBeEmpty();
    }

    [Fact]
    public void Resolve_AscentOfTwoLevels_BlocksTheWholeChain()
    {
        var map = CreateFlatMap();
        map.AddHex(new Hex(FirstChainHex, 2));
        var entering = UnitAt(SourceHex);
        var occupant = UnitAt(DestinationHex);

        var result = _sut.Resolve(Request(entering), map, [entering, occupant]);

        result.Outcome.ShouldBe(DominoEffectOutcome.Blocked);
        result.Failure.ShouldBe(DisplacementFailureReason.TooHigh);
        result.Commands.ShouldBeEmpty();
    }

    [Fact]
    public void Resolve_AscentOfOneLevel_IsAnOrdinaryDomino()
    {
        var map = CreateFlatMap();
        map.AddHex(new Hex(FirstChainHex, 1));
        var entering = UnitAt(SourceHex);
        var occupant = UnitAt(DestinationHex);

        var result = _sut.Resolve(Request(entering), map, [entering, occupant]);

        result.Outcome.ShouldBe(DominoEffectOutcome.Resolved);
        result.Commands.ShouldHaveSingleItem();
    }

    [Fact]
    public void Resolve_DropOfTwoLevelsIntoAnOccupiedHex_ReturnsAccidentalFallFromAbove()
    {
        var map = CreateFlatMap();
        map.AddHex(new Hex(FirstChainHex, -2));
        var entering = UnitAt(SourceHex);
        var first = UnitAt(DestinationHex);
        var second = UnitAt(FirstChainHex);

        var result = _sut.Resolve(Request(entering), map, [entering, first, second]);

        result.Outcome.ShouldBe(DominoEffectOutcome.AccidentalFallFromAbove);
        result.BlockedAt.ShouldBe(FirstChainHex);
        result.Commands.ShouldBeEmpty();
    }

    [Fact]
    public void Resolve_DropOfTwoLevelsIntoAnEmptyHex_IsAllowed()
    {
        var map = CreateFlatMap();
        map.AddHex(new Hex(FirstChainHex, -2));
        var entering = UnitAt(SourceHex);
        var occupant = UnitAt(DestinationHex);

        var result = _sut.Resolve(Request(entering), map, [entering, occupant]);

        result.Outcome.ShouldBe(DominoEffectOutcome.Resolved);
        result.Commands.ShouldHaveSingleItem().Surface.ShouldBe((int)HexSurface.Ground);
    }

    [Fact]
    public void Resolve_BridgeHexAtTheGroundLevel_KeepsTheGroundSurface()
    {
        var map = CreateFlatMap();
        var bridgeHex = new Hex(FirstChainHex);
        bridgeHex.AddTerrain(new BridgeTerrain(2, 10));
        map.AddHex(bridgeHex);
        var entering = UnitAt(SourceHex, height: 1);
        var occupant = UnitAt(DestinationHex);

        var result = _sut.Resolve(Request(entering), map, [entering, occupant]);

        result.Outcome.ShouldBe(DominoEffectOutcome.Resolved);
        result.Commands.ShouldHaveSingleItem().Surface.ShouldBe((int)HexSurface.Ground);
    }

    [Fact]
    public void Resolve_BridgeDeckMatchesTheSourceLevel_SelectsTheBridgeSurface()
    {
        var map = CreateFlatMap();
        var destinationBridgeHex = new Hex(DestinationHex);
        destinationBridgeHex.AddTerrain(new BridgeTerrain(1, 10));
        map.AddHex(destinationBridgeHex);
        var bridgeHex = new Hex(FirstChainHex);
        bridgeHex.AddTerrain(new BridgeTerrain(1, 10));
        map.AddHex(bridgeHex);
        var entering = UnitAt(SourceHex, surface: HexSurface.Bridge);
        var occupant = UnitAt(DestinationHex, surface: HexSurface.Bridge);

        var result = _sut.Resolve(Request(entering), map, [entering, occupant]);

        result.Outcome.ShouldBe(DominoEffectOutcome.Resolved);
        result.Commands.ShouldHaveSingleItem().Surface.ShouldBe((int)HexSurface.Bridge);
    }

    [Fact]
    public void Resolve_UnitThatCannotStandUnderTheBridge_ExcludesTheGroundSurface()
    {
        var map = CreateFlatMap();
        var bridgeHex = new Hex(FirstChainHex);
        bridgeHex.AddTerrain(new BridgeTerrain(1, 10));
        map.AddHex(bridgeHex);
        var entering = UnitAt(SourceHex, height: 2);
        var occupant = UnitAt(DestinationHex, height: 2);

        var result = _sut.Resolve(Request(entering), map, [entering, occupant]);

        result.Outcome.ShouldBe(DominoEffectOutcome.Resolved);
        result.Commands.ShouldHaveSingleItem().Surface.ShouldBe((int)HexSurface.Bridge);
    }

    [Fact]
    public void Resolve_MultipleOccupants_PrefersTheSameConcreteUnitType()
    {
        var map = CreateFlatMap();
        var entering = CreateMech(SourceHex);
        var sameType = CreateMech(DestinationHex);
        var otherType = UnitAt(DestinationHex);

        var result = _sut.Resolve(Request(entering), map, [entering, sameType, otherType]);

        result.Outcome.ShouldBe(DominoEffectOutcome.Resolved);
        result.Commands.ShouldHaveSingleItem().UnitId.ShouldBe(sameType.Id);
        _diceRoller.DidNotReceive().RollD6();
    }

    [Fact]
    public void Resolve_MultipleOccupantsOfTheSameType_SelectsRandomly()
    {
        var map = CreateFlatMap();
        var entering = CreateMech(SourceHex);
        var first = CreateMech(DestinationHex);
        var second = CreateMech(DestinationHex);
        _diceRoller.RollD6().Returns(new DiceResult(2));

        var result = _sut.Resolve(Request(entering), map, [entering, first, second]);

        result.Outcome.ShouldBe(DominoEffectOutcome.Resolved);
        result.Commands.ShouldHaveSingleItem().UnitId.ShouldBe(second.Id);
        _diceRoller.Received(1).RollD6();
    }

    [Fact]
    public void Resolve_ChainLongerThanTheIterationLimit_BlocksTheChain()
    {
        var map = CreateFlatMap();
        var units = new List<IUnit>();
        var entering = UnitAt(SourceHex);
        units.Add(entering);
        var coordinates = SourceHex;
        for (var i = 0; i < DominoEffectResolver.MaxDisplacementIterations + 1; i++)
        {
            coordinates = coordinates.GetNeighbour(ChainDirection);
            units.Add(UnitAt(coordinates));
        }

        var result = _sut.Resolve(Request(entering), map, units);

        result.Outcome.ShouldBe(DominoEffectOutcome.Blocked);
        result.Failure.ShouldBe(DisplacementFailureReason.IterationLimit);
        result.Commands.ShouldBeEmpty();
    }

    [Fact]
    public void Resolve_ExplicitOccupants_AreTheOnlyDislodgedUnits()
    {
        var map = CreateFlatMap();
        var entering = UnitAt(SourceHex);
        var explicitOccupant = UnitAt(DestinationHex);
        var otherOccupant = UnitAt(DestinationHex);

        var result = _sut.Resolve(
            Request(entering, [explicitOccupant]), map, [entering, explicitOccupant, otherOccupant]);

        result.Outcome.ShouldBe(DominoEffectOutcome.Resolved);
        result.Commands.ShouldHaveSingleItem().UnitId.ShouldBe(explicitOccupant.Id);
        // A single explicit occupant needs no random selection.
        _diceRoller.DidNotReceive().RollD6();
    }

    [Fact]
    public void Resolve_EntryElevationRuleDisabled_AllowsADeepDropIntoAnOccupiedHex()
    {
        var map = CreateFlatMap();
        var entering = UnitAt(SourceHex);
        var occupant = UnitAt(DestinationHex);
        map.AddHex(new Hex(SourceHex, 3));

        var result = _sut.Resolve(
            Request(entering, applyEntryElevationRule: false), map, [entering, occupant]);

        result.Outcome.ShouldBe(DominoEffectOutcome.Resolved);
        result.Commands.ShouldHaveSingleItem();
    }

    private DominoEffectRequest Request(
        IUnit entering,
        IReadOnlyList<IUnit>? occupants = null,
        bool applyEntryElevationRule = true,
        HexCoordinates? destination = null) => new()
    {
        EnteringUnit = entering,
        Destination = destination ?? DestinationHex,
        Direction = ChainDirection,
        Reason = DisplacementReason.DominoEffect,
        GameOriginId = _gameId,
        Occupants = occupants?.Where(occupant => occupant.Id != entering.Id).ToList(),
        ApplyEntryElevationRule = applyEntryElevationRule
    };

    private static IUnit UnitAt(
        HexCoordinates coordinates,
        HexDirection facing = HexDirection.Top,
        HexSurface surface = HexSurface.Ground,
        int height = 1)
    {
        var unit = Substitute.For<IUnit>();
        unit.Id.Returns(Guid.NewGuid());
        unit.Position.Returns(new HexPosition(coordinates, facing, surface));
        unit.IsDeployed.Returns(true);
        unit.IsDestroyed.Returns(false);
        unit.Height.Returns(height);
        return unit;
    }

    private static Mech CreateMech(HexCoordinates coordinates)
    {
        var factory = new MechFactory(
            new TotalWarfareRulesProvider(),
            new ClassicBattletechComponentProvider(),
            Substitute.For<ILocalizationService>());
        var data = MechFactoryTests.CreateDummyMechData();
        data.Id = Guid.NewGuid();
        var mech = factory.Create(data);
        mech.Deploy(new HexPosition(coordinates, HexDirection.Top), null);
        return mech;
    }

    private static BattleMap CreateFlatMap()
    {
        var map = new BattleMap(30, 30);
        for (var q = 1; q <= 30; q++)
        for (var r = 1; r <= 30; r++)
            AddHex(map, new HexCoordinates(q, r));
        return map;
    }

    private static void AddHex(BattleMap map, HexCoordinates coordinates) => AddHex(map, new Hex(coordinates));

    private static void AddHex(BattleMap map, Hex hex) => map.AddHex(hex);
}
