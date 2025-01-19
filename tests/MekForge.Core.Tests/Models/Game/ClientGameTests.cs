using FluentAssertions;
using NSubstitute;
using Sanet.MekForge.Core.Data;
using Sanet.MekForge.Core.Models.Game;
using Sanet.MekForge.Core.Models.Game.Commands;
using Sanet.MekForge.Core.Models.Game.Commands.Client;
using Sanet.MekForge.Core.Models.Game.Commands.Server;
using Sanet.MekForge.Core.Models.Game.Phases;
using Sanet.MekForge.Core.Models.Game.Players;
using Sanet.MekForge.Core.Models.Game.Transport;
using Sanet.MekForge.Core.Models.Map;
using Sanet.MekForge.Core.Models.Map.Terrains;
using Sanet.MekForge.Core.Models.Units;
using Sanet.MekForge.Core.Tests.Data;
using Sanet.MekForge.Core.Utils.Generators;
using Sanet.MekForge.Core.Utils.TechRules;

namespace Sanet.MekForge.Core.Tests.Models.Game;

public class ClientGameTests
{
    private readonly ClientGame _clientGame;
    private readonly ICommandPublisher _commandPublisher;

    public ClientGameTests()
    {
        var battleState = BattleMap.GenerateMap(5, 5, new SingleTerrainGenerator(5,5, new ClearTerrain()));
        _commandPublisher = Substitute.For<ICommandPublisher>();
        var rulesProvider = new ClassicBattletechRulesProvider();
        _clientGame = new ClientGame(battleState,[], rulesProvider, _commandPublisher);
    }

    [Fact]
    public void HandleCommand_ShouldAddPlayer_WhenJoinGameCommandIsReceived()
    {
        // Arrange
        var joinCommand = new JoinGameCommand
        {
            PlayerId = Guid.NewGuid(),
            PlayerName = "Player1",
            GameOriginId = Guid.NewGuid(),
            Units = [],
            Tint = "#FF0000"
        };

        // Act
        _clientGame.HandleCommand(joinCommand);

        // Assert
        _clientGame.Players.Should().HaveCount(1);
        _clientGame.Players[0].Name.Should().Be(joinCommand.PlayerName);
    }
    
    [Fact]
    public void HandleCommand_ShouldNotProcessOwnCommands_WhenGameOriginIdMatches()
    {
        // Arrange
        var command = new JoinGameCommand
        {
            PlayerId = Guid.NewGuid(),
            PlayerName = "Player1",
            Units = [],
            GameOriginId = _clientGame.Id, // Set to this game's ID
            Tint = "#FF0000"
        };

        // Act
        _clientGame.HandleCommand(command);

        // Assert
        // Verify that no players were added since the command was from this game instance
        _clientGame.Players.Should().BeEmpty();
    }

    [Fact]
    public void JoinGameWithUnits_ShouldPublishJoinGameCommand_WhenCalled()
    {
        // Arrange
        var units = new List<UnitData>();
        var player = new Player(Guid.NewGuid(), "Player1");

        // Act
        _clientGame.JoinGameWithUnits(player, units);

        // Assert
        _commandPublisher.Received(1).PublishCommand(Arg.Is<JoinGameCommand>(cmd =>
            cmd.PlayerId == player.Id &&
            cmd.PlayerName == player.Name &&
            cmd.Units.Count == units.Count));
    }
    
    [Fact]
    public void HandleCommand_ShouldSetPlayerStatus_WhenPlayerStatusCommandIsReceived()
    {
        // Arrange
        var playerId = Guid.NewGuid();
        var player = new Player(playerId, "Player1");
        _clientGame.HandleCommand(new JoinGameCommand
        {
            PlayerId = player.Id,
            GameOriginId = Guid.NewGuid(),
            PlayerName = player.Name, Units = [],
            Tint = "#FF0000"
        });

        var statusCommand = new UpdatePlayerStatusCommand
        {
            GameOriginId = Guid.NewGuid(),
            PlayerId = playerId,
            PlayerStatus = PlayerStatus.Playing
        };

        // Act
        _clientGame.HandleCommand(statusCommand);

        // Assert
        var updatedPlayer = _clientGame.Players.FirstOrDefault(p => p.Id == playerId);
        updatedPlayer.Should().NotBeNull();
        updatedPlayer.Status.Should().Be(PlayerStatus.Playing);
    }
    
    [Fact]
    public void SetPlayerReady_ShouldNotPublishPlayerStatusCommand_WhenCalled_ButPlayerIsNotInGame()
    {
        // Arrange
        var player = new Player(Guid.NewGuid(), "Player1");

        // Act
        _clientGame.SetPlayerReady(player);

        // Assert
        _commandPublisher.DidNotReceive().PublishCommand(Arg.Any<UpdatePlayerStatusCommand>());
    }
    
