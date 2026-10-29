using NSubstitute;
using Sanet.MakaMek.Core.Models.Game.Mechanics.PhysicalAttack;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Models.Terrains;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Mechanics.PhysicalAttack;

public class PushDisplacementResolverTests
{
    private static readonly HexCoordinates AttackerHex = new(5, 5);
    private static readonly HexCoordinates TargetHex = AttackerHex.GetNeighbour(HexDirection.Top);
    private static readonly HexCoordinates DestinationHex = TargetHex.GetNeighbour(HexDirection.Top);

    private readonly PushDisplacementResolver _sut = new(new TotalWarfareRulesProvider());

    [Fact]
    public void Resolve_AllowedPush_MovesTheTargetOneHexAwayAndTheAttackerIntoItsHex()
    {
        // Arrange
        var map = CreateFlatMap();
        var attacker = UnitAt(AttackerHex, HexDirection.Top);
        var target = UnitAt(TargetHex, HexDirection.Bottom);

        // Act
        var result = _sut.Resolve(attacker, target, map, [attacker, target]);

        // Assert
        result.ShouldNotBeNull();
        result.TargetDestination.Coordinates.ShouldBe(DestinationHex);
        result.TargetDestination.Facing.ShouldBe(target.Position!.Facing);
        result.TargetDestination.Surface.ShouldBe(HexSurface.Ground);
        result.AttackerDestination.Coordinates.ShouldBe(TargetHex);
        result.AttackerDestination.Facing.ShouldBe(attacker.Position!.Facing);
        result.LevelsFallen.ShouldBe(0);
        result.TriggersAutomaticFall.ShouldBeFalse();
    }

