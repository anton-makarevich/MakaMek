using NSubstitute;
using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Data.Game.Mechanics.PhysicalAttacks;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Displacement;
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
using Sanet.MakaMek.Map.Models.Terrains;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Mechanics.PhysicalAttack;

public class DfaDisplacementResolverTests
{
    private static readonly HexCoordinates TargetCoordinates = new(5, 5);
    // Two hexes south of the target: the push line and the attack direction both come from here.
    private static readonly HexCoordinates StartCoordinates = new(5, 3);

    private readonly DfaDisplacementResolver _sut =
        new(new TotalWarfareRulesProvider(),
            new DominoEffectResolver(Substitute.For<IDiceRoller>()));

    [Fact]
    public void ResolveHit_DisplacesTheTargetAwayFromTheJumpOriginAndLandsTheAttackerInItsHex()
    {
        var map = CreateMap();
        var attacker = CreateMech(new HexPosition(new HexCoordinates(5, 4), HexDirection.Top));
        var target = CreateMech(new HexPosition(TargetCoordinates, HexDirection.Top));

        var result = _sut.ResolveHit(
            attacker, target, StartCoordinates, map, [attacker, target], Guid.NewGuid());

        result.ShouldNotBeNull();
        // The jump origin (5,3) sits north of the target, so the push continues south to (5,6).
        result.TargetDestination!.Coordinates.ShouldBe(new HexCoordinates(5, 6));
        result.TargetDestination.Facing.ShouldBe(target.Position!.Facing);
        result.AttackerDestination.Coordinates.ShouldBe(TargetCoordinates);
        result.DominoEffect.ShouldBeNull();
        result.TriggersAutomaticFall.ShouldBeFalse();
    }

    [Fact]
    public void ResolveHit_WhenTheIdealHexIsOffMap_FallsBackToAnAdjacentOpenHex()
    {
        var map = CreateMap();
        var attacker = CreateMech(new HexPosition(new HexCoordinates(5, 2), HexDirection.Top));
        var target = CreateMech(new HexPosition(new HexCoordinates(5, 1), HexDirection.Top));

        var result = _sut.ResolveHit(
            attacker, target, new HexCoordinates(5, 3), map, [attacker, target], Guid.NewGuid());

        result.ShouldNotBeNull();
        result.TargetDestination.ShouldNotBeNull();
        result.TargetDestination.Coordinates.ShouldNotBe(new HexCoordinates(5, 0));
        map.IsOnMap(result.TargetDestination.Coordinates).ShouldBeTrue();
        result.TargetDestination.Coordinates.DistanceTo(new HexCoordinates(5, 1)).ShouldBe(1);
        result.AttackerDestination.Coordinates.ShouldBe(new HexCoordinates(5, 1));
    }

    [Fact]
    public void ResolveHit_WhenTheIdealHexIsImpassable_FallsBackToTheClosestOpenHex()
    {
        var map = CreateMap();
        var ideal = new HexCoordinates(5, 6);
        map.AddHex(new Hex(ideal, 3));
        var attacker = CreateMech(new HexPosition(new HexCoordinates(5, 4), HexDirection.Top));
        var target = CreateMech(new HexPosition(TargetCoordinates, HexDirection.Top));

        var result = _sut.ResolveHit(
            attacker, target, StartCoordinates, map, [attacker, target], Guid.NewGuid());

        result.ShouldNotBeNull();
        result.TargetDestination!.Coordinates.ShouldNotBe(ideal);
        map.GetHex(result.TargetDestination.Coordinates)!.Level.ShouldBeLessThanOrEqualTo(3);
        result.AttackerDestination.Coordinates.ShouldBe(TargetCoordinates);
    }

    [Fact]
    public void ResolveHit_WhenTheIdealHexIsMoreThanTwoLevelsLower_DisplacesTheTargetAndTriggersAutomaticFall()
    {
        var map = CreateMap();
        var ideal = new HexCoordinates(5, 6);
        map.AddHex(new Hex(ideal, -3));
        var attacker = CreateMech(new HexPosition(new HexCoordinates(5, 4), HexDirection.Top));
        var target = CreateMech(new HexPosition(TargetCoordinates, HexDirection.Top));

        var result = _sut.ResolveHit(
            attacker, target, StartCoordinates, map, [attacker, target], Guid.NewGuid());

        result.ShouldNotBeNull();
        result.TargetDestination!.Coordinates.ShouldBe(ideal);
        result.LevelsFallen.ShouldBe(3);
        result.TriggersAutomaticFall.ShouldBeTrue();
    }