    [Fact]
    public void SetPlayerReady_ShouldPublishPlayerStatusCommand_WhenCalled_AndPlayerIsInGame()
    {
        // Arrange
        var player = new Player(Guid.NewGuid(), "Player1");
        _clientGame.HandleCommand(new JoinGameCommand
        {
            PlayerId = player.Id,
            GameOriginId = Guid.NewGuid(),
            PlayerName = player.Name,
            Units = [],
            Tint = "#FF0000"
        });

        // Act
        _clientGame.SetPlayerReady(player);

        // Assert
        _commandPublisher.Received(1).PublishCommand(Arg.Is<UpdatePlayerStatusCommand>(cmd => 
            cmd.PlayerId == player.Id && 
            cmd.PlayerStatus == PlayerStatus.Playing &&
            cmd.GameOriginId == _clientGame.Id
        ));
    }

    [Fact]
    public void ChangePhase_ShouldProcessCommand()
    {
        // Arrange
        var command = new ChangePhaseCommand
        {
            GameOriginId = Guid.NewGuid(),
            Phase = PhaseNames.End
        };
        
        // Act
        _clientGame.HandleCommand(command);
        
        // Assert
        _clientGame.TurnPhase.Should().Be(PhaseNames.End);
    }
    
    [Fact]
    public void ChangeActivePlayer_ShouldProcessCommand()
    {
        // Arrange
        var player = new Player(Guid.NewGuid(), "Player1");
        _clientGame.HandleCommand(new JoinGameCommand
        {
            PlayerId = player.Id,
            GameOriginId = Guid.NewGuid(),
            PlayerName = player.Name,
            Units = [],
            Tint = "#FF0000"
        });
        var actualPlayer = _clientGame.Players.FirstOrDefault(p => p.Id == player.Id);
        var command = new ChangeActivePlayerCommand
        {
            GameOriginId = Guid.NewGuid(),
            PlayerId = player.Id,
            UnitsToPlay = 0
        };
        
        // Act
        _clientGame.HandleCommand(command);
        
        // Assert
        _clientGame.ActivePlayer.Should().Be(actualPlayer);
        actualPlayer.Name.Should().Be(player.Name);
        actualPlayer.Id.Should().Be(player.Id);
    }

    [Fact]
    public void HandleCommand_ShouldAddCommandToLog_WhenCommandIsValid()
    {
        // Arrange
        var joinCommand = new JoinGameCommand
        {
            PlayerId = Guid.NewGuid(),
            PlayerName = "Player1",
            GameOriginId = Guid.NewGuid(),
            Units = [],
            Tint = "#FF0000"
        };

        // Act
        _clientGame.HandleCommand(joinCommand);

        // Assert
        _clientGame.CommandLog.Should().HaveCount(1);
        _clientGame.CommandLog[0].Should().BeEquivalentTo(joinCommand);
    }

    [Fact]
    public void HandleCommand_ShouldNotAddCommandToLog_WhenGameOriginIdMatches()
    {
        // Arrange
        var command = new JoinGameCommand
        {
            PlayerId = Guid.NewGuid(),
            PlayerName = "Player1",
            Units = [],
            GameOriginId = _clientGame.Id,
            Tint = "#FF0000"
        };

        // Act
        _clientGame.HandleCommand(command);

        // Assert
        _clientGame.CommandLog.Should().BeEmpty();
    }

    [Fact]
    public void Commands_ShouldEmitCommand_WhenHandleCommandIsCalled()
    {
        // Arrange
        var joinCommand = new JoinGameCommand
        {
            PlayerId = Guid.NewGuid(),
            PlayerName = "Player1",
            GameOriginId = Guid.NewGuid(),
            Units = [],
            Tint = "#FF0000"
        };
        var receivedCommands = new List<GameCommand>();
        using var subscription = _clientGame.Commands.Subscribe(cmd => receivedCommands.Add(cmd));

        // Act
        _clientGame.HandleCommand(joinCommand);

        // Assert
        receivedCommands.Should().HaveCount(1);
        receivedCommands.First().Should().BeEquivalentTo(joinCommand);
    }

