using NSubstitute;
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
    public void GetMissCandidates_ExcludesOccupiedHexes()
    {
        var map = CreateMap();
        var target = CreateMech(new HexPosition(TargetCoordinates, HexDirection.Top));
        var occupant = CreateMech(new HexPosition(new HexCoordinates(5, 6), HexDirection.Top));

        var candidates = _sut.GetMissCandidates(target, map, [target, occupant]);

        candidates.Count.ShouldBe(5);
        candidates.ShouldNotContain(candidate => candidate.Coordinates == new HexCoordinates(5, 6));
    }

    [Fact]
    public void GetMissCandidates_IncludesTheApproachHexHeldOnlyByAnAirborneDfaAttacker()
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

        var candidates = _sut.GetMissCandidates(target, map, [target, dfaAttacker]);

        candidates.Count.ShouldBe(6);
        candidates.ShouldContain(candidate => candidate.Coordinates == approach);
    }

    [Fact]
    public void ResolveLanding_WhenTheHexIsFree_GroundsTheAttacker()
    {
        var map = CreateMap();
        var attacker = CreateMech(new HexPosition(new HexCoordinates(5, 4), HexDirection.Top, AirborneLevel: 4));

        var result = _sut.ResolveLanding(
            attacker, new HexPosition(new HexCoordinates(5, 5), HexDirection.Top), map, [attacker]);

        result.ShouldNotBeNull();
        result.Destination.Coordinates.ShouldBe(new HexCoordinates(5, 5));
        result.Destination.AirborneLevel.ShouldBeNull();
        result.DominoEffect.ShouldBeNull();
    }

    [Fact]
    public void ResolveLanding_WhenTheHexIsOccupied_RunsTheDominoEffect()
    {
        var map = CreateMap();
        var attacker = CreateMech(new HexPosition(new HexCoordinates(5, 4), HexDirection.Top, AirborneLevel: 4));
        var occupant = CreateMech(new HexPosition(new HexCoordinates(5, 5), HexDirection.Top));

        var result = _sut.ResolveLanding(
            attacker,
            new HexPosition(new HexCoordinates(5, 5), HexDirection.Top),
            map,
            [attacker, occupant]);

        result.ShouldNotBeNull();
        result.DominoEffect.ShouldNotBeNull().HasDisplacements.ShouldBeTrue();
        result.Destination.Coordinates.ShouldBe(new HexCoordinates(5, 5));
    }

    [Fact]
    public void ResolveLanding_WhenTheDominoIsBlocked_UsesTheSideHexPrecedent()
    {
        var map = CreateMap();
        var attackerPosition = new HexPosition(new HexCoordinates(5, 5), HexDirection.Top, AirborneLevel: 4);
        var attacker = CreateMech(attackerPosition);
        var occupant = CreateMech(new HexPosition(new HexCoordinates(5, 5), HexDirection.Top));
        // The domino's first link would climb three levels, so it is blocked and the side hexes
        // are tried in the charge order: right of the approach facing first.
        map.AddHex(new Hex(new HexCoordinates(5, 4), 3));

        var result = _sut.ResolveLanding(
            attacker, attackerPosition, map, [attacker, occupant]);

        result.ShouldNotBeNull();
        result.Destination.Coordinates.ShouldBe(
            new HexCoordinates(5, 5).GetNeighbour(HexDirection.Top.Rotate(1)));
        result.IsOverlapUnresolved.ShouldBeFalse();
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
