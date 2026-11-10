using NSubstitute;
using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Models.Game.Mechanics.PhysicalAttack;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Core.Models.Units.Pilots;
using Sanet.MakaMek.Core.Tests.Utils;
using Sanet.MakaMek.Core.Utils;
using Sanet.Localization;
using Sanet.MakaMek.Map.Factories;
using Sanet.MakaMek.Map.Generators;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Models.MovementCosts;
using Sanet.MakaMek.Map.Models.Terrains;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Mechanics.PhysicalAttack;

public class ChargeDisplacementResolverTests
{
    private static readonly HexCoordinates AttackerCoordinates = new(5, 5);
    private static readonly HexCoordinates TargetCoordinates =
        AttackerCoordinates.GetNeighbour(HexDirection.Top);
    private static readonly HexDirection ChargeDirection = HexDirection.Top;

    private readonly ChargeDisplacementResolver _sut =
        new(new TotalWarfareRulesProvider(), Substitute.For<IDiceRoller>());

    [Fact]
    public void ResolveHit_DisplacesTheTargetInTheChargeDirectionAndAdvancesTheAttacker()
    {
        // Arrange
        var map = CreateMap();
        var attacker = CreateMech(new HexPosition(AttackerCoordinates, ChargeDirection));
        var target = CreateMech(new HexPosition(TargetCoordinates, HexDirection.Bottom));
        MoveToChargeApproach(attacker, target.Position!);

        // Act
        var result = _sut.ResolveHit(attacker, target, map, [attacker, target], Guid.NewGuid());

        // Assert
        result.ShouldNotBeNull();
        result.TargetDestination!.Coordinates.ShouldBe(TargetCoordinates.GetNeighbour(ChargeDirection));
        result.TargetDestination.Facing.ShouldBe(target.Position!.Facing);
        result.AttackerDestination!.Coordinates.ShouldBe(TargetCoordinates);
        result.DominoEffect.ShouldBeNull();
        result.TriggersAutomaticFall.ShouldBeFalse();
    }

    [Fact]
    public void ResolveHit_IntoAnOccupiedHex_ResolvesTheDominoChain()
    {
        // Arrange
        var map = CreateMap();
        var attacker = CreateMech(new HexPosition(AttackerCoordinates, ChargeDirection));
        var target = CreateMech(new HexPosition(TargetCoordinates, HexDirection.Bottom));
        var occupant = CreateMech(new HexPosition(
            TargetCoordinates.GetNeighbour(ChargeDirection), HexDirection.Bottom));
        MoveToChargeApproach(attacker, target.Position!);

        // Act
        var result = _sut.ResolveHit(attacker, target, map, [attacker, target, occupant], Guid.NewGuid());

        // Assert
        result.ShouldNotBeNull();
        result.DominoEffect.ShouldNotBeNull().HasDisplacements.ShouldBeTrue();
        result.DominoEffect.Commands.ShouldHaveSingleItem().UnitId.ShouldBe(occupant.Id);
        result.DominoEffect.Commands[0].DisplacementReason.ShouldBe(DisplacementReason.DominoEffect);
        result.TargetDestination!.Coordinates.ShouldBe(TargetCoordinates.GetNeighbour(ChargeDirection));
        result.AttackerDestination!.Coordinates.ShouldBe(TargetCoordinates);
    }

