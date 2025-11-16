using JetBrains.Annotations;
using NSubstitute;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Services.Localization;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Data.Game.Commands.Client;

[TestSubject(typeof(PlayerLeftCommand))]
public class PlayerLeftCommandTest
{

    [Fact]
    public void Render_ShouldFormatCorrectly()
    {
        // Arrange
        var localizationService = Substitute.For<ILocalizationService>();
        localizationService.GetString("Command_PlayerLeft").Returns("{0} has left the game.");
        var sut = new PlayerLeftCommand
        {
            GameOriginId = Guid.NewGuid(),
            PlayerId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow
        };
        var game = Substitute.For<IGame>();
        game.Players.Returns(new List<IPlayer> { new Player(sut.PlayerId, "Player 1", PlayerControlType.Local) });

        // Act
        var result = sut.Render(localizationService, game);

        // Assert
        result.ShouldBe("Player 1 has left the game.");
        localizationService.Received(1).GetString("Command_PlayerLeft");
    }
    
    [Fact]
    public void Render_ShouldReturnDefaultPlayerName_WhenPlayerNotFound()
    {
        // Arrange
        var localizationService = Substitute.For<ILocalizationService>();
        localizationService.GetString("Command_PlayerLeft").Returns("{0} has left the game.");
        var sut = new PlayerLeftCommand
        {
            GameOriginId = Guid.NewGuid(),
            PlayerId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow
        };
        var game = Substitute.For<IGame>();
        game.Players.Returns(new List<IPlayer> { new Player(Guid.NewGuid(), "Player 2", PlayerControlType.Local) });

        // Act
        var result = sut.Render(localizationService, game);

        // Assert
        result.ShouldBe("Unknown has left the game.");
    }
}