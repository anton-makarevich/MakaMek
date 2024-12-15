using FluentAssertions;
using NSubstitute;
using Sanet.MekForge.Core.Data;
using Sanet.MekForge.Core.Game;
using Sanet.MekForge.Core.Models;
using Sanet.MekForge.Core.Models.Game.Commands;
using Sanet.MekForge.Core.Models.Game.Protocol;
using Sanet.MekForge.Core.Utils.TechRules;

namespace Sanet.MekForge.Core.Tests.Game;

public class LocalGameTests
{
    private readonly LocalGame _localGame;
    private readonly ICommandPublisher _commandPublisher;
    private readonly IPlayer _localPlayer;

    public LocalGameTests()
    {
        var battleState = new BattleState(new BattleMap(5, 5));
        _commandPublisher = Substitute.For<ICommandPublisher>();
        var rulesProvider = Substitute.For<IRulesProvider>();
        _localPlayer = Substitute.For<IPlayer>();
        _localGame = new LocalGame(battleState, rulesProvider, _commandPublisher, _localPlayer);
    }

    [Fact]
    public void HandleCommand_ShouldAddPlayer_WhenJoinGameCommandIsReceived()
    {
        // Arrange
        var joinCommand = new JoinGameCommand
        {
            PlayerId = Guid.NewGuid(),
            PlayerName = "Player1",
            Units = new List<UnitData>()
        };

        // Act
        _localGame.HandleCommand(joinCommand);

        // Assert
        _localGame.Players.Should().HaveCount(1);
        _localGame.Players[0].Name.Should().Be(joinCommand.PlayerName);
    }

    [Fact]
    public void JoinGameWithUnits_ShouldPublishJoinGameCommand_WhenCalled()
    {
        // Arrange
        var units = new List<UnitData>();

        // Act
        _localGame.JoinGameWithUnits(units);

        // Assert
        _commandPublisher.Received(1).PublishCommand(Arg.Is<JoinGameCommand>(cmd =>
            cmd.PlayerId == _localPlayer.Id &&
            cmd.PlayerName == _localPlayer.Name &&
            cmd.Units.Count == units.Count));
    }
}