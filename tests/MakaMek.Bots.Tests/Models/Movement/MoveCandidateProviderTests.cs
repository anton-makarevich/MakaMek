using NSubstitute;
using Sanet.MakaMek.Bots.Models.Movement;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Map.Models;
using Shouldly;

namespace Sanet.MakaMek.Bots.Tests.Models.Movement;

public class MoveCandidateProviderTests
{
    private readonly IClientGame _game = Substitute.For<IClientGame>();
    private readonly IBattleMap _battleMap = Substitute.For<IBattleMap>();
    private readonly IUnit _unit = Substitute.For<IUnit>();
    private readonly MoveCandidateProvider _sut;

    public MoveCandidateProviderTests()
    {
        _game.BattleMap.Returns(_battleMap);
        _sut = new MoveCandidateProvider(_game);
    }

    [Fact]
    public void GetCandidatePaths_WhenThereIsNoBattleMap_ShouldReturnEmpty()
    {
        // Arrange
        _game.BattleMap.Returns((IBattleMap?)null);

        // Act
        var result = _sut.GetCandidatePaths(_unit, new HashSet<HexCoordinates>());

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public void GetCandidatePaths_WhenTheUnitHasNoPosition_ShouldReturnEmpty()
    {
        // Arrange
        _unit.Position.Returns((HexPosition?)null);

        // Act
        var result = _sut.GetCandidatePaths(_unit, new HashSet<HexCoordinates>());

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public void GetCandidatePaths_ShouldPassOnlyOtherDeployedUnitsAsProhibitedHexes()
    {
        // Arrange - the moving unit is not its own obstacle, and units that are not on the board
        // (not deployed, or without a position) must not block pathfinding either.
        var movingPosition = new HexPosition(new HexCoordinates(1, 1), HexDirection.Top);
        var otherUnitPosition = new HexPosition(new HexCoordinates(2, 2), HexDirection.Top);

        var movingUnit = CreateUnit(Guid.NewGuid(), movingPosition);
        movingUnit.GetAvailableMovementTypes().Returns([MovementType.Walk]);
        movingUnit.GetMovementPoints(MovementType.Walk).Returns(4);
        movingUnit.CanMoveBackward(MovementType.Walk).Returns(false);
        movingUnit.Height.Returns(2);
        movingUnit.MaxLevelChangeForward.Returns(1);
        movingUnit.MaxLevelChangeBackward.Returns(1);

        var otherUnit = CreateUnit(Guid.NewGuid(), otherUnitPosition);
        var undeployedUnit = CreateUnit(
            Guid.NewGuid(),
            new HexPosition(new HexCoordinates(3, 3), HexDirection.Top),
            isDeployed: false);
        var unitWithoutPosition = CreateUnit(Guid.NewGuid(), null);

        var player = Substitute.For<IPlayer>();
        player.Units.Returns([movingUnit, otherUnit, undeployedUnit, unitWithoutPosition]);
        _game.Players.Returns([player]);

        var rulesProvider = Substitute.For<IRulesProvider>();
        rulesProvider.GetDfaAirborneBaseOffset().Returns(0);
        _game.RulesProvider.Returns(rulesProvider);

        IReadOnlySet<HexCoordinates>? prohibitedHexes = null;
        _battleMap
            .GetReachableHexes(
                Arg.Any<HexPosition>(),
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<IReadOnlySet<HexCoordinates>?>(),
                Arg.Any<int?>())
            .Returns(callInfo =>
            {
                prohibitedHexes = callInfo.ArgAt<IReadOnlySet<HexCoordinates>?>(3);
                return [];
            });

        // Act
        var result = _sut.GetCandidatePaths(movingUnit, new HashSet<HexCoordinates>());

        // Assert
        result.ShouldBeEmpty();
        prohibitedHexes.ShouldNotBeNull();
        prohibitedHexes!.ShouldBe(new[] { otherUnitPosition.Coordinates });
    }

    private static IUnit CreateUnit(Guid id, HexPosition? position, bool isDeployed = true)
    {
        var unit = Substitute.For<IUnit>();
        unit.Id.Returns(id);
        unit.IsDeployed.Returns(isDeployed);
        unit.Position.Returns(position);
        return unit;
    }
}
