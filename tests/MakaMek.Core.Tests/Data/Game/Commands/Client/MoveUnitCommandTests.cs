using NSubstitute;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Tests.Utils;
using Sanet.MakaMek.Core.Utils;
using Sanet.MakaMek.Localization;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Models.MovementCosts;
using Sanet.MakaMek.Map.Models.Terrains;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Data.Game.Commands.Client;

public class MoveUnitCommandTests
{
    private readonly ILocalizationService _localizationService = Substitute.For<ILocalizationService>();
    private readonly IGame _game = Substitute.For<IGame>();
    private readonly Guid _gameId = Guid.NewGuid();
    private readonly Player _player1 = new Player(Guid.NewGuid(), "Player 1", PlayerControlType.Human);
    private readonly Unit _unit;

    public MoveUnitCommandTests()
    {
        _game.Players.Returns([_player1]);
        var unitData = MechFactoryTests.CreateDummyMechData();
        _unit = new MechFactory(
            new TotalWarfareRulesProvider(),
            new ClassicBattletechComponentProvider(),
            _localizationService).Create(unitData);
        _player1.AddUnit(_unit);
    }

    private MoveUnitCommand CreateCommand()
    {
        var startPos = new HexPosition(3, 5, HexDirection.Top);
        var endPos = new HexPosition(4, 5, HexDirection.Bottom);
        var pathSegment = new PathSegment(startPos, endPos, [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }]);

        return new MoveUnitCommand
        {
            MovementType = MovementType.Walk,
            GameOriginId = _gameId,
            PlayerId = _player1.Id,
            UnitId = _unit.Id,
            MovementPath = [pathSegment.ToData()],
            IsCompleted = true
        };
    }

    [Fact]
    public void Render_ShouldFormatCorrectly()
    {
        // Arrange
        var command = CreateCommand();
        _unit.Deploy(new HexPosition(1, 1, HexDirection.Top), null);
        _localizationService.GetString("Command_MoveUnit").Returns("formatted move command");
        _localizationService.GetString("Command_MoveUnit_Completed").Returns("completed");

        // Act
        var result = command.Render(_localizationService, _game);

        // Assert
        result.ShouldBe("formatted move command" + Environment.NewLine + "completed");
        _localizationService.Received(1).GetString("Command_MoveUnit");
    }

    [Fact]
    public void Render_ShouldReturnEmpty_WhenPlayerNotFound()
    {
        // Arrange
        var command = CreateCommand() with { PlayerId = Guid.NewGuid() };

        // Act
        var result = command.Render(_localizationService, _game);

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public void Render_ShouldReturnEmpty_WhenUnitNotFound()
    {
        // Arrange
        var command = CreateCommand() with { UnitId = Guid.NewGuid() };

        // Act
        var result = command.Render(_localizationService, _game);

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public void Render_ShouldIncludeCompletedLine_WhenMovementIsCompleted()
    {
        // Arrange
        var command = CreateCommand();
        _unit.Deploy(new HexPosition(1, 1, HexDirection.Top), null);
        _localizationService.GetString("Command_MoveUnit").Returns("move line");
        _localizationService.GetString("Command_MoveUnit_Completed").Returns("completed");

        // Act
        var result = command.Render(_localizationService, _game);

        // Assert
        result.ShouldBe("move line" + Environment.NewLine + "completed");
    }

    [Fact]
    public void Render_ShouldIncludeIncompleteLine_WhenMovementIsNotCompleted()
    {
        // Arrange
        var command = CreateCommand() with { IsCompleted = false };
        _unit.Deploy(new HexPosition(1, 1, HexDirection.Top), null);
        _localizationService.GetString("Command_MoveUnit").Returns("move line");
        _localizationService.GetString("Command_MoveUnit_Incomplete").Returns("interrupted");

        // Act
        var result = command.Render(_localizationService, _game);

        // Assert
        result.ShouldBe("move line" + Environment.NewLine + "interrupted");
    }
}