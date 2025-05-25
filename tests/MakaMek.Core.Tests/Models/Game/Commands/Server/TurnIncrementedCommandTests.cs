using Shouldly;
using NSubstitute;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Commands.Server;
using Sanet.MakaMek.Core.Services.Localization;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Commands.Server;

public class TurnIncrementedCommandTests
{
    private readonly ILocalizationService _localizationService = Substitute.For<ILocalizationService>();
    private readonly IGame _game = Substitute.For<IGame>();
    private readonly Guid _gameId = Guid.NewGuid();

    public TurnIncrementedCommandTests()
    {
        _localizationService.GetString("Command_TurnIncremented").Returns("Turn {0} has started.");
    }

    private TurnIncrementedCommand CreateCommand(int turnNumber = 1)
    {
        return new TurnIncrementedCommand
        {
            GameOriginId = _gameId,
            TurnNumber = turnNumber,
            Timestamp = DateTime.UtcNow
        };
    }

    [Fact]
    public void Render_ShouldFormatCorrectly()
    {
        // Arrange
        var command = CreateCommand(2);

        // Act
        var result = command.Render(_localizationService, _game);

        // Assert
        result.ShouldBe("Turn 2 has started.");
        _localizationService.Received(1).GetString("Command_TurnIncremented");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(10)]
    public void Render_ShouldIncludeTurnNumber(int turnNumber)
    {
        // Arrange
        var command = CreateCommand(turnNumber);

        // Act
        var result = command.Render(_localizationService, _game);

        // Assert
        result.ShouldBe($"Turn {turnNumber} has started.");
    }
}