    [Fact]
    public void ResolveHit_WhenTheIdealHexIsOccupied_ResolvesTheDominoChain()
    {
        var map = CreateMap();
        var attacker = CreateMech(new HexPosition(new HexCoordinates(5, 4), HexDirection.Top));
        var target = CreateMech(new HexPosition(TargetCoordinates, HexDirection.Top));
        var occupant = CreateMech(new HexPosition(new HexCoordinates(5, 6), HexDirection.Top));

        var result = _sut.ResolveHit(
            attacker, target, StartCoordinates, map, [attacker, target, occupant], Guid.NewGuid());

        result.ShouldNotBeNull();
        result.DominoEffect.ShouldNotBeNull().HasDisplacements.ShouldBeTrue();
        result.DominoEffect!.Commands.ShouldHaveSingleItem().UnitId.ShouldBe(occupant.Id);
        result.TargetDestination!.Coordinates.ShouldBe(new HexCoordinates(5, 6));
    }

    [Fact]
    public void ResolveHit_WhenTheIdealHexIsOccupiedButTheDominoIsBlocked_FallsBackToAnOpenHex()
    {
        var map = CreateMap();
        var ideal = new HexCoordinates(5, 6);
        // The domino's first link would climb three levels, so it cannot vacate the ideal hex.
        map.AddHex(new Hex(ideal.GetNeighbour(HexDirection.Bottom), 3));
        var attacker = CreateMech(new HexPosition(new HexCoordinates(5, 4), HexDirection.Top));
        var target = CreateMech(new HexPosition(TargetCoordinates, HexDirection.Top));
        var occupant = CreateMech(new HexPosition(ideal, HexDirection.Top));

        var result = _sut.ResolveHit(
            attacker, target, StartCoordinates, map, [attacker, target, occupant], Guid.NewGuid());

        result.ShouldNotBeNull();
        result.TargetDestination!.Coordinates.ShouldNotBe(ideal);
        result.TargetDestination.Coordinates.DistanceTo(TargetCoordinates).ShouldBe(1);
        result.DominoEffect.ShouldBeNull();
        result.AttackerDestination.Coordinates.ShouldBe(TargetCoordinates);
    }

    [Fact]
    public void ResolveHit_WhenTheIdealHexIsOffMapAndTheClosestHexIsOccupied_SkipsTheOccupant()
    {
        var map = CreateMap();
        var targetCoordinates = new HexCoordinates(5, 1);
        var attacker = CreateMech(new HexPosition(new HexCoordinates(5, 2), HexDirection.Top));
        var target = CreateMech(new HexPosition(targetCoordinates, HexDirection.Top));
        // The push away from (5,3) is towards (5,0), off the map. The best hexes still on the map
        // that follow it are the bottom pair; make the first of them occupied.
        var occupiedFallback = targetCoordinates.GetNeighbour(HexDirection.BottomRight);
        var occupant = CreateMech(new HexPosition(occupiedFallback, HexDirection.Top));

        var result = _sut.ResolveHit(
            attacker, target, new HexCoordinates(5, 3), map,
            [attacker, target, occupant], Guid.NewGuid());

        // A fallback hex must be genuinely open: the occupant is not displaced from it.
        result.ShouldNotBeNull();
        result.TargetDestination!.Coordinates.ShouldNotBe(occupiedFallback);
        result.TargetDestination.Coordinates.ShouldBe(
            targetCoordinates.GetNeighbour(HexDirection.BottomLeft));
        result.DominoEffect.ShouldBeNull();
    }

    [Fact]
    public void ResolveHit_WhenTheTargetIsDestroyed_LeavesItInPlaceAndLandsTheAttackerInItsHex()
    {
        var map = CreateMap();
        var attacker = CreateMech(new HexPosition(new HexCoordinates(5, 4), HexDirection.Top));
        var target = CreateMech(new HexPosition(TargetCoordinates, HexDirection.Top));
        target.MarkDestroyed();

        var result = _sut.ResolveHit(
            attacker, target, StartCoordinates, map, [attacker, target], Guid.NewGuid());

        result.ShouldNotBeNull();
        result.TargetDestination.ShouldBeNull();
        result.AttackerDestination.Coordinates.ShouldBe(TargetCoordinates);
    }

    [Fact]
    public void ResolveHit_WhenTheAttackerHasNoPosition_ReturnsNull()
    {
        var map = CreateMap();
        var attacker = CreateMech(new HexPosition(new HexCoordinates(5, 4), HexDirection.Top));
        attacker.RemoveFromBoard();
        var target = CreateMech(new HexPosition(TargetCoordinates, HexDirection.Top));

        var result = _sut.ResolveHit(
            attacker, target, StartCoordinates, map, [attacker, target], Guid.NewGuid());

        result.ShouldBeNull();
    }

