using NSubstitute;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Services.Localization;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Data.Game.Commands.Client;

public class UpdatePlayerStatusCommandTests
{
    private readonly ILocalizationService _localizationService = Substitute.For<ILocalizationService>();
    private readonly IGame _game = Substitute.For<IGame>();
    private readonly Guid _gameId = Guid.NewGuid();
    private readonly Player _player1 = new Player(Guid.NewGuid(), "Player 1", PlayerControlType.Local);

    public UpdatePlayerStatusCommandTests()
    {
        _game.Players.Returns([_player1]);
    }

    private UpdatePlayerStatusCommand CreateCommand()
    {
        return new UpdatePlayerStatusCommand
        {
            GameOriginId = _gameId,
            PlayerId = _player1.Id,
            PlayerStatus = PlayerStatus.Ready
        };
    }

    [Fact]
    public void Render_ShouldFormatCorrectly()
    {
        // Arrange
        var command = CreateCommand();
        _localizationService.GetString("Command_UpdatePlayerStatus").Returns("formatted status command");

        // Act
        var result = command.Render(_localizationService, _game);

        // Assert
        result.ShouldBe("formatted status command");
        _localizationService.Received(1).GetString("Command_UpdatePlayerStatus");
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
}