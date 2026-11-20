using NSubstitute;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons.Energy;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Presentation.UiStates;
using Shouldly;

namespace Sanet.MakaMek.Presentation.Tests.UiStates;

public class WeaponRangeAnalyzerTests
{
    private static readonly HexCoordinates AttackerHex = new(5, 5);
    private static readonly HexCoordinates FirstHex = new(6, 5);
    private static readonly HexCoordinates SecondHex = new(5, 6);
    private static readonly HexPosition AttackerPosition = new(AttackerHex, HexDirection.Top);

    private readonly IUnit _attacker = Substitute.For<IUnit>();
    private readonly IBattleMap _map = Substitute.For<IBattleMap>();

    public WeaponRangeAnalyzerTests()
    {
        _attacker.Position.Returns(AttackerPosition);
        _attacker.Height.Returns(1);
    }

    [Fact]
    public void Analyze_WithoutAMap_KeepsEveryCandidateHexAndSkipsLosHighlights()
    {
        // Arrange
        var weapon = new MediumLaser();
        var candidate = Candidate(weapon, mountLevel: 2, FirstHex, SecondHex);

        // Act
        var result = _attacker.AnalyzeWeaponRanges([candidate], [], map: null);

        // Assert
        result.Ranges[weapon].ShouldBe([FirstHex, SecondHex], ignoreOrder: true);
        result.LosHighlights.ShouldBeEmpty();
        result.ReachableHighlights.Keys.ShouldBe([FirstHex, SecondHex], ignoreOrder: true);
        result.ReachableHighlights[FirstHex].WeaponNames.ShouldBe(["Medium Laser"]);
    }

    [Fact]
    public void Analyze_WithABlockedHex_RemovesItFromTheRangeAndHighlightsIt()
    {
        // Arrange
        var weapon = new MediumLaser();
        var blockingHex = new HexCoordinates(4, 4);
        StubLineOfSightToHex(
            FirstHex,
            mountLevel: 2,
            LineOfSightResult.Unblocked(AttackerHex, FirstHex));
        StubLineOfSightToHex(
            SecondHex,
            mountLevel: 2,
            LineOfSightResult.Blocked(
                AttackerHex,
                SecondHex,
                blockingHex,
                LineOfSightBlockReason.Elevation));

        // Act
        var result = _attacker.AnalyzeWeaponRanges([Candidate(weapon, 2, FirstHex, SecondHex)], [], _map);

        // Assert
        result.Ranges[weapon].ShouldBe([FirstHex]);
        result.ReachableHighlights.Keys.ShouldBe([FirstHex]);
        var highlight = result.LosHighlights.ShouldHaveSingleItem().Value;
        highlight.Reason.ShouldBe(LineOfSightBlockReason.Elevation);
        highlight.BlockingHex.ShouldBe(blockingHex);
    }

    [Fact]
    public void Analyze_WhenAHexIsBlockedAtOneMountLevelButOpenAtAnother_DoesNotHighlightIt()
    {
        // Arrange: both weapons reach the same hex, but only the lower mount is blocked.
        var mediumLaser = new MediumLaser();
        var smallLaser = new SmallLaser();
        StubLineOfSightToHex(
            FirstHex,
            mountLevel: 1,
            LineOfSightResult.Blocked(
                AttackerHex,
                FirstHex,
                new HexCoordinates(4, 4),
                LineOfSightBlockReason.Elevation));
        StubLineOfSightToHex(
            FirstHex,
            mountLevel: 2,
            LineOfSightResult.Unblocked(AttackerHex, FirstHex));

        // Act
        var result = _attacker.AnalyzeWeaponRanges([Candidate(mediumLaser, 1, FirstHex), Candidate(smallLaser, 2, FirstHex)],
            [],
            _map);

        // Assert: the blocked weapon loses the hex, but the hex is not highlighted because the
        // other mount level can still see it.
        result.Ranges[mediumLaser].ShouldBeEmpty();
        result.Ranges[smallLaser].ShouldContain(FirstHex);
        result.LosHighlights.ShouldBeEmpty();
    }