    [Fact]
    public void ResolveHit_WhenTheTargetHasNoPosition_ReturnsNull()
    {
        var map = CreateMap();
        var attacker = CreateMech(new HexPosition(new HexCoordinates(5, 4), HexDirection.Top));
        var target = CreateMech(new HexPosition(TargetCoordinates, HexDirection.Top));
        target.RemoveFromBoard();

        var result = _sut.ResolveHit(
            attacker, target, StartCoordinates, map, [attacker, target], Guid.NewGuid());

        result.ShouldBeNull();
    }

    [Fact]
    public void ResolveMiss_WhenTheTargetHasNoPosition_ReturnsNull()
    {
        var map = CreateMap();
        var target = CreateMech(new HexPosition(TargetCoordinates, HexDirection.Top));
        target.RemoveFromBoard();

        var candidate = _sut.ResolveMiss(target, StartCoordinates, map, [target]);

        candidate.ShouldBeNull();
    }

    [Fact]
    public void ResolveMiss_PicksTheFirstFreeHexAwayFromTheJumpOrigin()
    {
        var map = CreateMap();
        var target = CreateMech(new HexPosition(TargetCoordinates, HexDirection.Top));

        var candidate = _sut.ResolveMiss(target, StartCoordinates, map, [target]);

        // The origin (5,3) is north of the target, so the miss displaces it south to (5,6),
        // exactly where a hit would push it.
        candidate.ShouldNotBeNull();
        candidate.Coordinates.ShouldBe(new HexCoordinates(5, 6));
        candidate.Surface.ShouldBe(HexSurface.Ground);
    }

    [Fact]
    public void ResolveMiss_SkipsTheIdealHexWhenItIsOccupied()
    {
        var map = CreateMap();
        var target = CreateMech(new HexPosition(TargetCoordinates, HexDirection.Top));
        var occupant = CreateMech(new HexPosition(new HexCoordinates(5, 6), HexDirection.Top));

        var candidate = _sut.ResolveMiss(target, StartCoordinates, map, [target, occupant]);

        candidate.ShouldNotBeNull();
        candidate.Coordinates.ShouldNotBe(new HexCoordinates(5, 6));
        candidate.Coordinates.DistanceTo(TargetCoordinates).ShouldBe(1);
    }

    [Fact]
    public void ResolveMiss_WhenOnlyAnAirborneDfaAttackerHoldsTheApproachHex_UsesThatHex()
    {
        var map = CreateMap();
        var target = CreateMech(new HexPosition(TargetCoordinates, HexDirection.Top));
        var approach = new HexCoordinates(5, 6);
        var dfaAttacker = CreateMech(new HexPosition(approach, HexDirection.Bottom, AirborneLevel: 4));
        dfaAttacker.DeclarePhysicalAttack(new PhysicalAttackDeclarationData
        {
            AttackType = PhysicalAttackType.DFA,
            AttackerLimbs = [],
            TargetId = target.Id
        });

        var candidate = _sut.ResolveMiss(target, StartCoordinates, map, [target, dfaAttacker]);

        // An airborne pending-DFA unit is not a ground occupant (Total Warfare, p. 149).
        candidate.ShouldNotBeNull();
        candidate.Coordinates.ShouldBe(approach);
    }

    [Fact]
    public void ResolveMiss_WhenOnlyADeepDropHexIsAvailable_ReturnsItAndFlagsTheFall()
    {
        var map = CreateMap();
        var target = CreateMech(new HexPosition(TargetCoordinates, HexDirection.Top));
        var deepDrop = new HexCoordinates(5, 6);
        map.AddHex(new Hex(deepDrop, -3));
        var occupants = new List<IUnit> { target };
        foreach (var direction in HexDirectionExtensions.AllDirections)
        {
            var neighbour = TargetCoordinates.GetNeighbour(direction);
            if (neighbour == deepDrop) continue;
            occupants.Add(CreateMech(new HexPosition(neighbour, HexDirection.Top)));
        }

        var candidate = _sut.ResolveMiss(target, StartCoordinates, map, occupants);

        candidate.ShouldNotBeNull();
        candidate.Coordinates.ShouldBe(deepDrop);
        candidate.LevelsFallen.ShouldBe(3);
        candidate.TriggersAutomaticFall.ShouldBeTrue();
    }