    [Fact]
    public void ResolveHit_WhenBothUnitsAreDestroyed_ReturnsNull()
    {
        // Arrange
        var map = CreateMap();
        var attacker = CreateMech(new HexPosition(AttackerCoordinates, ChargeDirection));
        var target = CreateMech(new HexPosition(TargetCoordinates, HexDirection.Bottom));
        MoveToChargeApproach(attacker, target.Position!);
        Destroy(attacker);
        Destroy(target);

        // Act
        var result = _sut.ResolveHit(attacker, target, map, [attacker, target], Guid.NewGuid());

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void ResolveHit_WhenThePrimaryHexIsBlocked_UsesTheAlternatingFallbackOrder()
    {
        // Arrange: the primary hex is too high to enter, so the first side hex (rotate +1) wins.
        var map = CreateMap();
        var attacker = CreateMech(new HexPosition(AttackerCoordinates, ChargeDirection));
        var target = CreateMech(new HexPosition(TargetCoordinates, HexDirection.Bottom));
        MoveToChargeApproach(attacker, target.Position!);
        var primaryCoordinates = TargetCoordinates.GetNeighbour(ChargeDirection);
        map.AddHex(new Hex(primaryCoordinates, 3));

        // Act
        var result = _sut.ResolveHit(attacker, target, map, [attacker, target], Guid.NewGuid());

        // Assert
        result.ShouldNotBeNull();
        result.TargetDestination!.Coordinates.ShouldBe(
            TargetCoordinates.GetNeighbour(ChargeDirection.Rotate(1)));
    }

    [Fact]
    public void ResolveHit_WhenThePrimaryHexIsOffTheMap_UsesAnAlternatingFallback()
    {
        // Arrange: the target stands on the far edge, so one hex beyond it is off the map.
        var map = CreateMap();
        var attackerCoordinates = new HexCoordinates(5, 2);
        var targetCoordinates = new HexCoordinates(5, 1);
        var attacker = CreateMech(new HexPosition(attackerCoordinates, HexDirection.Top));
        var target = CreateMech(new HexPosition(targetCoordinates, HexDirection.Bottom));
        MoveToChargeApproach(attacker, target.Position!);

        // Act
        var result = _sut.ResolveHit(attacker, target, map, [attacker, target], Guid.NewGuid());

        // Assert
        result.ShouldNotBeNull();
        result.TargetDestination.ShouldNotBeNull();
        map.IsOnMap(result.TargetDestination.Coordinates).ShouldBeTrue();
        result.TargetDestination.Coordinates
            .DistanceTo(targetCoordinates).ShouldBe(1);
        result.TargetDestination.Coordinates
            .ShouldNotBe(targetCoordinates.GetNeighbour(ChargeDirection));
        result.AttackerDestination!.Coordinates.ShouldBe(targetCoordinates);
    }

    [Fact]
    public void ResolveHit_WhenEveryFallbackIsBlocked_LeavesBothUnitsInPlace()
    {
        // Arrange: one legal direction is off the map and every side hex is occupied.
        var map = CreateMap();
        var attackerCoordinates = new HexCoordinates(5, 2);
        var targetCoordinates = new HexCoordinates(5, 1);
        var attacker = CreateMech(new HexPosition(attackerCoordinates, HexDirection.Top));
        var target = CreateMech(new HexPosition(targetCoordinates, HexDirection.Bottom));
        MoveToChargeApproach(attacker, target.Position!);

        var units = new List<IUnit> { attacker, target };
        foreach (var side in GetSideDirections(ChargeDirection))
        {
            var coordinates = targetCoordinates.GetNeighbour(side);
            if (!map.IsOnMap(coordinates)) continue;
            units.Add(CreateMech(new HexPosition(coordinates, HexDirection.Bottom)));
        }

        // Act
        var result = _sut.ResolveHit(attacker, target, map, units, Guid.NewGuid());

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void ResolveHit_WhenTheTargetIsDestroyed_LetsTheAttackerAdvance()
    {
        // Arrange
        var map = CreateMap();
        var attacker = CreateMech(new HexPosition(AttackerCoordinates, ChargeDirection));
        var target = CreateMech(new HexPosition(TargetCoordinates, HexDirection.Bottom));
        MoveToChargeApproach(attacker, target.Position!);
        Destroy(target);

        // Act
        var result = _sut.ResolveHit(attacker, target, map, [attacker, target], Guid.NewGuid());

        // Assert
        result.ShouldNotBeNull();
        result.TargetDestination.ShouldBeNull();
        result.AttackerDestination!.Coordinates.ShouldBe(TargetCoordinates);
    }

    [Fact]
    public void ResolveHit_WhenTheAttackerIsDestroyed_StillDisplacesTheTarget()
    {
        // Arrange
        var map = CreateMap();
        var attacker = CreateMech(new HexPosition(AttackerCoordinates, ChargeDirection));
        var target = CreateMech(new HexPosition(TargetCoordinates, HexDirection.Bottom));
        MoveToChargeApproach(attacker, target.Position!);
        Destroy(attacker);

        // Act
        var result = _sut.ResolveHit(attacker, target, map, [attacker, target], Guid.NewGuid());

        // Assert
        result.ShouldNotBeNull();
        result.TargetDestination!.Coordinates.ShouldBe(TargetCoordinates.GetNeighbour(ChargeDirection));
        result.AttackerDestination.ShouldBeNull();
    }

    [Fact]
    public void ResolveMiss_PlacesTheAttackerInTheRightSideHexFirst()
    {
        // Arrange
        var map = CreateMap();
        var attacker = CreateMech(new HexPosition(AttackerCoordinates, ChargeDirection));

        // Act
        var result = _sut.ResolveMiss(attacker, map, [attacker]);

        // Assert
        result.ShouldNotBeNull();
        result.TargetDestination.ShouldBeNull();
        result.AttackerDestination!.Coordinates.ShouldBe(
            AttackerCoordinates.GetNeighbour(ChargeDirection.Rotate(1)));
    }

    [Fact]
    public void ResolveMiss_WhenTheRightSideHexIsOccupied_UsesTheLeftSideHex()
    {
        // Arrange
        var map = CreateMap();
        var attacker = CreateMech(new HexPosition(AttackerCoordinates, ChargeDirection));
        var blocker = CreateMech(new HexPosition(
            AttackerCoordinates.GetNeighbour(ChargeDirection.Rotate(1)), HexDirection.Bottom));

        // Act
        var result = _sut.ResolveMiss(attacker, map, [attacker, blocker]);

        // Assert
        result.ShouldNotBeNull();
        result.AttackerDestination!.Coordinates.ShouldBe(
            AttackerCoordinates.GetNeighbour(ChargeDirection.Rotate(-1)));
    }

    [Fact]
    public void ResolveMiss_WhenBothNearSideHexesAreOccupied_UsesTheSecondRing()
    {
        // Arrange
        var map = CreateMap();
        var attacker = CreateMech(new HexPosition(AttackerCoordinates, ChargeDirection));
        var right = CreateMech(new HexPosition(
            AttackerCoordinates.GetNeighbour(ChargeDirection.Rotate(1)), HexDirection.Bottom));
        var left = CreateMech(new HexPosition(
            AttackerCoordinates.GetNeighbour(ChargeDirection.Rotate(-1)), HexDirection.Bottom));

        // Act
        var result = _sut.ResolveMiss(attacker, map, [attacker, right, left]);

        // Assert
        result.ShouldNotBeNull();
        result.AttackerDestination!.Coordinates.ShouldBe(
            AttackerCoordinates.GetNeighbour(ChargeDirection.Rotate(2)));
    }

    [Fact]
    public void ResolveMiss_WhenTheSideHexIsADeepDrop_RejectsIt()
    {
        // Arrange
        var map = CreateMap();
        var attacker = CreateMech(new HexPosition(AttackerCoordinates, ChargeDirection));
        var rightCoordinates = AttackerCoordinates.GetNeighbour(ChargeDirection.Rotate(1));
        map.AddHex(new Hex(rightCoordinates, -3));

        // Act
        var result = _sut.ResolveMiss(attacker, map, [attacker]);

        // Assert
        result.ShouldNotBeNull();
        result.AttackerDestination!.Coordinates.ShouldNotBe(rightCoordinates);
        result.AttackerDestination.Coordinates.ShouldBe(
            AttackerCoordinates.GetNeighbour(ChargeDirection.Rotate(-1)));
    }

    [Fact]
    public void ResolveMiss_WhenTheSideHexIsTooHigh_RejectsIt()
    {
        // Arrange
        var map = CreateMap();
        var attacker = CreateMech(new HexPosition(AttackerCoordinates, ChargeDirection));
        var rightCoordinates = AttackerCoordinates.GetNeighbour(ChargeDirection.Rotate(1));
        map.AddHex(new Hex(rightCoordinates, 3));

        // Act
        var result = _sut.ResolveMiss(attacker, map, [attacker]);

        // Assert
        result.ShouldNotBeNull();
        result.AttackerDestination!.Coordinates.ShouldBe(
            AttackerCoordinates.GetNeighbour(ChargeDirection.Rotate(-1)));
    }

    [Fact]
    public void ResolveMiss_WhenNoSideHexIsValid_ReturnsNull()
    {
        // Arrange
        var map = CreateMap();
        var attacker = CreateMech(new HexPosition(AttackerCoordinates, ChargeDirection));
        var blockers = GetSideDirections(ChargeDirection)
            .Select(side => new HexPosition(AttackerCoordinates.GetNeighbour(side), HexDirection.Bottom))
            .Where(position => map.IsOnMap(position.Coordinates))
            .Select(CreateMech)
            .ToList();

        // Act
        var result = _sut.ResolveMiss(attacker, map, [attacker, ..blockers]);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void ResolveMiss_WhenTheAttackerIsDestroyed_ReturnsNull()
    {
        // Arrange
        var map = CreateMap();
        var attacker = CreateMech(new HexPosition(AttackerCoordinates, ChargeDirection));
        Destroy(attacker);

        // Act
        var result = _sut.ResolveMiss(attacker, map, [attacker]);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void ResolveAdvance_WhenTheAttackerIsDestroyed_ReturnsNull()
    {
        // Arrange
        var attacker = CreateMech(new HexPosition(AttackerCoordinates, ChargeDirection));
        MoveToChargeApproach(attacker, new HexPosition(TargetCoordinates, ChargeDirection));
        Destroy(attacker);

        // Act
        var result = _sut.ResolveAdvance(attacker);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void ResolveAdvance_MovesTheAttackerToTheCommittedDestination()
    {
        // Arrange
        var attacker = CreateMech(new HexPosition(AttackerCoordinates, ChargeDirection));
        MoveToChargeApproach(attacker, new HexPosition(TargetCoordinates, ChargeDirection));

        // Act
        var result = _sut.ResolveAdvance(attacker);

        // Assert
        result.ShouldNotBeNull();
        result.TargetDestination.ShouldBeNull();
        result.AttackerDestination!.Coordinates.ShouldBe(TargetCoordinates);
    }

    [Fact]
    public void ResolveHit_WhenTheOccupiedHexCannotBeDislodged_UsesAFallbackHex()
    {
        // Arrange: the occupant's own destination is too high, so the domino chain is blocked
        var map = CreateMap();
        var attacker = CreateMech(new HexPosition(AttackerCoordinates, ChargeDirection));
        var target = CreateMech(new HexPosition(TargetCoordinates, HexDirection.Bottom));
        var primaryCoordinates = TargetCoordinates.GetNeighbour(ChargeDirection);
        var occupant = CreateMech(new HexPosition(primaryCoordinates, HexDirection.Bottom));
        MoveToChargeApproach(attacker, target.Position!);
        map.AddHex(new Hex(primaryCoordinates.GetNeighbour(ChargeDirection), 3));

        // Act
        var result = _sut.ResolveHit(attacker, target, map, [attacker, target, occupant], Guid.NewGuid());

        // Assert
        result.ShouldNotBeNull();
        result.DominoEffect.ShouldBeNull();
        result.TargetDestination!.Coordinates.ShouldBe(
            TargetCoordinates.GetNeighbour(ChargeDirection.Rotate(1)));
        result.AttackerDestination!.Coordinates.ShouldBe(TargetCoordinates);
    }

    [Fact]
    public void ResolveHit_WithoutACommittedMovement_ReturnsNull()
    {
        // Arrange
        var map = CreateMap();
        var attacker = CreateMech(new HexPosition(AttackerCoordinates, ChargeDirection));
        var target = CreateMech(new HexPosition(TargetCoordinates, HexDirection.Bottom));

        // Act
        var result = _sut.ResolveHit(attacker, target, map, [attacker, target]);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void ResolveAdvance_WithoutACommittedMovement_ReturnsNull()
    {
        // Arrange
        var attacker = CreateMech(new HexPosition(AttackerCoordinates, ChargeDirection));

        // Act
        var result = _sut.ResolveAdvance(attacker);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void ResolveMiss_WhenTheAttackerHasNoPosition_ReturnsNull()
    {
        // Arrange
        var map = CreateMap();
        var attacker = CreateMechWithoutDeployment();

        // Act
        var result = _sut.ResolveMiss(attacker, map, [attacker]);

        // Assert
        result.ShouldBeNull();
    }

    private static IEnumerable<HexDirection> GetSideDirections(HexDirection direction)
    {
        yield return direction.Rotate(1);
        yield return direction.Rotate(-1);
        yield return direction.Rotate(2);
        yield return direction.Rotate(-2);
    }

    private static void MoveToChargeApproach(IUnit attacker, HexPosition destination)
    {
        attacker.Move(
            new MovementPath(
                [new PathSegment(
                    attacker.Position!,
                    destination,
                    [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }])],
                MovementType.Walk),
            null,
            true,
            attacker.Position);
    }

    private static void Destroy(IUnit unit) =>
        unit.ApplyDamage(
            [new Sanet.MakaMek.Core.Data.Game.LocationHitData(
                [new Sanet.MakaMek.Core.Data.Game.LocationDamageData(PartLocation.Head, 0, 50, true)],
                [], [3, 4], PartLocation.Head)],
            HitDirection.Front);

    private static IBattleMap CreateMap() =>
        new BattleMapFactory().GenerateMap(10, 10,
            new SingleTerrainGenerator(10, 10, new ClearTerrain()));

    private static Mech CreateMech(HexPosition position)
    {
        var mech = CreateMechWithoutDeployment();
        mech.Deploy(position, null);
        return mech;
    }

    private static Mech CreateMechWithoutDeployment()
    {
        var factory = new MechFactory(
            new TotalWarfareRulesProvider(),
            new ClassicBattletechComponentProvider(),
            Substitute.For<ILocalizationService>());
        var data = MechFactoryTests.CreateDummyMechData();
        data.Id = Guid.NewGuid();
        var mech = factory.Create(data);
        mech.AssignPilot(new MechWarrior("Test", "Pilot", gunnery: 4, piloting: 5));
        return mech;
    }
}