    [Fact]
    public void DeployUnit_ShouldPublishCommand_WhenActivePlayerExists()
    {
        // Arrange
        var player = new Player(Guid.NewGuid(), "Player1");
        var unitData = MechFactoryTests.CreateDummyMechData();
        unitData.Id= Guid.NewGuid();
        _clientGame.HandleCommand(new JoinGameCommand
        {
            PlayerId = player.Id,
            GameOriginId = Guid.NewGuid(),
            PlayerName = player.Name,
            Units = [unitData],
            Tint = "#FF0000"
        });
        _clientGame.HandleCommand(new ChangeActivePlayerCommand
        {
            GameOriginId = Guid.NewGuid(),
            PlayerId = player.Id,
            UnitsToPlay = 1
        });

        var deployCommand = new DeployUnitCommand
        {
            GameOriginId = _clientGame.Id,
            PlayerId = player.Id,
            Position = new  HexCoordinateData(1, 1), 
            Direction = 0,
            UnitId = unitData.Id.Value
        };

        // Act
        _clientGame.DeployUnit(deployCommand);

        // Assert
        _commandPublisher.Received(1).PublishCommand(Arg.Is<DeployUnitCommand>(cmd =>
            cmd.PlayerId == player.Id &&
            cmd.Position == deployCommand.Position &&
            cmd.GameOriginId == _clientGame.Id));
    }

    [Fact]
    public void DeployUnit_ShouldNotPublishCommand_WhenNoActivePlayer()
    {
        // Arrange
        var deployCommand = new DeployUnitCommand
        {
            GameOriginId = _clientGame.Id,
            PlayerId = Guid.NewGuid(),
            Position = new HexCoordinateData(1,1),
            Direction = 0,
            UnitId = Guid.NewGuid()
        };
    
        // Act
        _clientGame.DeployUnit(deployCommand);
    
        // Assert
        _commandPublisher.DidNotReceive().PublishCommand(Arg.Any<DeployUnitCommand>());
    }
    
    [Fact]
    public void MoveUnit_ShouldPublishCommand_WhenActivePlayerExists()
    {
        // Arrange
        var player = new Player(Guid.NewGuid(), "Player1");
        var unitData = MechFactoryTests.CreateDummyMechData();
        unitData.Id= Guid.NewGuid();
        _clientGame.HandleCommand(new JoinGameCommand
        {
            PlayerId = player.Id,
            GameOriginId = Guid.NewGuid(),
            PlayerName = player.Name,
            Units = [unitData],
            Tint = "#FF0000"
        });
        _clientGame.HandleCommand(new ChangeActivePlayerCommand
        {
            GameOriginId = Guid.NewGuid(),
            PlayerId = player.Id,
            UnitsToPlay = 1
        });
    
        var moveCommand = new MoveUnitCommand
        {
            GameOriginId = _clientGame.Id,
            PlayerId = player.Id,
            MovementType = MovementType.Walk,
            UnitId = unitData.Id.Value,
            MovementPath = [new PathSegment(new HexPosition(1, 1, HexDirection.Top), new HexPosition(2, 2, HexDirection.Top), 1).ToData()]
        };
    
        // Act
        _clientGame.MoveUnit(moveCommand);
    
        // Assert
        _commandPublisher.Received(1).PublishCommand(Arg.Is<MoveUnitCommand>(cmd =>
            cmd.PlayerId == player.Id &&
            cmd.MovementType == moveCommand.MovementType &&
            cmd.GameOriginId == _clientGame.Id));
    }
    
    [Fact]
    public void MoveUnit_ShouldNotPublishCommand_WhenNoActivePlayer()
    {
        // Arrange
        var moveCommand = new MoveUnitCommand
        {
            GameOriginId = _clientGame.Id,
            PlayerId = Guid.NewGuid(),
            MovementType = MovementType.Walk,
            UnitId = Guid.NewGuid(),
            MovementPath = [new PathSegment(new HexPosition(1, 1, HexDirection.Top), new HexPosition(2, 2, HexDirection.Top), 1).ToData()]
        };
    
        // Act
        _clientGame.MoveUnit(moveCommand);
    
        // Assert
        _commandPublisher.DidNotReceive().PublishCommand(Arg.Any<MoveUnitCommand>());
    }

    [Fact]
    public void HandleCommand_ShouldDeployUnit_WhenDeployUnitCommandIsReceived()
    {
        // Arrange
        var player = new Player(Guid.NewGuid(), "Player1");
        var unitData = MechFactoryTests.CreateDummyMechData();
        unitData.Id = Guid.NewGuid();
        _clientGame.HandleCommand(new JoinGameCommand
        {
            PlayerId = player.Id,
            GameOriginId = Guid.NewGuid(),
            PlayerName = player.Name,
            Units = [unitData],
            Tint = "#FF0000"
        });

        var deployCommand = new DeployUnitCommand
        {
            GameOriginId = Guid.NewGuid(),
            PlayerId = player.Id,
            Position = new HexCoordinateData(1, 1),
            Direction = 0,
            UnitId = unitData.Id.Value
        };

        // Act
        _clientGame.HandleCommand(deployCommand);

        // Assert
        var deployedUnit = _clientGame.Players.First().Units.First();
        deployedUnit.IsDeployed.Should().BeTrue();
        deployedUnit.Position.Value.Coordinates.Q.Should().Be(1);
        deployedUnit.Position.Value.Coordinates.R.Should().Be(1);
        deployedUnit.Position.Value.Facing.Should().Be(HexDirection.Top);
    }

