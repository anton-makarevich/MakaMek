using Microsoft.Extensions.Logging;
using NSubstitute;
using Sanet.MakaMek.Bots.Data;
using Sanet.MakaMek.Bots.Models;
using Sanet.MakaMek.Bots.Models.DecisionEngines;
using Sanet.MakaMek.Bots.Models.Movement;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Models.MovementCosts;
using Sanet.MakaMek.Map.Models.Terrains;
using Shouldly;

namespace Sanet.MakaMek.Bots.Tests.Models.Movement;

public class MovementPathPlannerTests
{
    private static readonly HexPosition StartPosition = new(new HexCoordinates(1, 1), HexDirection.Top);
    private static readonly HexPosition Destination = new(new HexCoordinates(2, 1), HexDirection.Top);

    private readonly IClientGame _clientGame = Substitute.For<IClientGame>();
    private readonly IMoveCandidateProvider _candidateProvider = Substitute.For<IMoveCandidateProvider>();
    private readonly ITacticalEvaluator _tacticalEvaluator = Substitute.For<ITacticalEvaluator>();
    private readonly IUnit _unit = Substitute.For<IUnit>();
    private readonly MovementPathPlanner _sut;

    public MovementPathPlannerTests()
    {
        _clientGame.Logger.Returns(Substitute.For<ILogger>());
        _unit.Name.Returns("Test Unit");
        _sut = new MovementPathPlanner(_clientGame, _candidateProvider, _tacticalEvaluator);
    }

    [Fact]
    public void SelectBestPath_WhenWalkAndRunTie_ShouldPreferWalk()
    {
        // Arrange - Run is supplied last so an unordered score collection surfaces it first;
        // the selection must still prefer Walk when every score is tied.
        var walkPath = CreatePath(MovementType.Walk);
        var runPath = CreatePath(MovementType.Run);
        var candidates = new List<MovementPath> { walkPath, runPath };
        _candidateProvider.GetCandidatePaths(_unit, Arg.Any<IReadOnlySet<HexCoordinates>>())
            .Returns(candidates);

        _tacticalEvaluator
            .EvaluatePath(_unit, Arg.Any<MovementPath>(), Arg.Any<IReadOnlyList<IUnit>>(), Arg.Any<ITurnState>())
            .Returns(callInfo =>
            {
                var path = callInfo.ArgAt<MovementPath>(1);
                return new PositionScore
                {
                    Position = Destination,
                    MovementType = path.MovementType,
                    Path = path,
                    OffensiveIndex = 10,
                    DefensiveIndex = 5,
                    EnemiesInRearArc = 0
                };
            });

        // Act
        // Repeated because path scores are collected concurrently, so a tie is resolved
        // in an arbitrary order unless the movement type breaks it.
        for (var i = 0; i < 100; i++)
        {
            var result = _sut.SelectBestPath(_unit, [], new HashSet<HexCoordinates>(), null, new BotSettings(1.0f));

            // Assert - a full tie keeps the historical preference for the cheaper movement type
            result.ShouldBeSameAs(walkPath);
        }
    }

    [Fact]
    public void SelectBestPath_WhenNoCandidateScores_ShouldReturnNull()
    {
        // Arrange
        _candidateProvider.GetCandidatePaths(_unit, Arg.Any<IReadOnlySet<HexCoordinates>>())
            .Returns(new List<MovementPath>());

        // Act
        var result = _sut.SelectBestPath(_unit, [], new HashSet<HexCoordinates>(), null, new BotSettings(1.0f));

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void SelectBestPath_WhenOnePathEvaluationThrows_ShouldStillUseTheRemainingScores()
    {
        // Arrange
        var failingPath = CreatePath(MovementType.Run);
        var validPath = CreatePath(MovementType.Walk);
        _candidateProvider.GetCandidatePaths(_unit, Arg.Any<IReadOnlySet<HexCoordinates>>())
            .Returns(new List<MovementPath> { failingPath, validPath });

        _tacticalEvaluator
            .EvaluatePath(_unit, failingPath, Arg.Any<IReadOnlyList<IUnit>>(), Arg.Any<ITurnState>())
            .Returns(_ => throw new InvalidOperationException("Path evaluation failed"));
        _tacticalEvaluator
            .EvaluatePath(_unit, validPath, Arg.Any<IReadOnlyList<IUnit>>(), Arg.Any<ITurnState>())
            .Returns(new PositionScore
            {
                Position = Destination,
                MovementType = MovementType.Walk,
                Path = validPath,
                OffensiveIndex = 10,
                DefensiveIndex = 5,
                EnemiesInRearArc = 0
            });

        // Act
        var result = _sut.SelectBestPath(_unit, [], new HashSet<HexCoordinates>(), null, new BotSettings(1.0f));

        // Assert - a single failed evaluation must not discard the paths that scored successfully
        result.ShouldBeSameAs(validPath);
    }

    [Fact]
    public void SelectBestPath_WhenEveryPathEvaluationThrows_ShouldReturnNull()
    {
        // Arrange
        var failingPath = CreatePath(MovementType.Walk);
        _candidateProvider.GetCandidatePaths(_unit, Arg.Any<IReadOnlySet<HexCoordinates>>())
            .Returns(new List<MovementPath> { failingPath });

        _tacticalEvaluator
            .EvaluatePath(_unit, failingPath, Arg.Any<IReadOnlyList<IUnit>>(), Arg.Any<ITurnState>())
            .Returns(_ => throw new InvalidOperationException("Path evaluation failed"));

        // Act
        var result = _sut.SelectBestPath(_unit, [], new HashSet<HexCoordinates>(), null, new BotSettings(1.0f));

        // Assert
        result.ShouldBeNull();
    }

    private static MovementPath CreatePath(MovementType movementType)
    {
        var segment = new PathSegment(
            StartPosition,
            Destination,
            [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }]);

        return new MovementPath([segment], movementType);
    }
}
