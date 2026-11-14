using NSubstitute;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons.Energy;
using Sanet.MakaMek.Map.Factories;
using Sanet.MakaMek.Map.Generators;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Models.Terrains;
using Sanet.MakaMek.Presentation.UiStates;
using Shouldly;

namespace Sanet.MakaMek.Presentation.Tests.UiStates;

/// <summary>
/// Regression tests for the range/highlight computation that <see cref="WeaponRangeAnalyzer"/> took over
/// from the body of <c>WeaponsAttackState.HighlightWeaponRanges</c> when the complexity refactor split it
/// up. The state-level tests assert the resulting map highlights; these pin the analyzer contract.
/// </summary>
public class WeaponRangeAnalyzerTests
{
    private static readonly IBattleMapFactory BattleMapFactory = new BattleMapFactory();

    private readonly WeaponRangeAnalyzer _sut = new();
    private readonly IUnit _attacker = Substitute.For<IUnit>();
    private readonly HexCoordinates _attackerCoordinates = new(1, 1);
    private readonly HexCoordinates _adjacentCoordinates = new(1, 2);
    private readonly HexCoordinates _targetCoordinates = new(1, 3);
    private readonly HexCoordinates _blockingCoordinates = new(1, 2);

    public WeaponRangeAnalyzerTests()
    {
        _attacker.Position.Returns(new HexPosition(_attackerCoordinates, HexDirection.Bottom));
    }

    [Fact]
    public void Analyze_WithoutMap_ReturnsEveryCandidateHex_AndNoLosHighlights()
    {
        // Arrange - a missing map must skip LOS checks entirely, like the uncached pre-move state
        var candidateHexes = new HashSet<HexCoordinates> { _targetCoordinates, new(2, 3) };
        var weapon = new MediumLaser();
        var candidates = new List<WeaponRangeAnalyzer.WeaponCandidate>
        {
            new(weapon, candidateHexes, 0)
        };

        // Act
        var ranges = _sut.Analyze(_attacker, candidates, [], map: null, out var losHighlights);

        // Assert
        ranges.Keys.ShouldBe([weapon]);
        ranges[weapon].ShouldBe(candidateHexes, ignoreOrder: true);
        losHighlights.ShouldBeEmpty();
    }

    [Fact]
    public void Analyze_WithoutCandidates_ReturnsNoRanges_AndNoLosHighlights()
    {
        // Arrange
        var map = CreateMap();

        // Act
        var ranges = _sut.Analyze(_attacker, [], [], map, out var losHighlights);

        // Assert
        ranges.ShouldBeEmpty();
        losHighlights.ShouldBeEmpty();
    }

    [Fact]
    public void Analyze_KeepsHexesWithLineOfSight_AndDoesNotReportThemAsBlocked()
    {
        // Arrange - an adjacent target has no intervening hexes
        var map = CreateMap();
        var weapon = new MediumLaser();
        var candidates = new List<WeaponRangeAnalyzer.WeaponCandidate>
        {
            new(weapon, [_adjacentCoordinates], 0)
        };

        // Act
        var ranges = _sut.Analyze(_attacker, candidates, [], map, out var losHighlights);

        // Assert
        ranges[weapon].ShouldBe([_adjacentCoordinates], ignoreOrder: true);
        losHighlights.ShouldBeEmpty();
    }

    [Fact]
    public void Analyze_RemovesHexesBlockedFromEveryMountLevel_AndHighlightsThem()
    {
        // Arrange - a level-5 hill sits between the attacker and the target hex
        var map = CreateMapWithHill();
        var lowWeapon = new MediumLaser();
        var highWeapon = new MediumLaser();
        var candidates = new List<WeaponRangeAnalyzer.WeaponCandidate>
        {
            new(lowWeapon, [_targetCoordinates], 0),
            new(highWeapon, [_targetCoordinates], 7)
        };

        // Act
        var ranges = _sut.Analyze(_attacker, candidates, [], map, out var losHighlights);

        // Assert
        ranges[lowWeapon].ShouldBeEmpty();
        ranges[highWeapon].ShouldBeEmpty();
        var highlight = losHighlights.ShouldHaveSingleItem();
        highlight.Key.ShouldBe(_targetCoordinates);
        highlight.Value.Reason.ShouldBe(LineOfSightBlockReason.Elevation);
        highlight.Value.BlockingHex.ShouldBe(_blockingCoordinates);
    }