    [Fact]
    public void HandleCommand_ShouldNotDeployUnit_WhenUnitIsAlreadyDeployed()
    {
        // Arrange
        var player = new Player(Guid.NewGuid(), "Player1");
        var unitData = MechFactoryTests.CreateDummyMechData();
        unitData.Id = Guid.NewGuid();
        _clientGame.HandleCommand(new JoinGameCommand
        {
            PlayerId = player.Id,
            GameOriginId = Guid.NewGuid(),
            PlayerName = player.Name,
            Units = [unitData],
            Tint = "#FF0000"
        });

        var firstDeployCommand = new DeployUnitCommand
        {
            GameOriginId = Guid.NewGuid(),
            PlayerId = player.Id,
            Position = new HexCoordinateData(1, 1),
            Direction = 0,
            UnitId = unitData.Id.Value
        };
        _clientGame.HandleCommand(firstDeployCommand);

        var initialPosition = _clientGame.Players.First().Units.First().Position;

        var secondDeployCommand = new DeployUnitCommand
        {
            GameOriginId = Guid.NewGuid(),
            PlayerId = player.Id,
            Position = new HexCoordinateData(2, 2),
            Direction = 1,
            UnitId = unitData.Id.Value
        };

        // Act
        _clientGame.HandleCommand(secondDeployCommand);

        // Assert
        var unit = _clientGame.Players.First().Units.First();
        unit.Position.Should().Be(initialPosition);
    }

    [Fact]
    public void HandleCommand_ShouldMoveUnit_WhenMoveUnitCommandIsReceived()
    {
        // Arrange
        var player = new Player(Guid.NewGuid(), "Player1");
        var unitData = MechFactoryTests.CreateDummyMechData();
        unitData.Id = Guid.NewGuid();
        _clientGame.HandleCommand(new JoinGameCommand
        {
            PlayerId = player.Id,
            GameOriginId = Guid.NewGuid(),
            PlayerName = player.Name,
            Units = [unitData],
            Tint = "#FF0000"
        });

        // First deploy the unit
        var deployCommand = new DeployUnitCommand
        {
            GameOriginId = Guid.NewGuid(),
            PlayerId = player.Id,
            Position = new HexCoordinateData(1, 1),
            Direction = 0,
            UnitId = unitData.Id.Value
        };
        _clientGame.HandleCommand(deployCommand);

        var moveCommand = new MoveUnitCommand
        {
            GameOriginId = Guid.NewGuid(),
            PlayerId = player.Id,
            MovementType = MovementType.Walk,
            UnitId = unitData.Id.Value,
            MovementPath = [new PathSegment(new HexPosition(1, 1, HexDirection.Top), new HexPosition(2, 2, HexDirection.Top), 1).ToData()]
        };

        // Act
        _clientGame.HandleCommand(moveCommand);

        // Assert
        var movedUnit = _clientGame.Players.First().Units.First();
        movedUnit.Position.Value.Coordinates.Q.Should().Be(2);
        movedUnit.Position.Value.Coordinates.R.Should().Be(2);
        movedUnit.Position.Value.Facing.Should().Be(HexDirection.Top);
    }

    [Fact]
    public void HandleCommand_ShouldNotMoveUnit_WhenUnitDoesNotExist()
    {
        // Arrange
        var player = new Player(Guid.NewGuid(), "Player1");
        _clientGame.HandleCommand(new JoinGameCommand
        {
            PlayerId = player.Id,
            GameOriginId = Guid.NewGuid(),
            PlayerName = player.Name,
            Units = [],
            Tint = "#FF0000"
        });

        var moveCommand = new MoveUnitCommand
        {
            GameOriginId = Guid.NewGuid(),
            PlayerId = player.Id,
            MovementType = MovementType.Walk,
            UnitId = Guid.NewGuid(),
            MovementPath = [new PathSegment(new HexPosition(1, 1, HexDirection.Top), new HexPosition(2, 2, HexDirection.Top), 1).ToData()]
        };

        // Act & Assert
        _clientGame.Invoking(g => g.HandleCommand(moveCommand))
            .Should().NotThrow();
    }
}