using System.Reactive.Linq;
using System.Reactive.Subjects;
using NSubstitute;
using Sanet.MakaMek.Bots.Models;
using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Players;
using Shouldly;

namespace Sanet.MakaMek.Bots.Tests.Models;

public class BotManagerTests : IDisposable
{
    private readonly BotManager _sut;
    private readonly IClientGame _clientGame;
    private readonly Subject<IGameCommand> _commandSubject;

    public BotManagerTests()
    {
        _sut = new BotManager();
        _clientGame = Substitute.For<IClientGame>();
        _commandSubject = new Subject<IGameCommand>();
        
        _clientGame.Commands.Returns(_commandSubject.AsObservable());
        _clientGame.Id.Returns(Guid.NewGuid());
    }

    [Fact]
    public void Initialize_ShouldSetClientGame()
    {
        // Act
        _sut.Initialize(_clientGame);
        
        // Assert
        _sut.Bots.ShouldBeEmpty();
        _sut.ClientGame.ShouldBe(_clientGame);
    }

    [Fact]
    public void Initialize_ShouldClearExistingBots()
    {
        // Arrange
        _sut.Initialize(_clientGame);
        var player = CreateBotPlayer();
        _sut.AddBot(player);
        _sut.Bots.Count.ShouldBe(1);
        
        // Act
        _sut.Initialize(_clientGame);
        
        // Assert
        _sut.Bots.ShouldBeEmpty();
    }

    [Fact]
    public void AddBot_WhenNotInitialized_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var player = CreateBotPlayer();
        
        // Act & Assert
        Should.Throw<InvalidOperationException>(() => _sut.AddBot(player))
            .Message.ShouldContain("BotManager must be initialized");
    }

    [Fact]
    public void AddBot_WhenPlayerIsNotBot_ShouldThrowArgumentException()
    {
        // Arrange
        _sut.Initialize(_clientGame);
        var player = CreateHumanPlayer();
        
        // Act & Assert
        Should.Throw<ArgumentException>(() => _sut.AddBot(player))
            .Message.ShouldContain("Player must have ControlType.Bot");
    }

    [Fact]
    public void AddBot_WhenValidBotPlayer_ShouldAddToCollection()
    {
        // Arrange
        _sut.Initialize(_clientGame);
        var player = CreateBotPlayer();
        
        // Act
        _sut.AddBot(player);
        
        // Assert
        _sut.Bots.Count.ShouldBe(1);
        _sut.Bots[0].Player.ShouldBe(player);
    }

    [Fact]
    public void IsBot_WhenPlayerIsBot_ShouldReturnTrue()
    {
        // Arrange
        _sut.Initialize(_clientGame);
        var player = CreateBotPlayer();
        _sut.AddBot(player);
        
        // Act
        var result = _sut.IsBot(player.Id);
        
        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void IsBot_WhenPlayerIsNotBot_ShouldReturnFalse()
    {
        // Arrange
        _sut.Initialize(_clientGame);
        var playerId = Guid.NewGuid();
        
        // Act
        var result = _sut.IsBot(playerId);
        
        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void RemoveBot_WhenBotExists_ShouldRemoveFromCollection()
    {
        // Arrange
        _sut.Initialize(_clientGame);
        var player = CreateBotPlayer();
        _sut.AddBot(player);
        _sut.Bots.Count.ShouldBe(1);
        
        // Act
        _sut.RemoveBot(player.Id);
        
        // Assert
        _sut.Bots.ShouldBeEmpty();
        _sut.IsBot(player.Id).ShouldBeFalse();
    }

    [Fact]
    public void RemoveBot_WhenBotDoesNotExist_ShouldNotThrow()
    {
        // Arrange
        _sut.Initialize(_clientGame);
        var playerId = Guid.NewGuid();
        
        // Act & Assert
        Should.NotThrow(() => _sut.RemoveBot(playerId));
    }

    [Fact]
    public void Clear_ShouldRemoveAllBots()
    {
        // Arrange
        _sut.Initialize(_clientGame);
        var player1 = CreateBotPlayer();
        var player2 = CreateBotPlayer();
        _sut.AddBot(player1);
        _sut.AddBot(player2);
        _sut.Bots.Count.ShouldBe(2);
        
        // Act
        _sut.Clear();
        
        // Assert
        _sut.Bots.ShouldBeEmpty();
    }
    
    [Fact]
    public void Initialize_ShouldRecreateDecisionEngineProvider()
    {
        _sut.Initialize(_clientGame);
        var originalProvider = _sut.DecisionEngineProvider;
        
        _sut.Initialize(_clientGame);
        
        _sut.DecisionEngineProvider.ShouldNotBeSameAs(originalProvider);
    }

    private static IPlayer CreateBotPlayer()
    {
        var player = Substitute.For<IPlayer>();
        player.Id.Returns(Guid.NewGuid());
        player.Name.Returns($"Bot Player {Guid.NewGuid()}");
        player.ControlType.Returns(PlayerControlType.Bot);
        return player;
    }

    private IPlayer CreateHumanPlayer()
    {
        var player = Substitute.For<IPlayer>();
        player.Id.Returns(Guid.NewGuid());
        player.Name.Returns($"Human Player {Guid.NewGuid()}");
        player.ControlType.Returns(PlayerControlType.Human);
        return player;
    }

    public void Dispose()
    {
        _sut.Clear();
        _commandSubject.Dispose();
    }
}