    [Fact]
    public void Resolve_DestinationOffTheMap_IsBlocked()
    {
        // Arrange: the target stands at the edge, so the destination would be off-map
        var map = new BattleMap(10, 10);
        AddHex(map, new HexCoordinates(5, 2));
        AddHex(map, new HexCoordinates(5, 1));
        var attacker = UnitAt(new HexCoordinates(5, 2), HexDirection.Top);
        var target = UnitAt(new HexCoordinates(5, 1), HexDirection.Bottom);

        // Act
        var result = _sut.Resolve(attacker, target, map, [attacker, target]);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void Resolve_DestinationOccupiedByAnotherUnit_IsBlocked()
    {
        // Arrange
        var map = CreateFlatMap();
        var attacker = UnitAt(AttackerHex, HexDirection.Top);
        var target = UnitAt(TargetHex, HexDirection.Bottom);
        var occupant = UnitAt(DestinationHex, HexDirection.Top);

        // Act
        var result = _sut.Resolve(attacker, target, map, [attacker, target, occupant]);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void Resolve_DestinationHigherThanTheTarget_IsBlocked()
    {
        // Arrange
        var map = CreateFlatMap();
        AddHex(map, new Hex(DestinationHex, 1));
        var attacker = UnitAt(AttackerHex, HexDirection.Top);
        var target = UnitAt(TargetHex, HexDirection.Bottom);

        // Act
        var result = _sut.Resolve(attacker, target, map, [attacker, target]);

        // Assert
        result.ShouldBeNull();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void Resolve_DropOfOneOrTwoLevels_IsAllowedWithoutAFall(int levelsFallen)
    {
        // Arrange
        var map = CreateFlatMap();
        AddHex(map, new Hex(TargetHex, levelsFallen));
        var attacker = UnitAt(AttackerHex, HexDirection.Top);
        var target = UnitAt(TargetHex, HexDirection.Bottom);

        // Act
        var result = _sut.Resolve(attacker, target, map, [attacker, target]);

        // Assert
        result.ShouldNotBeNull();
        result.LevelsFallen.ShouldBe(levelsFallen);
        result.TriggersAutomaticFall.ShouldBeFalse();
    }

    [Fact]
    public void Resolve_DropOfMoreThanTwoLevels_TriggersAnAutomaticFall()
    {
        // Arrange
        var map = CreateFlatMap();
        AddHex(map, new Hex(TargetHex, 3));
        var attacker = UnitAt(AttackerHex, HexDirection.Top);
        var target = UnitAt(TargetHex, HexDirection.Bottom);

        // Act
        var result = _sut.Resolve(attacker, target, map, [attacker, target]);

        // Assert
        result.ShouldNotBeNull();
        result.LevelsFallen.ShouldBe(3);
        result.TriggersAutomaticFall.ShouldBeTrue();
    }

    [Fact]
    public void Resolve_DestinationBridgeDeckAtTheTargetsLevel_KeepsTheBridgeSurface()
    {
        // Arrange: the target is on a bridge deck at level 2, and the destination deck is also level 2
        var map = CreateFlatMap();
        AddHex(map, CreateBridgeHex(TargetHex, level: 0, bridgeHeight: 2));
        AddHex(map, CreateBridgeHex(DestinationHex, level: 0, bridgeHeight: 2));
        var attacker = UnitAt(AttackerHex, HexDirection.Top);
        var target = UnitAt(TargetHex, HexDirection.Bottom, HexSurface.Bridge);

        // Act
        var result = _sut.Resolve(attacker, target, map, [attacker, target]);

        // Assert
        result.ShouldNotBeNull();
        result.TargetDestination.Surface.ShouldBe(HexSurface.Bridge);
        result.LevelsFallen.ShouldBe(0);
    }

    [Fact]
    public void Resolve_DestinationBridgeDeckAtADifferentLevel_UsesTheGroundSurface()
    {
        // Arrange: the target is on a bridge deck at level 2, the destination deck is at level 4
        // and its ground is at level 2
        var map = CreateFlatMap();
        AddHex(map, CreateBridgeHex(TargetHex, level: 0, bridgeHeight: 2));
        AddHex(map, CreateBridgeHex(DestinationHex, level: 2, bridgeHeight: 2));
        var attacker = UnitAt(AttackerHex, HexDirection.Top);
        var target = UnitAt(TargetHex, HexDirection.Bottom, HexSurface.Bridge);

        // Act
        var result = _sut.Resolve(attacker, target, map, [attacker, target]);

        // Assert
        result.ShouldNotBeNull();
        result.TargetDestination.Surface.ShouldBe(HexSurface.Ground);
        result.LevelsFallen.ShouldBe(0);
    }

    [Fact]
    public void Resolve_TargetNotDirectlyInFront_IsBlocked()
    {
        // Arrange: the target is in a side hex
        var map = CreateFlatMap();
        var attacker = UnitAt(AttackerHex, HexDirection.Top);
        var target = UnitAt(AttackerHex.GetNeighbour(HexDirection.BottomRight), HexDirection.Bottom);

        // Act
        var result = _sut.Resolve(attacker, target, map, [attacker, target]);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void Resolve_DestroyedTarget_IsBlocked()
    {
        // Arrange
        var map = CreateFlatMap();
        var attacker = UnitAt(AttackerHex, HexDirection.Top);
        var target = UnitAt(TargetHex, HexDirection.Bottom, isDestroyed: true);

        // Act
        var result = _sut.Resolve(attacker, target, map, [attacker, target]);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void Resolve_DestroyedAttacker_IsBlocked()
    {
        // Arrange
        var map = CreateFlatMap();
        var attacker = UnitAt(AttackerHex, HexDirection.Top, isDestroyed: true);
        var target = UnitAt(TargetHex, HexDirection.Bottom);

        // Act
        var result = _sut.Resolve(attacker, target, map, [attacker, target]);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void Resolve_UndeployedAttacker_IsBlocked()
    {
        // Arrange
        var map = CreateFlatMap();
        var attacker = UnitAt(AttackerHex, HexDirection.Top, isDeployed: false);
        var target = UnitAt(TargetHex, HexDirection.Bottom);

        // Act
        var result = _sut.Resolve(attacker, target, map, [attacker, target]);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void Resolve_UndeployedTarget_IsBlocked()
    {
        // Arrange
        var map = CreateFlatMap();
        var attacker = UnitAt(AttackerHex, HexDirection.Top);
        var target = UnitAt(TargetHex, HexDirection.Bottom, isDeployed: false);

        // Act
        var result = _sut.Resolve(attacker, target, map, [attacker, target]);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void Resolve_DestinationHexMissingFromTheMap_IsBlocked()
    {
        // Arrange: the destination is inside the map bounds but has no hex
        var map = new BattleMap(10, 10);
        AddHex(map, new Hex(AttackerHex));
        AddHex(map, new Hex(TargetHex));
        var attacker = UnitAt(AttackerHex, HexDirection.Top);
        var target = UnitAt(TargetHex, HexDirection.Bottom);

        // Act
        var result = _sut.Resolve(attacker, target, map, [attacker, target]);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void Resolve_TargetHexMissingFromTheMap_IsBlocked()
    {
        // Arrange: the target's own hex has no terrain data on the map
        var map = new BattleMap(10, 10);
        AddHex(map, new Hex(DestinationHex));
        var attacker = UnitAt(AttackerHex, HexDirection.Top);
        var target = UnitAt(TargetHex, HexDirection.Bottom);

        // Act
        var result = _sut.Resolve(attacker, target, map, [attacker, target]);

        // Assert
        result.ShouldBeNull();
    }

    private static IUnit UnitAt(HexCoordinates coordinates, HexDirection facing,
        HexSurface surface = HexSurface.Ground, bool isDestroyed = false, bool isDeployed = true)
    {
        var unit = Substitute.For<IUnit>();
        unit.Id.Returns(Guid.NewGuid());
        if (isDeployed)
            unit.Position.Returns(new HexPosition(coordinates, facing, surface));
        else
            unit.Position.Returns((HexPosition?)null);
        unit.IsDeployed.Returns(isDeployed);
        unit.IsDestroyed.Returns(isDestroyed);
        return unit;
    }

    private static BattleMap CreateFlatMap()
    {
        var map = new BattleMap(10, 10);
        for (var q = 1; q <= 10; q++)
        for (var r = 1; r <= 10; r++)
            AddHex(map, new HexCoordinates(q, r));
        return map;
    }

    private static Hex CreateBridgeHex(HexCoordinates coordinates, int level, int bridgeHeight)
    {
        var hex = new Hex(coordinates, level);
        hex.AddTerrain(new BridgeTerrain(bridgeHeight, 10));
        return hex;
    }

    private static void AddHex(BattleMap map, HexCoordinates coordinates) => AddHex(map, new Hex(coordinates));

    private static void AddHex(BattleMap map, Hex hex) => map.AddHex(hex);
}
