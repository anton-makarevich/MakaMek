using NSubstitute;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Services.Localization;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Data.Game.Commands.Client;

public class JoinGameCommandTests
{
    private readonly ILocalizationService _localizationService = Substitute.For<ILocalizationService>();
    private readonly IGame _game = Substitute.For<IGame>();
    private readonly Guid _gameId = Guid.NewGuid();
    private readonly Player _player1 = new Player(Guid.NewGuid(), "Player 1");

    public JoinGameCommandTests()
    {
        _game.Players.Returns([_player1]);
    }

    private JoinGameCommand CreateCommand()
    {
        return new JoinGameCommand
        {
            GameOriginId = _gameId,
            PlayerId = _player1.Id,
            PlayerName = _player1.Name,
            Units = [],
            Tint = "#FF0000",
            PilotAssignments = []
        };
    }

    [Fact]
    public void Render_ShouldFormatCorrectly()
    {
        // Arrange
        var command = CreateCommand();

        _localizationService.GetString("Command_JoinGame").Returns("formatted join command");

        // Act
        var result = command.Render(_localizationService, _game);

        // Assert
        result.ShouldBe("formatted join command");
        _localizationService.Received(1).GetString("Command_JoinGame");
    }
}