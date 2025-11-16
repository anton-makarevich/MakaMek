using NSubstitute;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Services.Localization;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Data.Game.Commands.Server;

public class ChangeActivePlayerCommandTests
{
    private readonly ILocalizationService _localizationService = Substitute.For<ILocalizationService>();
    private readonly IGame _game = Substitute.For<IGame>();
    private readonly Guid _gameId = Guid.NewGuid();
    private readonly Player _player1 = new Player(Guid.NewGuid(), "Player 1", PlayerControlType.Local);

    public ChangeActivePlayerCommandTests()
    {
        _game.Players.Returns([_player1]);
    }

    private ChangeActivePlayerCommand CreateCommand()
    {
        return new ChangeActivePlayerCommand
        {
            GameOriginId = _gameId,
            PlayerId = _player1.Id,
            UnitsToPlay = 1
        };
    }

    [Fact]
    public void Render_ShouldFormatCorrectly()
    {
        // Arrange
        var command = CreateCommand();

        _localizationService.GetString("Command_ChangeActivePlayerUnits").Returns("formatted active player command");

        // Act
        var result = command.Render(_localizationService, _game);

        // Assert
        result.ShouldBe("formatted active player command");
        _localizationService.Received(1).GetString("Command_ChangeActivePlayerUnits");
    }
    
    [Fact]
    public void Render_ShouldFormatCorrectly_WhenNoUnits()
    {
        // Arrange
        var command = new ChangeActivePlayerCommand
        {
            GameOriginId = _gameId,
            PlayerId = _player1.Id,
            UnitsToPlay = 0
        };

        _localizationService.GetString("Command_ChangeActivePlayer").Returns("formatted active player command");

        // Act
        var result = command.Render(_localizationService, _game);

        // Assert
        result.ShouldBe("formatted active player command");
        _localizationService.Received(1).GetString("Command_ChangeActivePlayer");
    }
    
    [Fact]
    public void Render_ShouldReturnEmpty_WhenPlayerNotFound()
    {
        // Arrange
        var command = new ChangeActivePlayerCommand
        {
            GameOriginId = _gameId,
            PlayerId = Guid.NewGuid(),
            UnitsToPlay = 1
        };
        
        // Act
        var result = command.Render(_localizationService, _game);
        
        // Assert
        result.ShouldBeEmpty();
    }
}