    [Fact]
    public void ResolveMiss_WhenEveryNeighbourIsTaken_ReturnsNull()
    {
        var map = CreateMap();
        var target = CreateMech(new HexPosition(TargetCoordinates, HexDirection.Top));
        var occupants = HexDirectionExtensions.AllDirections
            .Select(direction => CreateMech(new HexPosition(TargetCoordinates.GetNeighbour(direction), HexDirection.Top)))
            .ToList<IUnit>();
        occupants.Add(target);

        var candidate = _sut.ResolveMiss(target, StartCoordinates, map, occupants);

        candidate.ShouldBeNull();
    }

    [Fact]
    public void ResolveLanding_WhenTheHexIsFree_GroundsTheAttacker()
    {
        var map = CreateMap();
        var attacker = CreateMech(new HexPosition(new HexCoordinates(5, 4), HexDirection.Top, AirborneLevel: 4));

        var result = _sut.ResolveLanding(
            attacker, new HexPosition(new HexCoordinates(5, 5), HexDirection.Top), StartCoordinates, map,
            [attacker]);

        result.ShouldNotBeNull();
        result.Destination.Coordinates.ShouldBe(new HexCoordinates(5, 5));
        result.Destination.AirborneLevel.ShouldBeNull();
        result.DominoEffect.ShouldBeNull();
    }

    [Fact]
    public void ResolveLanding_WhenTheAttackerHasNoPosition_ReturnsNull()
    {
        var map = CreateMap();
        var attacker = CreateMech(new HexPosition(new HexCoordinates(5, 4), HexDirection.Top));
        attacker.RemoveFromBoard();

        var result = _sut.ResolveLanding(
            attacker, new HexPosition(new HexCoordinates(5, 5), HexDirection.Top), StartCoordinates, map,
            [attacker]);

        result.ShouldBeNull();
    }

    [Fact]
    public void ResolveLanding_WhenTheLandingHexIsOffTheMap_ReturnsNull()
    {
        var map = CreateMap();
        var attacker = CreateMech(new HexPosition(new HexCoordinates(5, 4), HexDirection.Top, AirborneLevel: 4));

        var result = _sut.ResolveLanding(
            attacker, new HexPosition(new HexCoordinates(-1, -1), HexDirection.Top), StartCoordinates, map,
            [attacker]);

        result.ShouldBeNull();
    }

    [Fact]
    public void ResolveLanding_WhenTheHexIsOccupied_ForcesTheOccupantOut()
    {
        var map = CreateMap();
        var attacker = CreateMech(new HexPosition(new HexCoordinates(5, 4), HexDirection.Top, AirborneLevel: 4));
        var occupant = CreateMech(new HexPosition(new HexCoordinates(5, 5), HexDirection.Top));

        var result = _sut.ResolveLanding(
            attacker,
            new HexPosition(new HexCoordinates(5, 5), HexDirection.Top),
            StartCoordinates,
            map,
            [attacker, occupant]);

        result.ShouldNotBeNull();
        result.DominoEffect.ShouldNotBeNull().HasDisplacements.ShouldBeTrue();
        result.Destination.Coordinates.ShouldBe(new HexCoordinates(5, 5));
        // The landing is the attack, so knocking the occupant out is its first displacement.
        result.DominoEffect!.Commands.ShouldHaveSingleItem().DisplacementReason
            .ShouldBe(DisplacementReason.DfaLandingOccupant);
    }

    [Fact]
    public void ResolveLanding_WhenTheHexIsOccupied_ForcesTheOccupantAwayFromTheJumpOrigin()
    {
        var map = CreateMap();
        // The jump runs (4,3) -> approach (5,4) -> landing (5,5). Measured from the approach hex
        // the occupant would be forced south to (5,6); measured from the jump origin it is forced
        // south-east to (6,5) (Total Warfare, p. 150).
        var attacker = CreateMech(new HexPosition(new HexCoordinates(5, 4), HexDirection.Top, AirborneLevel: 4));
        var occupant = CreateMech(new HexPosition(new HexCoordinates(5, 5), HexDirection.Top));

        var result = _sut.ResolveLanding(
            attacker,
            new HexPosition(new HexCoordinates(5, 5), HexDirection.Top),
            new HexCoordinates(4, 3),
            map,
            [attacker, occupant]);

        result.ShouldNotBeNull();
        result.DominoEffect.ShouldNotBeNull().HasDisplacements.ShouldBeTrue();
        var displacement = result.DominoEffect!.Commands.ShouldHaveSingleItem();
        displacement.UnitId.ShouldBe(occupant.Id);
        displacement.ToCoordinates.ShouldBe(new HexCoordinates(6, 5).ToData());
    }

