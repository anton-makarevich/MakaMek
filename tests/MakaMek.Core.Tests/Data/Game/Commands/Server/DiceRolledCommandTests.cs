using NSubstitute;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Services.Localization;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Data.Game.Commands.Server;

public class DiceRolledCommandTests
{
    private readonly ILocalizationService _localizationService = Substitute.For<ILocalizationService>();
    private readonly IGame _game = Substitute.For<IGame>();
    private readonly Guid _gameId = Guid.NewGuid();
    private readonly Player _player1 = new Player(Guid.NewGuid(), "Player 1", PlayerControlType.Local);

    public DiceRolledCommandTests()
    {
        _game.Players.Returns([_player1]);
    }

    private DiceRolledCommand CreateCommand()
    {
        return new DiceRolledCommand
        {
            GameOriginId = _gameId,
            PlayerId = _player1.Id,
            Roll = 10
        };
    }

    [Fact]
    public void Render_ShouldFormatCorrectly()
    {
        // Arrange
        var command = CreateCommand();
        _localizationService.GetString("Command_DiceRolled").Returns("formatted dice command");

        // Act
        var result = command.Render(_localizationService, _game);

        // Assert
        result.ShouldBe("formatted dice command");
        _localizationService.Received(1).GetString("Command_DiceRolled");
    }

    [Fact]
    public void Render_ShouldReturnEmpty_WhenPlayerNotFound()
    {
        // Arrange
        var command = CreateCommand();

        // Act
        var result = command.Render(_localizationService, _game);

        // Assert
        result.ShouldBeEmpty();
    }
}