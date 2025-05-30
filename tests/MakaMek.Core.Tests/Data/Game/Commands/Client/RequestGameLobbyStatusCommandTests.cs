using NSubstitute;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Services.Localization;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Data.Game.Commands.Client;

public class RequestGameLobbyStatusCommandTests
{
    private readonly ILocalizationService _localizationService = Substitute.For<ILocalizationService>();
    private readonly IGame _game = Substitute.For<IGame>();
    private readonly Guid _gameId = Guid.NewGuid();

    private RequestGameLobbyStatusCommand CreateCommand()
    {
        return new RequestGameLobbyStatusCommand
        {
            GameOriginId = _gameId,
            Timestamp = DateTime.UtcNow
        };
    }

    [Fact]
    public void Render_ShouldFormatCorrectly()
    {
        // Arrange
        var command = CreateCommand();
        var expectedFormat = "Client {0} requested game lobby status for game.";
        var expectedResult = $"Client {_gameId} requested game lobby status for game.";

        _localizationService.GetString("Command_RequestGameLobbyStatus").Returns(expectedFormat);

        // Act
        var result = command.Render(_localizationService, _game);

        // Assert
        result.ShouldBe(expectedResult);
        _localizationService.Received(1).GetString("Command_RequestGameLobbyStatus");
    }
    
    [Fact]
    public void Properties_ShouldBeSetCorrectly()
    {
        // Arrange
        var timestamp = DateTime.UtcNow;
        
        // Act
        var command = new RequestGameLobbyStatusCommand
        {
            GameOriginId = _gameId,
            Timestamp = timestamp
        };
        
        // Assert
        command.GameOriginId.ShouldBe(_gameId);
        command.Timestamp.ShouldBe(timestamp);
    }
}