    [Fact]
    public void ResolveLanding_WhenTheLandingHexIsAboveTheClimbLimit_StillLandsAndDisplacesTheOccupant()
    {
        var map = CreateMap();
        var landing = new HexCoordinates(5, 5);
        map.AddHex(new Hex(landing, 3));
        // The jump into a Level 3 hex is legal with enough Jump MP (Total Warfare p. 149), so the
        // landing is not a forced climb from the Level 0 approach hex.
        var attacker = CreateMech(new HexPosition(new HexCoordinates(5, 4), HexDirection.Top, AirborneLevel: 5));
        var occupant = CreateMech(new HexPosition(landing, HexDirection.Top));

        var result = _sut.ResolveLanding(
            attacker, new HexPosition(landing, HexDirection.Top), StartCoordinates, map, [attacker, occupant]);

        result.ShouldNotBeNull();
        result.IsOverlapUnresolved.ShouldBeFalse();
        result.Destination.Coordinates.ShouldBe(landing);
        result.Destination.Surface.ShouldBe(HexSurface.Ground);
        result.Destination.AirborneLevel.ShouldBeNull();
        result.DominoEffect.ShouldNotBeNull().HasDisplacements.ShouldBeTrue();
        result.DominoEffect!.Commands.ShouldHaveSingleItem().UnitId.ShouldBe(occupant.Id);
        // The landing is the attack, so knocking the occupant out is its first displacement.
        result.DominoEffect.Commands[0].DisplacementReason.ShouldBe(DisplacementReason.DfaLandingOccupant);
    }

    [Fact]
    public void ResolveLanding_WhenTheApproachHexIsHigher_DoesNotFallFromAbove()
    {
        var map = CreateMap();
        var landing = new HexCoordinates(5, 5);
        map.AddHex(new Hex(new HexCoordinates(5, 4), 2));
        // The attacker descends from the air, not from the approach hex's ground level, so a drop of
        // two or more levels is not an accidental fall from above onto the occupant.
        var attacker = CreateMech(new HexPosition(new HexCoordinates(5, 4), HexDirection.Top, AirborneLevel: 4));
        var occupant = CreateMech(new HexPosition(landing, HexDirection.Top));

        var result = _sut.ResolveLanding(
            attacker, new HexPosition(landing, HexDirection.Top), StartCoordinates, map, [attacker, occupant]);

        result.ShouldNotBeNull();
        result.IsOverlapUnresolved.ShouldBeFalse();
        result.Destination.AirborneLevel.ShouldBeNull();
        result.DominoEffect.ShouldNotBeNull().HasDisplacements.ShouldBeTrue();
        result.DominoEffect!.Commands.ShouldHaveSingleItem().UnitId.ShouldBe(occupant.Id);
        result.DominoEffect.Commands[0].DisplacementReason.ShouldBe(DisplacementReason.DfaLandingOccupant);
    }

    [Fact]
    public void ResolveLanding_WhenTheOccupantCannotBeForcedOut_ReportsAnUnresolvedOverlap()
    {
        var map = CreateMap();
        var attackerPosition = new HexPosition(new HexCoordinates(5, 5), HexDirection.Top, AirborneLevel: 4);
        var attacker = CreateMech(attackerPosition);
        var occupant = CreateMech(new HexPosition(new HexCoordinates(5, 5), HexDirection.Top));
        // The domino's first link would climb three levels, so it is blocked. A DFA lands in the
        // target hex and has no side-hex fallback (that is the charge miss rule), so the overlap
        // stays unresolved instead of stacking the two units (Total Warfare, p. 150).
        map.AddHex(new Hex(new HexCoordinates(5, 4), 3));

        var result = _sut.ResolveLanding(
            attacker, attackerPosition, new HexCoordinates(5, 6), map, [attacker, occupant]);

        result.ShouldNotBeNull();
        result.Destination.Coordinates.ShouldBe(new HexCoordinates(5, 5));
        result.DominoEffect.ShouldBeNull();
        result.IsOverlapUnresolved.ShouldBeTrue();
    }

    private static IBattleMap CreateMap() =>
        new BattleMapFactory().GenerateMap(10, 10,
            new SingleTerrainGenerator(10, 10, new ClearTerrain()));

    private static Mech CreateMech(HexPosition position)
    {
        var factory = new MechFactory(
            new TotalWarfareRulesProvider(),
            new ClassicBattletechComponentProvider(),
            Substitute.For<ILocalizationService>());
        var data = MechFactoryTests.CreateDummyMechData();
        data.Id = Guid.NewGuid();
        var mech = factory.Create(data);
        mech.AssignPilot(new MechWarrior("Test", "Pilot", gunnery: 4, piloting: 5));
        mech.Deploy(position, null);
        return mech;
    }
}