    [Fact]
    public void Analyze_WhenEveryMountLevelIsBlocked_HighlightsTheHexWithTheFirstLevelReason()
    {
        // Arrange
        var lowerWeapon = new MediumLaser();
        var upperWeapon = new SmallLaser();
        var firstBlockingHex = new HexCoordinates(3, 3);
        var secondBlockingHex = new HexCoordinates(4, 4);
        StubLineOfSightToHex(
            FirstHex,
            mountLevel: 1,
            LineOfSightResult.Blocked(
                AttackerHex,
                FirstHex,
                firstBlockingHex,
                LineOfSightBlockReason.Elevation));
        StubLineOfSightToHex(
            FirstHex,
            mountLevel: 2,
            LineOfSightResult.Blocked(
                AttackerHex,
                FirstHex,
                secondBlockingHex,
                LineOfSightBlockReason.InterveningTerrain));

        // Act
        var result = _attacker.AnalyzeWeaponRanges([Candidate(lowerWeapon, 1, FirstHex), Candidate(upperWeapon, 2, FirstHex)],
            [],
            _map);

        // Assert
        result.Ranges[lowerWeapon].ShouldBeEmpty();
        result.Ranges[upperWeapon].ShouldBeEmpty();
        var highlight = result.LosHighlights.ShouldHaveSingleItem().Value;
        highlight.Reason.ShouldBe(LineOfSightBlockReason.Elevation);
        highlight.BlockingHex.ShouldBe(firstBlockingHex);
    }

    [Fact]
    public void Analyze_WhenAHexHasSeveralOccupants_UsesTheFirstOccupantWithLineOfSight()
    {
        // Arrange: a grounded unit and an airborne unit share the hex. The airborne unit is
        // checked second and has a clear line of sight, so the hex stays reachable.
        var weapon = new MediumLaser();
        var position = new HexPosition(FirstHex, HexDirection.Top);
        var blockedOccupant = Substitute.For<IUnit>();
        blockedOccupant.Position.Returns(position);
        blockedOccupant.Height.Returns(1);
        var openOccupant = Substitute.For<IUnit>();
        openOccupant.Position.Returns(position with { AirborneLevel = 3 });
        openOccupant.Height.Returns(2);
        StubLineOfSightToUnit(
            blockedOccupant,
            mountLevel: 2,
            LineOfSightResult.Blocked(
                AttackerHex,
                FirstHex,
                new HexCoordinates(4, 4),
                LineOfSightBlockReason.Elevation));
        StubLineOfSightToUnit(
            openOccupant,
            mountLevel: 2,
            LineOfSightResult.Unblocked(AttackerHex, FirstHex));

        // Act
        var result = _attacker.AnalyzeWeaponRanges([Candidate(weapon, 2, FirstHex)],
            [_attacker, blockedOccupant, openOccupant],
            _map);

        // Assert: the attacker is not treated as an occupant, and both co-located units are checked.
        result.Ranges[weapon].ShouldContain(FirstHex);
        result.LosHighlights.ShouldBeEmpty();
        _map.Received(1).GetLineOfSight(
            AttackerPosition,
            blockedOccupant.Position!,
            2,
            blockedOccupant.Height);
        _map.Received(1).GetLineOfSight(
            AttackerPosition,
            openOccupant.Position!,
            2,
            openOccupant.Height);
    }

    [Fact]
    public void Analyze_BuildsReachableHighlightsWithSortedWeaponNames()
    {
        // Arrange: the alphabetically later weapon is supplied first.
        var smallLaser = new SmallLaser();
        var mediumLaser = new MediumLaser();

        // Act
        var result = _attacker.AnalyzeWeaponRanges([Candidate(smallLaser, 2, FirstHex), Candidate(mediumLaser, 2, FirstHex)],
            [],
            map: null);

        // Assert
        result.ReachableHighlights[FirstHex].WeaponNames
            .ShouldBe(["Medium Laser", "Small Laser"]);
    }

    private static WeaponRangeAnalyzerExtensions.WeaponCandidate Candidate(
        Weapon weapon,
        int mountLevel,
        params HexCoordinates[] hexes) =>
        new(weapon, hexes.ToHashSet(), mountLevel);

    private void StubLineOfSightToHex(
        HexCoordinates to,
        int mountLevel,
        LineOfSightResult result) =>
        _map.GetLineOfSight(
                AttackerPosition.Coordinates,
                to,
                mountLevel,
                0,
                AttackerPosition.Surface,
                null,
                AttackerPosition.AirborneLevel,
                null)
            .Returns(result);

    private void StubLineOfSightToUnit(
        IUnit unit,
        int mountLevel,
        LineOfSightResult result) =>
        _map.GetLineOfSight(AttackerPosition, unit.Position!, mountLevel, unit.Height)
            .Returns(result);
}
