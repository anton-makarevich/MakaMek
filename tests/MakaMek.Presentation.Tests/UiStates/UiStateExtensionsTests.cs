using NSubstitute;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Presentation.UiStates;
using Shouldly;

namespace Sanet.MakaMek.Presentation.Tests.UiStates;

public class UiStateExtensionsTests
{
    private readonly IUiState _state;
    private readonly IClientGame _game;
    private readonly IPlayer _activePlayer;

    public UiStateExtensionsTests()
    {
        _state = Substitute.For<IUiState>();
        _game = Substitute.For<IClientGame>();
        _activePlayer = Substitute.For<IPlayer>();
        
        _state.Game.Returns(_game);
    }
    
    [Fact]
    public void CanHumanPlayerAct_WhenGameIsNull_ReturnsFalse()
    {
        // Arrange
        _state.Game.Returns((IClientGame?)null);

        // Act
        var result = _state.CanHumanPlayerAct();

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void CanHumanPlayerAct_WhenCanActivePlayerActIsFalse_ReturnsFalse()
    {
        // Arrange
        _game.CanActivePlayerAct.Returns(false);
        _game.PhaseStepState.Returns(new PhaseStepState(_activePlayer, 0));
        _activePlayer.ControlType.Returns(PlayerControlType.Human);

        // Act
        var result = _state.CanHumanPlayerAct();

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void CanHumanPlayerAct_WhenActivePlayerIsNull_ReturnsFalse()
    {
        // Arrange
        _game.CanActivePlayerAct.Returns(true);
        _game.PhaseStepState.Returns((PhaseStepState?)null);

        // Act
        var result = _state.CanHumanPlayerAct();

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void CanHumanPlayerAct_WhenActivePlayerIsBot_ReturnsFalse()
    {
        // Arrange
        _game.CanActivePlayerAct.Returns(true);
        _game.PhaseStepState.Returns(new PhaseStepState(_activePlayer, 0));
        _activePlayer.ControlType.Returns(PlayerControlType.Bot);

        // Act
        var result = _state.CanHumanPlayerAct();

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void CanHumanPlayerAct_WhenActivePlayerIsRemote_ReturnsFalse()
    {
        // Arrange
        _game.CanActivePlayerAct.Returns(true);
        _game.PhaseStepState.Returns(new PhaseStepState(_activePlayer, 0));
        _activePlayer.ControlType.Returns(PlayerControlType.Remote);

        // Act
        var result = _state.CanHumanPlayerAct();

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void CanHumanPlayerAct_WhenAllConditionsAreMet_ReturnsTrue()
    {
        // Arrange
        _game.CanActivePlayerAct.Returns(true);
        _game.PhaseStepState.Returns(new PhaseStepState(_activePlayer, 0));
        _activePlayer.ControlType.Returns(PlayerControlType.Human);

        // Act
        var result = _state.CanHumanPlayerAct();

        // Assert
        result.ShouldBeTrue();
    }
    
    [Fact]
    public void IsActiveHumanPlayer_WhenGameIsNull_ReturnsFalse()
    {
        // Arrange
        _state.Game.Returns((IClientGame?)null);

        // Act
        var result = _state.IsActiveHumanPlayer();

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void IsActiveHumanPlayer_WhenActivePlayerIsNull_ReturnsFalse()
    {
        // Arrange
        _game.PhaseStepState.Returns((PhaseStepState?)null);

        // Act
        var result = _state.IsActiveHumanPlayer();

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void IsActiveHumanPlayer_WhenPlayerIsNotLocal_ReturnsFalse()
    {
        // Arrange
        var playerId = Guid.NewGuid();
        var otherPlayerId = Guid.NewGuid();
        
        _game.PhaseStepState.Returns(new PhaseStepState(_activePlayer, 0));
        _activePlayer.Id.Returns(playerId);
        _activePlayer.ControlType.Returns(PlayerControlType.Human);
        _game.LocalPlayers.Returns(new List<Guid> { otherPlayerId });

        // Act
        var result = _state.IsActiveHumanPlayer();

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void IsActiveHumanPlayer_WhenPlayerIsLocalButBot_ReturnsFalse()
    {
        // Arrange
        var playerId = Guid.NewGuid();
        
        _game.PhaseStepState.Returns(new PhaseStepState(_activePlayer, 0));
        _activePlayer.Id.Returns(playerId);
        _activePlayer.ControlType.Returns(PlayerControlType.Bot);
        _game.LocalPlayers.Returns(new List<Guid> { playerId });

        // Act
        var result = _state.IsActiveHumanPlayer();

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void IsActiveHumanPlayer_WhenPlayerIsLocalButRemote_ReturnsFalse()
    {
        // Arrange
        var playerId = Guid.NewGuid();
        
        _game.PhaseStepState.Returns(new PhaseStepState(_activePlayer, 0));
        _activePlayer.Id.Returns(playerId);
        _activePlayer.ControlType.Returns(PlayerControlType.Remote);
        _game.LocalPlayers.Returns(new List<Guid> { playerId });

        // Act
        var result = _state.IsActiveHumanPlayer();

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void IsActiveHumanPlayer_WhenAllConditionsAreMet_ReturnsTrue()
    {
        // Arrange
        var playerId = Guid.NewGuid();
        
        _game.PhaseStepState.Returns(new PhaseStepState(_activePlayer, 0));
        _activePlayer.Id.Returns(playerId);
        _activePlayer.ControlType.Returns(PlayerControlType.Human);
        _game.LocalPlayers.Returns(new List<Guid> { playerId });

        // Act
        var result = _state.IsActiveHumanPlayer();

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void IsActiveHumanPlayer_WhenPlayerIsOneOfMultipleLocalPlayers_ReturnsTrue()
    {
        // Arrange
        var playerId = Guid.NewGuid();
        var otherPlayerId1 = Guid.NewGuid();
        var otherPlayerId2 = Guid.NewGuid();
        
        _game.PhaseStepState.Returns(new PhaseStepState(_activePlayer, 0));
        _activePlayer.Id.Returns(playerId);
        _activePlayer.ControlType.Returns(PlayerControlType.Human);
        _game.LocalPlayers.Returns(new List<Guid> { otherPlayerId1, playerId, otherPlayerId2 });

        // Act
        var result = _state.IsActiveHumanPlayer();

        // Assert
        result.ShouldBeTrue();
    }
}
