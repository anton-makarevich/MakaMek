using NSubstitute;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Displacement;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Models.Terrains;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Mechanics.Displacement;

public class ForcedEntryCheckTests
{
    private static readonly HexCoordinates From = new(5, 5);
    private static readonly HexCoordinates To = new(5, 6);

    [Fact]
    public void Check_DestinationOffMap_ReturnsOffMap()
    {
        var map = CreateMap(new Hex(From));

        var result = ForcedEntryCheck.Check(
            Unit(), map, From, HexSurface.Ground, new HexCoordinates(5, 31));

        result.Failure.ShouldBe(DisplacementFailureReason.OffMap);
        result.Surface.ShouldBe(HexSurface.Ground);
        result.LevelDifference.ShouldBe(0);
        result.IsLegal.ShouldBeFalse();
    }

    [Fact]
    public void Check_DestinationHexMissing_ReturnsProhibitedTerrain()
    {
        var map = CreateMap(new Hex(From));

        var result = ForcedEntryCheck.Check(Unit(), map, From, HexSurface.Ground, To);

        result.Failure.ShouldBe(DisplacementFailureReason.ProhibitedTerrain);
        result.IsLegal.ShouldBeFalse();
    }

    [Fact]
    public void Check_SourceHexMissing_ReturnsOffMap()
    {
        // The destination is on the map and present, but the source hex is not.
        var map = CreateMap(new Hex(To));

        var result = ForcedEntryCheck.Check(Unit(), map, From, HexSurface.Ground, To);

        result.Failure.ShouldBe(DisplacementFailureReason.OffMap);
        result.Surface.ShouldBe(HexSurface.Ground);
        result.LevelDifference.ShouldBe(0);
        result.IsLegal.ShouldBeFalse();
    }

    [Fact]
    public void Check_AscentAboveTheUnitLimit_ReturnsTooHigh()
    {
        var map = CreateMap(new Hex(From), new Hex(To, 3));

        var result = ForcedEntryCheck.Check(
            Unit(maxLevelChangeForward: 2), map, From, HexSurface.Ground, To);

        result.Failure.ShouldBe(DisplacementFailureReason.TooHigh);
        result.Surface.ShouldBe(HexSurface.Ground);
        result.IsLegal.ShouldBeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Check_UnitThatCannotClimb_ReturnsTooHighForAnyAscent(int maxLevelChangeForward)
    {
        var map = CreateMap(new Hex(From), new Hex(To, 1));

        var result = ForcedEntryCheck.Check(
            Unit(maxLevelChangeForward: maxLevelChangeForward), map, From, HexSurface.Ground, To);

        result.Failure.ShouldBe(DisplacementFailureReason.TooHigh);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void Check_AscentWithinTheUnitLimit_IsLegal(int destinationLevel)
    {
        var map = CreateMap(new Hex(From), new Hex(To, destinationLevel));

        var result = ForcedEntryCheck.Check(
            Unit(maxLevelChangeForward: 2), map, From, HexSurface.Ground, To);

        result.IsLegal.ShouldBeTrue();
        result.Failure.ShouldBeNull();
        result.Surface.ShouldBe(HexSurface.Ground);
        result.LevelDifference.ShouldBe(destinationLevel);
    }

    [Fact]
    public void Check_DeepDrop_IsLegalAndFlaggedAsDeepDrop()
    {
        // Downward displacement is unlimited: the caller resolves drops of two or more levels as falls.
        var map = CreateMap(new Hex(From), new Hex(To, -3));

        var result = ForcedEntryCheck.Check(Unit(), map, From, HexSurface.Ground, To);

        result.IsLegal.ShouldBeTrue();
        result.Surface.ShouldBe(HexSurface.Ground);
        result.LevelDifference.ShouldBe(-3);
        result.IsDeepDrop.ShouldBeTrue();
    }

    [Fact]
    public void Check_UnitThatFitsUnderTheBridge_SelectsTheClosestSurface()
    {
        var map = CreateMap(new Hex(From), BridgeHex(To, height: 1));

        var result = ForcedEntryCheck.Check(Unit(height: 1), map, From, HexSurface.Ground, To);

        result.IsLegal.ShouldBeTrue();
        result.Surface.ShouldBe(HexSurface.Ground);
        result.LevelDifference.ShouldBe(0);
    }

    [Fact]
    public void Check_UnitThatCannotFitUnderTheBridge_ExcludesTheGroundSurface()
    {
        var map = CreateMap(new Hex(From), BridgeHex(To, height: 1));

        var result = ForcedEntryCheck.Check(Unit(height: 2), map, From, HexSurface.Ground, To);

        result.IsLegal.ShouldBeTrue();
        result.Surface.ShouldBe(HexSurface.Bridge);
        result.LevelDifference.ShouldBe(1);
    }

    [Fact]
    public void Check_EquidistantSurfaces_PreferTheSourceSurface()
    {
        // Source standing level 1 sits exactly between the ground (0) and the bridge deck (2).
        var map = CreateMap(new Hex(From, 1), BridgeHex(To, height: 2));

        var result = ForcedEntryCheck.Check(Unit(), map, From, HexSurface.Ground, To);

        result.IsLegal.ShouldBeTrue();
        result.Surface.ShouldBe(HexSurface.Ground);
        result.LevelDifference.ShouldBe(-1);
    }

    [Fact]
    public void Check_EquidistantSurfacesFromBridge_PreferTheBridgeSurface()
    {
        var map = CreateMap(
            BridgeHex(From, height: 1),
            BridgeHex(To, height: 2));

        var result = ForcedEntryCheck.Check(Unit(), map, From, HexSurface.Bridge, To);

        result.IsLegal.ShouldBeTrue();
        result.Surface.ShouldBe(HexSurface.Bridge);
        result.LevelDifference.ShouldBe(1);
    }

    private static IUnit Unit(int height = 1, int maxLevelChangeForward = 2)
    {
        var unit = Substitute.For<IUnit>();
        unit.Height.Returns(height);
        unit.MaxLevelChangeForward.Returns(maxLevelChangeForward);
        return unit;
    }

    private static Hex BridgeHex(HexCoordinates coordinates, int height)
    {
        var hex = new Hex(coordinates);
        hex.AddTerrain(new BridgeTerrain(height, 10));
        return hex;
    }

    private static BattleMap CreateMap(params Hex[] hexes)
    {
        var map = new BattleMap(30, 30);
        foreach (var hex in hexes)
            map.AddHex(hex);
        return map;
    }
}
