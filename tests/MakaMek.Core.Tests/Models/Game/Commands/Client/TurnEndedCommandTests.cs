using Shouldly;
using NSubstitute;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Commands.Client;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Services.Localization;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Commands.Client;

public class TurnEndedCommandTests
{
    private readonly ILocalizationService _localizationService = Substitute.For<ILocalizationService>();
    private readonly IGame _game = Substitute.For<IGame>();
    private readonly Guid _gameId = Guid.NewGuid();
    private readonly Player _player;

    public TurnEndedCommandTests()
    {
        _player = new Player(Guid.NewGuid(), "Player 1");
        _game.Players.Returns(new List<IPlayer> { _player });
        _localizationService.GetString("Command_TurnEnded").Returns("{0} has ended their turn.");
    }

    private TurnEndedCommand CreateCommand()
    {
        return new TurnEndedCommand
        {
            GameOriginId = _gameId,
            PlayerId = _player.Id,
            Timestamp = DateTime.UtcNow
        };
    }

    [Fact]
    public void Render_ShouldFormatCorrectly()
    {
        // Arrange
        var command = CreateCommand();

        // Act
        var result = command.Render(_localizationService, _game);

        // Assert
        result.ShouldBe("Player 1 has ended their turn.");
        _localizationService.Received(1).GetString("Command_TurnEnded");
    }

    [Fact]
    public void Render_ShouldReturnEmptyPlayerName_WhenPlayerNotFound()
    {
        // Arrange
        var command = CreateCommand() with { PlayerId = Guid.NewGuid() };

        // Act
        var result = command.Render(_localizationService, _game);

        // Assert
        result.ShouldBe(" has ended their turn.");
    }
}
