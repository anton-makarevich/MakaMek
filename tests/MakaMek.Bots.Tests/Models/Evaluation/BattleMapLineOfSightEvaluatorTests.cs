using NSubstitute;
using Sanet.MakaMek.Bots.Models.Evaluation;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Map.Models;
using Shouldly;

namespace Sanet.MakaMek.Bots.Tests.Models.Evaluation;

public class BattleMapLineOfSightEvaluatorTests
{
    private static readonly HexPosition Origin =
        new(new HexCoordinates(1, 1), HexDirection.Bottom);
    private static readonly HexPosition Target =
        new(new HexCoordinates(1, 4), HexDirection.Top);

    private readonly IClientGame _game = Substitute.For<IClientGame>();
    private readonly IBattleMap _battleMap = Substitute.For<IBattleMap>();
    private readonly BattleMapLineOfSightEvaluator _sut;

    public BattleMapLineOfSightEvaluatorTests()
    {
        _sut = new BattleMapLineOfSightEvaluator(_game);
    }

    [Fact]
    public void HasLineOfSight_WhenThereIsNoBattleMap_ShouldReturnFalse()
    {
        // Arrange
        _game.BattleMap.Returns((IBattleMap?)null);

        // Act
        var result = _sut.HasLineOfSight(Origin, Target, 2, 2);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void HasLineOfSight_ShouldReturnTheBattleMapsVerdict()
    {
        // Arrange
        _game.BattleMap.Returns(_battleMap);
        _battleMap.GetLineOfSight(Origin, Target, 2, 2)
            .Returns(LineOfSightResult.Blocked(
                Origin.Coordinates, Target.Coordinates, new HexCoordinates(1, 2)));

        // Act
        var result = _sut.HasLineOfSight(Origin, Target, 2, 2);

        // Assert
        result.ShouldBeFalse();
        _battleMap.Received(1).GetLineOfSight(Origin, Target, 2, 2);
    }
}