    [Fact]
    public void Analyze_KeepsAHexThatAnyMountLevelCanSee_AndDoesNotHighlightIt()
    {
        // Arrange - the airborne occupant puts the target above the hill, but only the elevated mount
        // has a line of sight to it while the low mount still fails
        var map = CreateMapWithHill();
        var airborne = CreateUnitAt(AirbornePosition());
        var lowWeapon = new MediumLaser();
        var elevatedWeapon = new MediumLaser();
        var candidates = new List<WeaponRangeAnalyzer.WeaponCandidate>
        {
            new(lowWeapon, [_targetCoordinates], 0),
            new(elevatedWeapon, [_targetCoordinates], 7)
        };

        // Act
        var ranges = _sut.Analyze(_attacker, candidates, [airborne], map, out var losHighlights);

        // Assert
        ranges[lowWeapon].ShouldBeEmpty();
        ranges[elevatedWeapon].ShouldBe([_targetCoordinates], ignoreOrder: true);
        losHighlights.ShouldBeEmpty();
    }

    [Fact]
    public void Analyze_UsesTheOccupantWithLineOfSight_WhenTwoUnitsShareAHex()
    {
        // Arrange - a grounded unit behind the hill is blocked, a co-located airborne unit is not
        var map = CreateMapWithHill();
        var grounded = CreateUnitAt(new HexPosition(_targetCoordinates, HexDirection.Bottom));
        var airborne = CreateUnitAt(AirbornePosition());
        var weapon = new MediumLaser();
        var candidates = new List<WeaponRangeAnalyzer.WeaponCandidate>
        {
            new(weapon, [_targetCoordinates], 7)
        };

        // Act
        var ranges = _sut.Analyze(_attacker, candidates, [grounded, airborne], map, out var losHighlights);

        // Assert - the hex is reachable because one occupant is visible
        ranges[weapon].ShouldBe([_targetCoordinates], ignoreOrder: true);
        losHighlights.ShouldBeEmpty();
    }

    [Fact]
    public void Analyze_RemovesOccupiedHexes_WhenEveryOccupantIsBlocked()
    {
        // Arrange - a single grounded occupant behind the hill
        var map = CreateMapWithHill();
        var grounded = CreateUnitAt(new HexPosition(_targetCoordinates, HexDirection.Bottom));
        var weapon = new MediumLaser();
        var candidates = new List<WeaponRangeAnalyzer.WeaponCandidate>
        {
            new(weapon, [_targetCoordinates], 7)
        };

        // Act
        var ranges = _sut.Analyze(_attacker, candidates, [grounded], map, out var losHighlights);

        // Assert
        ranges[weapon].ShouldBeEmpty();
        losHighlights.Keys.ShouldBe([_targetCoordinates], ignoreOrder: true);
    }

    private IBattleMap CreateMapWithHill()
    {
        var map = CreateMap();
        map.GetHex(_blockingCoordinates)!.WithLevel(5);
        return map;
    }

    private HexPosition AirbornePosition() =>
        new HexPosition(_targetCoordinates, HexDirection.Bottom) with { AirborneLevel = 7 };

    private static IBattleMap CreateMap() =>
        BattleMapFactory.GenerateMap(11, 11, new SingleTerrainGenerator(11, 11, new ClearTerrain()));

    private static IUnit CreateUnitAt(HexPosition position)
    {
        var unit = Substitute.For<IUnit>();
        unit.Position.Returns(position);
        return unit;
    }
}
