using AsyncAwaitBestPractices;
using Shouldly;
using NSubstitute;
using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Data.Map;
using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Data.Units.Components;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Models.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Mechs.Falling;
using Sanet.MakaMek.Core.Models.Game.Phases;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Map.Factory;
using Sanet.MakaMek.Core.Models.Map.Terrains;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons.Missile;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Core.Models.Units.Pilots;
using Sanet.MakaMek.Core.Services.Cryptography;
using Sanet.MakaMek.Core.Services.Localization;
using Sanet.MakaMek.Core.Services.Transport;
using Sanet.MakaMek.Core.Tests.Models.Map;
using Sanet.MakaMek.Core.Tests.Models.Units.Components.Weapons;
using Sanet.MakaMek.Core.Tests.Utils;
using Sanet.MakaMek.Core.Utils;
using Sanet.MakaMek.Core.Utils.Generators;
using Shouldly.ShouldlyExtensionMethods;

namespace Sanet.MakaMek.Core.Tests.Models.Game;

public class ClientGameTests
{
    private readonly ClientGame _sut;
    private readonly ICommandPublisher _commandPublisher;
    private readonly IBattleMapFactory _mapFactory = Substitute.For<IBattleMapFactory>();
    private readonly IRulesProvider _rulesProvider = new ClassicBattletechRulesProvider();
    private readonly IComponentProvider _componentProvider = new ClassicBattletechComponentProvider();
    private readonly IHashService _hashService = Substitute.For<IHashService>();
    private readonly Guid _idempotencyKey = Guid.NewGuid();
    
    private const int CommandAckTimeout = 300;
    public ClientGameTests()
    {
        _hashService.ComputeCommandIdempotencyKey(
            Arg.Any<Guid>(),
            Arg.Any<Guid>(),
            Arg.Any<Type>(),
            Arg.Any<int>(),
            Arg.Any<string>(),
            Arg.Any<Guid?>())
            .Returns(_idempotencyKey);
        
        var battleMap = BattleMapTests.BattleMapFactory.GenerateMap(5, 5, new SingleTerrainGenerator(5,5, new ClearTerrain()));
        _commandPublisher = Substitute.For<ICommandPublisher>();
        IMechFactory mechFactory = new MechFactory(
            _rulesProvider,
            _componentProvider,
            Substitute.For<ILocalizationService>());
        _mapFactory.CreateFromData(Arg.Any<IList<HexData>>()).Returns(battleMap); 
        _sut = new ClientGame(
            _rulesProvider,
            mechFactory,
            _commandPublisher,
            Substitute.For<IToHitCalculator>(),
            Substitute.For<IPilotingSkillCalculator>(),
            Substitute.For<IConsciousnessCalculator>(),
            Substitute.For<IHeatEffectsCalculator>(),
            _mapFactory,
            _hashService,
            CommandAckTimeout);
    }
    
    private static LocationHitData CreateHitDataForLocation(PartLocation partLocation,
        int damage,
        int[]? aimedShotRoll = null,
        int[]? locationRoll = null)
    {
        return new LocationHitData(
        [
            new LocationDamageData(partLocation,
                damage,
                0,
                false)
        ], aimedShotRoll??[], locationRoll??[], partLocation);
    }
    
    /// <summary>
    /// Waits for a command to be published to the mock command publisher with retry logic.
    /// This helps avoid race conditions in async tests.
    /// </summary>
    private async Task<T?> WaitForPublishedCommand<T>(
        ICommandPublisher publisher, 
        int maxAttempts = 50, 
        int delayMs = 10) where T : struct
    {
        for (var i = 0; i < maxAttempts; i++)
        {
            var calls = publisher.ReceivedCalls().ToList();
            if (calls.Count > 0)
            {
                return (T)calls.First().GetArguments()[0]!;
            }
            await Task.Delay(delayMs);
        }
        return null;
    }

    [Fact]
    public void IsDisposed_ShouldBeFalse_ByDefault()
    {
        _sut.IsDisposed.ShouldBeFalse();
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
            Tint = "#FF0000",
            PilotAssignments = []
        };

        // Act
        _sut.HandleCommand(joinCommand);

        // Assert
        _sut.Players.Count.ShouldBe(1);
        _sut.Players[0].Name.ShouldBe(joinCommand.PlayerName);
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
            GameOriginId = _sut.Id, // Set to this game's ID
            Tint = "#FF0000",
            PilotAssignments = []
        };

        // Act
        _sut.HandleCommand(command);

        // Assert
        // Verify that no players were added since the command was from this game instance
        _sut.Players.ShouldBeEmpty();
    }

    [Fact]
    public void JoinGameWithUnits_ShouldPublishJoinGameCommand_WhenCalled()
    {
        // Arrange
        var units = new List<UnitData>();
        var player = new Player(Guid.NewGuid(), "Player1", PlayerControlType.Human);

        // Act
        _sut.JoinGameWithUnits(player, units,[]);

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
        var player = new Player(playerId, "Player1", PlayerControlType.Remote);
        _sut.HandleCommand(new JoinGameCommand
        {
            PlayerId = player.Id,
            GameOriginId = Guid.NewGuid(),
            PlayerName = player.Name, Units = [],
            Tint = "#FF0000",
            PilotAssignments = []
        });

        var statusCommand = new UpdatePlayerStatusCommand
        {
            GameOriginId = Guid.NewGuid(),
            PlayerId = playerId,
            PlayerStatus = PlayerStatus.Ready
        };

        // Act
        _sut.HandleCommand(statusCommand);

        // Assert
        var updatedPlayer = _sut.Players.FirstOrDefault(p => p.Id == playerId);
        updatedPlayer.ShouldNotBeNull();
        updatedPlayer.Status.ShouldBe(PlayerStatus.Ready);
    }
    
    [Fact]
    public void SetPlayerReady_ShouldNotPublishPlayerStatusCommand_WhenCalled_ButPlayerIsNotInGame()
    {
        // Arrange
        var player = new Player(Guid.NewGuid(), "Player1", PlayerControlType.Human);
        var readyCommand = new UpdatePlayerStatusCommand
        {
            GameOriginId = Guid.NewGuid(),
            PlayerStatus = PlayerStatus.Ready,
            PlayerId = player.Id
        };
        // Act
        _sut.SetPlayerReady(readyCommand);

        // Assert
        _commandPublisher.DidNotReceive().PublishCommand(Arg.Any<UpdatePlayerStatusCommand>());
    }
    
    [Fact]
    public void SetPlayerReady_ShouldPublishPlayerStatusCommand_WhenCalled_AndPlayerIsInGame()
    {
        // Arrange
        var player = new Player(Guid.NewGuid(), "Player1", PlayerControlType.Human);
        _sut.HandleCommand(new JoinGameCommand
        {
            PlayerId = player.Id,
            GameOriginId = Guid.NewGuid(),
            PlayerName = player.Name,
            Units = [],
            Tint = "#FF0000",
            PilotAssignments = []
        });

        var readyCommand = new UpdatePlayerStatusCommand
        {
            PlayerStatus = PlayerStatus.Ready,
            PlayerId = player.Id,
            GameOriginId = _sut.Id 
        };

        // Act
        _sut.SetPlayerReady(readyCommand);

        // Assert
        _commandPublisher.Received(1).PublishCommand(Arg.Is<UpdatePlayerStatusCommand>(cmd => 
            cmd.PlayerId == player.Id && 
            cmd.PlayerStatus == PlayerStatus.Ready &&
            cmd.GameOriginId == _sut.Id
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
        _sut.HandleCommand(command);
        
        // Assert
        _sut.TurnPhase.ShouldBe(PhaseNames.End);
    }
    
    [Fact]
    public void ChangeActivePlayer_ShouldProcessCommand()
    {
        // Arrange
        var player = new Player(Guid.NewGuid(), "Player1", PlayerControlType.Human);
        _sut.HandleCommand(new JoinGameCommand
        {
            PlayerId = player.Id,
            GameOriginId = Guid.NewGuid(),
            PlayerName = player.Name,
            Units = [],
            Tint = "#FF0000",
            PilotAssignments = []
        });
        var actualPlayer = _sut.Players.FirstOrDefault(p => p.Id == player.Id);
        var command = new ChangeActivePlayerCommand
        {
            GameOriginId = Guid.NewGuid(),
            PlayerId = player.Id,
            UnitsToPlay = 0
        };
        
        // Act
        _sut.HandleCommand(command);
        
        // Assert
        _sut.PhaseStepState?.ActivePlayer.ShouldBe(actualPlayer);
        actualPlayer!.Name.ShouldBe(player.Name);
        actualPlayer.Id.ShouldBe(player.Id);
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
            Tint = "#FF0000",
            PilotAssignments = []
        };

        // Act
        _sut.HandleCommand(joinCommand);

        // Assert
        _sut.CommandLog.Count.ShouldBe(1);
        _sut.CommandLog[0].ShouldBeEquivalentTo(joinCommand);
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
            GameOriginId = _sut.Id,
            Tint = "#FF0000",
            PilotAssignments = []
        };

        // Act
        _sut.HandleCommand(command);

        // Assert
        _sut.CommandLog.ShouldBeEmpty();
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
            Tint = "#FF0000",
            PilotAssignments = []
        };
        var receivedCommands = new List<IGameCommand>();
        using var subscription = _sut.Commands.Subscribe(cmd => receivedCommands.Add(cmd));

        // Act
        _sut.HandleCommand(joinCommand);

        // Assert
        receivedCommands.Count.ShouldBe(1);
        receivedCommands.First().ShouldBeEquivalentTo(joinCommand);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void DeployUnit_ShouldPublishCommand_WhenActivePlayerExists(bool isLocalPlayer)
    {
        // Arrange
        var player = new Player(Guid.NewGuid(), "Player1", PlayerControlType.Human);
        var unitData = MechFactoryTests.CreateDummyMechData();
        unitData.Id= Guid.NewGuid();
        if (isLocalPlayer)
        {
            _sut.JoinGameWithUnits(player,[unitData],[]);
        }

        _sut.HandleCommand(new JoinGameCommand
        {
            PlayerId = player.Id,
            GameOriginId = Guid.NewGuid(),
            PlayerName = player.Name,
            Units = [unitData],
            Tint = "#FF0000",
            PilotAssignments = [],
            IdempotencyKey = _idempotencyKey
        });

        _sut.HandleCommand(new ChangeActivePlayerCommand
        {
            GameOriginId = Guid.NewGuid(),
            PlayerId = player.Id,
            UnitsToPlay = 1
        });

        var deployCommand = new DeployUnitCommand
        {
            GameOriginId = _sut.Id,
            PlayerId = player.Id,
            Position = new  HexCoordinateData(1, 1), 
            Direction = 0,
            UnitId = unitData.Id.Value
        };

        // Act
        _sut.DeployUnit(deployCommand);

        // Assert
        if (isLocalPlayer)
        {
            _commandPublisher.Received(1).PublishCommand(Arg.Is<DeployUnitCommand>(cmd =>
                cmd.PlayerId == player.Id &&
                cmd.Position == deployCommand.Position &&
                cmd.GameOriginId == _sut.Id));
        }
        else
        {
            _commandPublisher.DidNotReceive().PublishCommand(Arg.Any<DeployUnitCommand>());
        }
    }

    [Fact]
    public void DeployUnit_ShouldNotPublishCommand_WhenNoActivePlayer()
    {
        // Arrange
        var deployCommand = new DeployUnitCommand
        {
            GameOriginId = _sut.Id,
            PlayerId = Guid.NewGuid(),
            Position = new HexCoordinateData(1,1),
            Direction = 0,
            UnitId = Guid.NewGuid()
        };
    
        // Act
        _sut.DeployUnit(deployCommand);
    
        // Assert
        _commandPublisher.DidNotReceive().PublishCommand(Arg.Any<DeployUnitCommand>());
    }
    
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void MoveUnit_ShouldPublishCommand_WhenActivePlayerExists(bool isLocalPlayer)
    {
        // Arrange
        var player = new Player(Guid.NewGuid(), "Player1", PlayerControlType.Human);
        var unitData = MechFactoryTests.CreateDummyMechData();
        unitData.Id = Guid.NewGuid();
        if (isLocalPlayer)
        {
            _sut.JoinGameWithUnits(player, [],[]);
        }
        _sut.HandleCommand(new JoinGameCommand
        {
            PlayerId = player.Id,
            GameOriginId = Guid.NewGuid(),
            PlayerName = player.Name,
            Units = [unitData],
            Tint = "#FF0000",
            PilotAssignments = [],
            IdempotencyKey = _idempotencyKey
        });
        _sut.HandleCommand(new ChangeActivePlayerCommand
        {
            GameOriginId = Guid.NewGuid(),
            PlayerId = player.Id,
            UnitsToPlay = 1
        });
    
        var moveCommand = new MoveUnitCommand
        {
            GameOriginId = _sut.Id,
            PlayerId = player.Id,
            MovementType = MovementType.Walk,
            UnitId = unitData.Id.Value,
            MovementPath = [new PathSegment(new HexPosition(1, 1, HexDirection.Top), new HexPosition(2, 2, HexDirection.Top), 1).ToData()]
        };
    
        // Act
        _sut.MoveUnit(moveCommand);

        // Assert
        if (isLocalPlayer)
        {
            _commandPublisher.Received(1).PublishCommand(Arg.Is<MoveUnitCommand>(cmd =>
                cmd.PlayerId == player.Id &&
                cmd.MovementType == moveCommand.MovementType &&
                cmd.GameOriginId == _sut.Id));
        }
        else
        {
            _commandPublisher.DidNotReceive().PublishCommand(Arg.Any<MoveUnitCommand>());
        }
    }
    
    [Fact]
    public void MoveUnit_ShouldNotPublishCommand_WhenNoActivePlayer()
    {
        // Arrange
        var moveCommand = new MoveUnitCommand
        {
            GameOriginId = _sut.Id,
            PlayerId = Guid.NewGuid(),
            MovementType = MovementType.Walk,
            UnitId = Guid.NewGuid(),
            MovementPath = [new PathSegment(new HexPosition(1, 1, HexDirection.Top), new HexPosition(2, 2, HexDirection.Top), 1).ToData()]
        };
    
        // Act
        _sut.MoveUnit(moveCommand);
    
        // Assert
        _commandPublisher.DidNotReceive().PublishCommand(Arg.Any<MoveUnitCommand>());
    }

    [Fact]
    public void HandleCommand_ShouldDeployUnit_WhenDeployUnitCommandIsReceived()
    {
        // Arrange
        var player = new Player(Guid.NewGuid(), "Player1", PlayerControlType.Human);
        var unitData = MechFactoryTests.CreateDummyMechData();
        unitData.Id = Guid.NewGuid();
        _sut.HandleCommand(new JoinGameCommand
        {
            PlayerId = player.Id,
            GameOriginId = Guid.NewGuid(),
            PlayerName = player.Name,
            Units = [unitData],
            Tint = "#FF0000",
            PilotAssignments = [] 
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
        _sut.HandleCommand(deployCommand);

        // Assert
        var deployedUnit = _sut.Players.First().Units.First();
        deployedUnit.IsDeployed.ShouldBeTrue();
        deployedUnit.Position!.Coordinates.Q.ShouldBe(1);
        deployedUnit.Position.Coordinates.R.ShouldBe(1);
        deployedUnit.Position.Facing.ShouldBe(HexDirection.Top);
    }

    [Fact]
    public void HandleCommand_ShouldNotDeployUnit_WhenUnitIsAlreadyDeployed()
    {
        // Arrange
        var player = new Player(Guid.NewGuid(), "Player1", PlayerControlType.Human);
        var unitData = MechFactoryTests.CreateDummyMechData();
        unitData.Id = Guid.NewGuid();
        _sut.HandleCommand(new JoinGameCommand
        {
            PlayerId = player.Id,
            GameOriginId = Guid.NewGuid(),
            PlayerName = player.Name,
            Units = [unitData],
            Tint = "#FF0000",
            PilotAssignments = []
        });

        var firstDeployCommand = new DeployUnitCommand
        {
            GameOriginId = Guid.NewGuid(),
            PlayerId = player.Id,
            Position = new HexCoordinateData(1, 1),
            Direction = 0,
            UnitId = unitData.Id.Value
        };
        _sut.HandleCommand(firstDeployCommand);

        var initialPosition = _sut.Players.First().Units.First().Position;

        var secondDeployCommand = new DeployUnitCommand
        {
            GameOriginId = Guid.NewGuid(),
            PlayerId = player.Id,
            Position = new HexCoordinateData(2, 2),
            Direction = 1,
            UnitId = unitData.Id.Value
        };

        // Act
        _sut.HandleCommand(secondDeployCommand);

        // Assert
        var unit = _sut.Players.First().Units.First();
        unit.Position.ShouldBe(initialPosition);
    }

    [Fact]
    public void HandleCommand_ShouldMoveUnit_WhenMoveUnitCommandIsReceived()
    {
        // Arrange
        var player = new Player(Guid.NewGuid(), "Player1", PlayerControlType.Human);
        var unitData = MechFactoryTests.CreateDummyMechData();
        unitData.Id = Guid.NewGuid();
        _sut.HandleCommand(new JoinGameCommand
        {
            PlayerId = player.Id,
            GameOriginId = Guid.NewGuid(),
            PlayerName = player.Name,
            Units = [unitData],
            Tint = "#FF0000",
            PilotAssignments = []
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
        _sut.HandleCommand(deployCommand);

        var moveCommand = new MoveUnitCommand
        {
            GameOriginId = Guid.NewGuid(),
            PlayerId = player.Id,
            MovementType = MovementType.Walk,
            UnitId = unitData.Id.Value,
            MovementPath = [new PathSegment(new HexPosition(1, 1, HexDirection.Top), new HexPosition(2, 2, HexDirection.Top), 1).ToData()]
        };

        // Act
        _sut.HandleCommand(moveCommand);

        // Assert
        var movedUnit = _sut.Players[0].Units[0];
        movedUnit.Position!.Coordinates.Q.ShouldBe(2);
        movedUnit.Position.Coordinates.R.ShouldBe(2);
        movedUnit.Position.Facing.ShouldBe(HexDirection.Top);
    }

    [Fact]
    public void HandleCommand_ShouldNotMoveUnit_WhenUnitDoesNotExist()
    {
        // Arrange
        var player = new Player(Guid.NewGuid(), "Player1", PlayerControlType.Human);
        _sut.HandleCommand(new JoinGameCommand
        {
            PlayerId = player.Id,
            GameOriginId = Guid.NewGuid(),
            PlayerName = player.Name,
            Units = [],
            Tint = "#FF0000",
            PilotAssignments = []
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
        Should.NotThrow(() => _sut.HandleCommand(moveCommand));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ConfigureUnitWeapons_ShouldPublishCommand_WhenActivePlayerExists(bool isLocalPlayer)
    {
        // Arrange
        var player = new Player(Guid.NewGuid(), "Player1", PlayerControlType.Human);
        var unitData = MechFactoryTests.CreateDummyMechData();
        unitData.Id = Guid.NewGuid();
        if (isLocalPlayer)
        {
            _sut.JoinGameWithUnits(player, [unitData],[]);
        }

        _sut.HandleCommand(new JoinGameCommand
        {
            PlayerId = player.Id,
            GameOriginId = Guid.NewGuid(),
            PlayerName = player.Name,
            Units = [unitData],
            Tint = "#FF0000",
            PilotAssignments = [],
            IdempotencyKey = _idempotencyKey
        });

        _sut.HandleCommand(new ChangeActivePlayerCommand
        {
            GameOriginId = Guid.NewGuid(),
            PlayerId = player.Id,
            UnitsToPlay = 1
        });

        var command = new WeaponConfigurationCommand
        {
            GameOriginId = _sut.Id,
            PlayerId = player.Id,
            UnitId = unitData.Id.Value,
            Configuration = new WeaponConfiguration
            {
                Type = WeaponConfigurationType.TorsoRotation,
                Value = 1
            }
        };

        // Act
        _sut.ConfigureUnitWeapons(command);

        // Assert
        if (isLocalPlayer)
        {
            _commandPublisher.Received(1).PublishCommand(Arg.Is<WeaponConfigurationCommand>(cmd =>
                cmd.PlayerId == command.PlayerId &&
                cmd.UnitId == command.UnitId &&
                cmd.GameOriginId == command.GameOriginId &&
                cmd.IdempotencyKey.HasValue)); // Should have an idempotency key
        }
        else
        {
            _commandPublisher.DidNotReceive().PublishCommand(command);
        }
    }

    [Fact]
    public void ConfigureUnitWeapons_ShouldNotPublishCommand_WhenNoActivePlayer()
    {
        // Arrange
        var command = new WeaponConfigurationCommand
        {
            GameOriginId = Guid.NewGuid(),
            PlayerId = Guid.NewGuid(),
            UnitId = Guid.NewGuid(),
            Configuration = new WeaponConfiguration
            {
                Type = WeaponConfigurationType.TorsoRotation,
                Value = 1
            }
        };

        // Act
        _sut.ConfigureUnitWeapons(command);

        // Assert
        _commandPublisher.DidNotReceive().PublishCommand(command);
    }

    [Fact]
    public void HandleCommand_ShouldRotateTorso_WhenWeaponConfigurationCommandIsReceived()
    {
        // Arrange
        var player = new Player(Guid.NewGuid(), "Player1", PlayerControlType.Human);
        var unitData = MechFactoryTests.CreateDummyMechData();
        unitData.Id = Guid.NewGuid();
        _sut.HandleCommand(new JoinGameCommand
        {
            PlayerId = player.Id,
            GameOriginId = Guid.NewGuid(),
            PlayerName = player.Name,
            Units = [unitData],
            Tint = "#FF0000",
            PilotAssignments = []
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
        _sut.HandleCommand(deployCommand);

        var configCommand = new WeaponConfigurationCommand
        {
            GameOriginId = Guid.NewGuid(),
            PlayerId = player.Id,
            UnitId = unitData.Id.Value,
            Configuration = new WeaponConfiguration
            {
                Type = WeaponConfigurationType.TorsoRotation,
                Value = (int)HexDirection.TopRight
            }
        };

        // Act
        _sut.HandleCommand(configCommand);

        // Assert
        var unit = _sut.Players[0].Units[0];
        (unit as Mech)!.TorsoDirection.ShouldBe(HexDirection.TopRight);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void DeclareWeaponAttack_ShouldPublishCommand_WhenActivePlayerExists(bool isLocalPlayer)
    {
        // Arrange
        var player = new Player(Guid.NewGuid(), "Player1", PlayerControlType.Human);
        var unitData = MechFactoryTests.CreateDummyMechData();
        unitData.Id = Guid.NewGuid();
        if (isLocalPlayer)
        {
            _sut.JoinGameWithUnits(player,[],[]);
        }
        _sut.HandleCommand(new JoinGameCommand
        {
            PlayerId = player.Id,
            GameOriginId = Guid.NewGuid(),
            PlayerName = player.Name,
            Units = [unitData],
            Tint = "#FF0000",
            PilotAssignments = [],
            IdempotencyKey = _idempotencyKey
        });

        _sut.HandleCommand(new ChangeActivePlayerCommand
        {
            GameOriginId = Guid.NewGuid(),
            PlayerId = player.Id,
            UnitsToPlay = 1
        });

        var targetPlayer = new Player(Guid.NewGuid(), "Player2", PlayerControlType.Human);
        var targetUnitData = MechFactoryTests.CreateDummyMechData();
        targetUnitData.Id = Guid.NewGuid();
        _sut.HandleCommand(new JoinGameCommand
        {
            PlayerId = targetPlayer.Id,
            GameOriginId = Guid.NewGuid(),
            PlayerName = targetPlayer.Name,
            Units = [targetUnitData],
            Tint = "#00FF00",
            PilotAssignments = []
        });

        var command = new WeaponAttackDeclarationCommand
        {
            GameOriginId = _sut.Id,
            PlayerId = player.Id,
            UnitId = unitData.Id.Value,
            WeaponTargets =
            [
                new WeaponTargetData()
                {
                    Weapon = new ComponentData
                    {
                        Name = "Medium Laser",
                        Type = MakaMekComponent.MediumLaser,
                        Assignments = [new LocationSlotAssignment(PartLocation.RightArm, 1, 2)]
                    },
                    TargetId = targetUnitData.Id.Value,
                    IsPrimaryTarget = true
                }
            ]
        };

        // Act
        _sut.DeclareWeaponAttack(command);

        // Assert
        if (isLocalPlayer)
        {
            _commandPublisher.Received(1).PublishCommand(Arg.Is<WeaponAttackDeclarationCommand>(cmd =>
                cmd.PlayerId == command.PlayerId &&
                cmd.UnitId == command.UnitId &&
                cmd.GameOriginId == command.GameOriginId &&
                cmd.IdempotencyKey.HasValue)); // Should have an idempotency key
        }
        else
        {
            _commandPublisher.DidNotReceive().PublishCommand(command);
        }
    }

    [Fact]
    public void DeclareWeaponAttack_ShouldNotPublishCommand_WhenNoActivePlayer()
    {
        // Arrange
        var command = new WeaponAttackDeclarationCommand
        {
            GameOriginId = Guid.NewGuid(),
            PlayerId = Guid.NewGuid(),
            UnitId = Guid.NewGuid(),
            WeaponTargets =
            [
                new WeaponTargetData
                {
                    Weapon = new ComponentData
                    {
                        Name = "Medium Laser",
                        Type = MakaMekComponent.MediumLaser,
                        Assignments = [new LocationSlotAssignment(PartLocation.RightArm, 1, 2)]
                    },
                    TargetId = Guid.NewGuid(),
                    IsPrimaryTarget = true
                }
            ]
        };

        // Act
        _sut.DeclareWeaponAttack(command);

        // Assert
        _commandPublisher.DidNotReceive().PublishCommand(Arg.Any<WeaponAttackDeclarationCommand>());
    }

    [Fact]
    public void HandleCommand_ShouldDeclareWeaponAttack_WhenWeaponAttackDeclarationCommandIsReceived()
    {
        // Arrange
        var attackerPlayer = new Player(Guid.NewGuid(), "Attacker", PlayerControlType.Human);
        var attackerUnitData = MechFactoryTests.CreateDummyMechData();
        attackerUnitData.Id = Guid.NewGuid();
        _sut.HandleCommand(new JoinGameCommand
        {
            PlayerId = attackerPlayer.Id,
            GameOriginId = Guid.NewGuid(),
            PlayerName = attackerPlayer.Name,
            Units = [attackerUnitData],
            Tint = "#FF0000",
            PilotAssignments = []
        });

        // Deploy the attacker unit
        var deployCommand = new DeployUnitCommand
        {
            GameOriginId = Guid.NewGuid(),
            PlayerId = attackerPlayer.Id,
            Position = new HexCoordinateData(1, 1),
            Direction = 0,
            UnitId = attackerUnitData.Id.Value
        };
        _sut.HandleCommand(deployCommand);

        // Add a target player and unit
        var targetPlayer = new Player(Guid.NewGuid(), "Target", PlayerControlType.Human);
        var targetUnitData = MechFactoryTests.CreateDummyMechData();
        targetUnitData.Id = Guid.NewGuid();
        _sut.HandleCommand(new JoinGameCommand
        {
            PlayerId = targetPlayer.Id,
            GameOriginId = Guid.NewGuid(),
            PlayerName = targetPlayer.Name,
            Units = [targetUnitData],
            Tint = "#00FF00",
            PilotAssignments = []
        });

        // Deploy the target unit
        var deployTargetCommand = new DeployUnitCommand
        {
            GameOriginId = Guid.NewGuid(),
            PlayerId = targetPlayer.Id,
            Position = new HexCoordinateData(2, 2),
            Direction = 0,
            UnitId = targetUnitData.Id.Value
        };
        _sut.HandleCommand(deployTargetCommand);

        // Create weapon attack declaration command
        var weaponAttackCommand = new WeaponAttackDeclarationCommand
        {
            GameOriginId = Guid.NewGuid(),
            PlayerId = attackerPlayer.Id,
            UnitId = attackerUnitData.Id.Value,
            WeaponTargets =
            [
                new WeaponTargetData
                {
                    Weapon = new ComponentData
                    {
                        Name = "Medium Laser",
                        Type = MakaMekComponent.MediumLaser,
                        Assignments = [new LocationSlotAssignment(PartLocation.RightArm, 1, 2)]
                    },
                    TargetId = targetUnitData.Id.Value,
                    IsPrimaryTarget = true
                }
            ]
        };
        var attackerUnit = _sut.Players.First(p => p.Id == attackerPlayer.Id).Units.First();
        attackerUnit.AssignPilot(new MechWarrior("John", "Doe"));
        
        // Act
        _sut.HandleCommand(weaponAttackCommand);

        // Assert
        // Verify that the unit has declared a weapon attack
        attackerUnit.HasDeclaredWeaponAttack.ShouldBeTrue();
    }

    [Fact]
    public void HandleCommand_ShouldApplyDamage_WhenWeaponAttackResolutionCommandIsReceived()
    {
        // Arrange
        // Add target player and unit
        var targetPlayerId = Guid.NewGuid();
        var targetUnitData = MechFactoryTests.CreateDummyMechData();
        targetUnitData.Id = Guid.NewGuid();
        var targetJoinCommand = new JoinGameCommand
        {
            PlayerId = targetPlayerId,
            PlayerName = "Target",
            GameOriginId = Guid.NewGuid(),
            Units = [targetUnitData],
            Tint = "#00FF00",
            PilotAssignments = []
        };
        _sut.HandleCommand(targetJoinCommand);
        var targetPlayer = _sut.Players.First(p => p.Id == targetPlayerId);
        var targetMech = targetPlayer.Units.First() as Mech;
        targetMech!.Deploy(new HexPosition(new HexCoordinates(1, 2), HexDirection.Top));
        
        // Create hit locations data
        var hitLocations = new List<LocationHitData>
        {
            CreateHitDataForLocation(PartLocation.CenterTorso, 5, [],[]),
            CreateHitDataForLocation(PartLocation.LeftArm, 3, [],[])
        };
        
        // Create the attack resolution command
        var attackResolutionCommand = new WeaponAttackResolutionCommand
        {
            GameOriginId = Guid.NewGuid(),
            PlayerId = Guid.NewGuid(),
            AttackerId = targetMech.Id,
            TargetId = targetMech.Id,
            WeaponData = new ComponentData
            {
                Name = "Test Weapon",
                Type = MakaMekComponent.MachineGun,
                Assignments = [new LocationSlotAssignment(PartLocation.RightArm, 0, 2)]
            },
            ResolutionData = new AttackResolutionData(
                10,
                [],
                true,
                HitDirection.Front,
                0,
                new AttackHitLocationsData(hitLocations, 8, [], 0))
        };

        // Get initial armor values for verification
        var centerTorsoPart = targetMech.Parts[PartLocation.CenterTorso];
        var leftArmPart = targetMech.Parts[PartLocation.LeftArm];
        var initialCenterTorsoArmor = centerTorsoPart.CurrentArmor;
        var initialLeftArmArmor = leftArmPart.CurrentArmor;

        // Act
        _sut.HandleCommand(attackResolutionCommand);

        // Assert
        // Verify that armor was reduced by the damage amount
        centerTorsoPart.CurrentArmor.ShouldBe(initialCenterTorsoArmor - 5);
        leftArmPart.CurrentArmor.ShouldBe(initialLeftArmArmor - 3);
    }

    [Fact]
    public void HandleCommand_ShouldApplyHeat_WhenHeatUpdatedCommandIsReceived()
    {
        // Arrange
        // Add player and unit
        var playerId = Guid.NewGuid();
        var unitData = MechFactoryTests.CreateDummyMechData();
        unitData.Id = Guid.NewGuid();
        var joinCommand = new JoinGameCommand
        {
            PlayerId = playerId,
            PlayerName = "Player1",
            GameOriginId = Guid.NewGuid(),
            Units = [unitData],
            Tint = "#FF0000",
            PilotAssignments = []
        };
        _sut.HandleCommand(joinCommand);
        
        // Get the unit and check initial heat
        var unit = _sut.Players.First(p => p.Id == playerId).Units.First();
        var initialHeat = unit.CurrentHeat;
        
        // Create heat data
        var heatData = new HeatData
        {
            MovementHeatSources =
            [
                new MovementHeatData
                {
                    MovementType = MovementType.Run,
                    MovementPointsSpent = 5,
                    HeatPoints = 12
                }
            ],
            WeaponHeatSources =
            [
                new WeaponHeatData
                {
                    WeaponName = "Medium Laser",
                    HeatPoints = 13
                }
            ],
            ExternalHeatSources = [],
            DissipationData = new HeatDissipationData
            {
                HeatSinks = 10,
                EngineHeatSinks = 10,
                DissipationPoints =20
            }
        };
        
        // Create the heat update command
        var heatUpdateCommand = new HeatUpdatedCommand
        {
            GameOriginId = Guid.NewGuid(),
            UnitId = unitData.Id.Value,
            HeatData = heatData,
            PreviousHeat = initialHeat,
            Timestamp = DateTime.UtcNow
        };
        
        // Act
        _sut.HandleCommand(heatUpdateCommand);
        
        // Assert
        unit.CurrentHeat.ShouldBe(5); //0+25-20
    }
    
    [Fact]
    public void HandleCommand_ShouldNotApplyHeat_WhenHeatUpdatedCommandIsReceived_WithWrongUnitId()
    {
        // Arrange
        // Add player and unit
        var playerId = Guid.NewGuid();
        var unitData = MechFactoryTests.CreateDummyMechData();
        unitData.Id = Guid.NewGuid();
        var joinCommand = new JoinGameCommand
        {
            PlayerId = playerId,
            PlayerName = "Player1",
            GameOriginId = Guid.NewGuid(),
            Units = [unitData],
            Tint = "#FF0000",
            PilotAssignments = []
        };
        _sut.HandleCommand(joinCommand);
        
        // Get the unit and check initial heat
        var unit = _sut.Players.First(p => p.Id == playerId).Units.First();
        var initialHeat = unit.CurrentHeat;
        
        // Create heat data
        var heatData = new HeatData
        {
            MovementHeatSources =
            [
                new MovementHeatData
                {
                    MovementType = MovementType.Run,
                    MovementPointsSpent = 5,
                    HeatPoints = 12
                }
            ],
            WeaponHeatSources =
            [
                new WeaponHeatData
                {
                    WeaponName = "Medium Laser",
                    HeatPoints = 13
                }
            ],
            ExternalHeatSources = [],
            DissipationData = new HeatDissipationData
            {
                HeatSinks = 10,
                EngineHeatSinks = 10,
                DissipationPoints =20
            }
        };
        
        // Create the heat update command
        var heatUpdateCommand = new HeatUpdatedCommand
        {
            GameOriginId = Guid.NewGuid(),
            UnitId = Guid.NewGuid(),
            HeatData = heatData,
            PreviousHeat = initialHeat,
            Timestamp = DateTime.UtcNow
        };
        
        // Act
        _sut.HandleCommand(heatUpdateCommand);
        
        // Assert
        unit.CurrentHeat.ShouldBe(initialHeat);
    }
    
    [Fact]
    public void HandleCommand_ShouldNotApplyHeat_WhenHeatUpdatedCommandIsReceivedSecondTime()
    {
        // Arrange
        // Add player and unit
        var playerId = Guid.NewGuid();
        var unitData = MechFactoryTests.CreateDummyMechData();
        unitData.Id = Guid.NewGuid();
        var joinCommand = new JoinGameCommand
        {
            PlayerId = playerId,
            PlayerName = "Player1",
            GameOriginId = Guid.NewGuid(),
            Units = [unitData],
            Tint = "#FF0000",
            PilotAssignments = []
        };
        _sut.HandleCommand(joinCommand);
        
        // Get the unit and check initial heat
        var unit = _sut.Players.First(p => p.Id == playerId).Units[0];
        var initialHeat = unit.CurrentHeat;
        
        // Create heat data
        var heatData = new HeatData
        {
            MovementHeatSources =
            [
                new MovementHeatData
                {
                    MovementType = MovementType.Run,
                    MovementPointsSpent = 5,
                    HeatPoints = 12
                }
            ],
            WeaponHeatSources =
            [
                new WeaponHeatData
                {
                    WeaponName = "Medium Laser",
                    HeatPoints = 13
                }
            ],
            ExternalHeatSources = [],
            DissipationData = new HeatDissipationData
            {
                HeatSinks = 10,
                EngineHeatSinks = 10,
                DissipationPoints =20
            }
        };
        
        // Create the heat update command
        var heatUpdateCommand = new HeatUpdatedCommand
        {
            GameOriginId = Guid.NewGuid(),
            UnitId = unitData.Id.Value,
            HeatData = heatData,
            PreviousHeat = initialHeat,
            Timestamp = DateTime.UtcNow
        };
        
        // Act
        _sut.HandleCommand(heatUpdateCommand);
        _sut.HandleCommand(heatUpdateCommand);
        
        // Assert
        unit.CurrentHeat.ShouldBe(5); //0+25-20
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void EndTurn_ShouldPublishCommand_WhenActivePlayerExists(bool isLocalPlayer)
    {
        // Arrange
        var player = new Player(Guid.NewGuid(), "Player1", PlayerControlType.Human);
        var unitData = MechFactoryTests.CreateDummyMechData();
        unitData.Id = Guid.NewGuid();
        if (isLocalPlayer)
        {
            _sut.JoinGameWithUnits(player,[],[]);
        }
        _sut.HandleCommand(new JoinGameCommand
        {
            PlayerId = player.Id,
            GameOriginId = Guid.NewGuid(),
            PlayerName = player.Name,
            Units = [unitData],
            Tint = "#FF0000",
            PilotAssignments = [],
            IdempotencyKey = _idempotencyKey
        });
        _sut.HandleCommand(new ChangePhaseCommand
        {
            GameOriginId = Guid.NewGuid(),
            Phase = PhaseNames.End
        });
        _sut.HandleCommand(new ChangeActivePlayerCommand
        {
            GameOriginId = Guid.NewGuid(),
            PlayerId = player.Id,
            UnitsToPlay = 0
        });


        var turnEndedCommand = new TurnEndedCommand
        {
            GameOriginId = _sut.Id,
            PlayerId = player.Id,
            Timestamp = DateTime.UtcNow
        };

        // Act
        _sut.EndTurn(turnEndedCommand);

        // Assert
        if (isLocalPlayer)
        {
            _commandPublisher.Received(1).PublishCommand(Arg.Is<TurnEndedCommand>(cmd =>
                cmd.PlayerId == player.Id &&
                cmd.GameOriginId == _sut.Id));
        }
        else
        {
            _commandPublisher.DidNotReceive().PublishCommand(Arg.Any<TurnEndedCommand>());
        }
    }

    [Fact]
    public void EndTurn_ShouldNotPublishCommand_WhenNoActivePlayer()
    {
        // Arrange
        var turnEndedCommand = new TurnEndedCommand
        {
            GameOriginId = _sut.Id,
            PlayerId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow
        };

        // Act
        _sut.EndTurn(turnEndedCommand);

        // Assert
        _commandPublisher.DidNotReceive().PublishCommand(Arg.Any<TurnEndedCommand>());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ShutdownUnit_ShouldPublishCommand_WhenActivePlayerExists(bool isLocalPlayer)
    {
        // Arrange
        var player = new Player(Guid.NewGuid(), "Player1", PlayerControlType.Human);
        var unitData = MechFactoryTests.CreateDummyMechData();
        unitData.Id = Guid.NewGuid();

        var joinCommand = new JoinGameCommand
        {
            PlayerId = player.Id,
            PlayerName = player.Name,
            GameOriginId = Guid.NewGuid(),
            Tint = player.Tint,
            Units = [unitData],
            PilotAssignments = [],
            IdempotencyKey = _idempotencyKey
        };

        _sut.HandleCommand(joinCommand);

        if (isLocalPlayer)
        {
            _sut.JoinGameWithUnits(player, [unitData],[]);
        }

        _sut.HandleCommand(new ChangeActivePlayerCommand
        {
            GameOriginId = Guid.NewGuid(),
            PlayerId = player.Id,
            UnitsToPlay = 0
        });

        var unit = _sut.Players.First(p => p.Id == player.Id).Units.First();
        var shutdownCommand = new ShutdownUnitCommand
        {
            GameOriginId = _sut.Id,
            PlayerId = player.Id,
            UnitId = unit.Id,
            Timestamp = DateTime.UtcNow
        };

        _commandPublisher.ClearReceivedCalls();

        // Act
        _sut.ShutdownUnit(shutdownCommand);

        // Assert
        if (isLocalPlayer)
        {
            _commandPublisher.Received(1).PublishCommand(Arg.Is<ShutdownUnitCommand>(cmd =>
                cmd.PlayerId == player.Id &&
                cmd.UnitId == unit.Id));
        }
        else
        {
            _commandPublisher.DidNotReceive().PublishCommand(Arg.Any<ShutdownUnitCommand>());
        }
    }

    [Fact]
    public void ShutdownUnit_ShouldNotPublishCommand_WhenNoActivePlayer()
    {
        // Arrange
        var shutdownCommand = new ShutdownUnitCommand
        {
            GameOriginId = _sut.Id,
            PlayerId = Guid.NewGuid(),
            UnitId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow
        };

        // Act
        _sut.ShutdownUnit(shutdownCommand);

        // Assert
        _commandPublisher.DidNotReceive().PublishCommand(Arg.Any<ShutdownUnitCommand>());
    }
    
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void StartupUnit_ShouldPublishCommand_WhenActivePlayerExists(bool isLocalPlayer)
    {
        // Arrange
        var player = new Player(Guid.NewGuid(), "Player1", PlayerControlType.Human);
        var unitData = MechFactoryTests.CreateDummyMechData();
        unitData.Id = Guid.NewGuid();

        var joinCommand = new JoinGameCommand
        {
            PlayerId = player.Id,
            PlayerName = player.Name,
            GameOriginId = Guid.NewGuid(),
            Tint = player.Tint,
            Units = [unitData],
            PilotAssignments = [],
            IdempotencyKey = _idempotencyKey
        };

        _sut.HandleCommand(joinCommand);

        if (isLocalPlayer)
        {
            _sut.JoinGameWithUnits(player, [unitData],[]);
        }

        _sut.HandleCommand(new ChangeActivePlayerCommand
        {
            GameOriginId = Guid.NewGuid(),
            PlayerId = player.Id,
            UnitsToPlay = 0
        });

        var unit = _sut.Players.First(p => p.Id == player.Id).Units.First();
        var startupUnitCommand = new StartupUnitCommand
        {
            GameOriginId = _sut.Id,
            PlayerId = player.Id,
            UnitId = unit.Id,
            Timestamp = DateTime.UtcNow
        };

        _commandPublisher.ClearReceivedCalls();

        // Act
        _sut.StartupUnit(startupUnitCommand);

        // Assert
        if (isLocalPlayer)
        {
            _commandPublisher.Received(1).PublishCommand(Arg.Is<StartupUnitCommand>(cmd =>
                cmd.PlayerId == player.Id &&
                cmd.UnitId == unit.Id));
        }
        else
        {
            _commandPublisher.DidNotReceive().PublishCommand(Arg.Any<StartupUnitCommand>());
        }
    }
    
    [Fact]
    public void StartupUnit_ShouldNotPublishCommand_WhenNoActivePlayer()
    {
        // Arrange
        var startupUnitCommand = new StartupUnitCommand
        {
            GameOriginId = _sut.Id,
            PlayerId = Guid.NewGuid(),
            UnitId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow
        };

        // Act
        _sut.StartupUnit(startupUnitCommand);

        // Assert
        _commandPublisher.DidNotReceive().PublishCommand(Arg.Any<StartupUnitCommand>());
    }

    [Fact]
    public void HandleCommand_ShouldClearPlayersEndedTurnAndSetFirstLocalPlayerAsActive_WhenEnteringEndPhase()
    {
        // Arrange
        var localPlayer1 = new Player(Guid.NewGuid(), "LocalPlayer1", PlayerControlType.Human);
        var localPlayer2 = new Player(Guid.NewGuid(), "LocalPlayer2", PlayerControlType.Human);
        
        // Create a new client game with local players
        var battleMap = BattleMapTests.BattleMapFactory.GenerateMap(5, 5, new SingleTerrainGenerator(5,5, new ClearTerrain()));
        var commandPublisher = Substitute.For<ICommandPublisher>();
        var clientGame = new ClientGame(
            _rulesProvider, 
            new MechFactory(
                _rulesProvider,
                _componentProvider,
                Substitute.For<ILocalizationService>()),
            commandPublisher,
            Substitute.For<IToHitCalculator>(),
            Substitute.For<IPilotingSkillCalculator>(),
            Substitute.For<IConsciousnessCalculator>(),
            Substitute.For<IHeatEffectsCalculator>(),
            _mapFactory,
            _hashService);
        var unitData = MechFactoryTests.CreateDummyMechData();
        clientGame.JoinGameWithUnits(localPlayer1,[unitData],[]);
        clientGame.JoinGameWithUnits(localPlayer2,[unitData],[]);
        clientGame.SetBattleMap(battleMap);
        
        // Add the local players to the game
        
        clientGame.HandleCommand(new JoinGameCommand
        {
            PlayerId = localPlayer1.Id,
            GameOriginId = Guid.NewGuid(),
            PlayerName = localPlayer1.Name,
            Units = [unitData],
            Tint = "#FF0000",
            PilotAssignments = []
        });
        
        clientGame.HandleCommand(new JoinGameCommand
        {
            PlayerId = localPlayer2.Id,
            GameOriginId = Guid.NewGuid(),
            PlayerName = localPlayer2.Name,
            Units = [unitData],
            Tint = "#00FF00",
            PilotAssignments = []
        });
        
        // Simulate a player ending their turn (to verify it gets cleared)
        clientGame.HandleCommand(new TurnEndedCommand
        {
            GameOriginId = Guid.NewGuid(),
            PlayerId = localPlayer1.Id,
            Timestamp = DateTime.UtcNow
        });

        // Act - Change to End phase (two-stage protocol)
        clientGame.HandleCommand(new ChangePhaseCommand
        {
            GameOriginId = Guid.NewGuid(),
            Phase = PhaseNames.End
        });

        // Phase should be set, but ActivePlayer not yet (waiting for StartPhaseCommand)
        clientGame.TurnPhase.ShouldBe(PhaseNames.End);
        clientGame.PhaseStepState.ShouldBeNull();

        // Now send StartPhaseCommand to complete phase initialization
        clientGame.HandleCommand(new StartPhaseCommand
        {
            GameOriginId = Guid.NewGuid(),
            Phase = PhaseNames.End
        });

        // Assert - ActivePlayer should now be set
        clientGame.PhaseStepState.ShouldNotBeNull();
        clientGame.PhaseStepState.Value.ActivePlayer.Id.ShouldBe(localPlayer1.Id); // The first local player should be active
        
        // Verify that the player can end turn again (previous end turn state was cleared)
        clientGame.HandleCommand(new TurnEndedCommand
        {
            GameOriginId = Guid.NewGuid(),
            PlayerId = localPlayer1.Id,
            Timestamp = DateTime.UtcNow
        });
        
        // After the first local player ends turn, the second local player should become active
        clientGame.PhaseStepState.ShouldNotBeNull();
        clientGame.PhaseStepState.Value.ActivePlayer.Id.ShouldBe(localPlayer2.Id);
    }
    
    [Fact]
    public void HandleCommand_ShouldUpdateActivePlayer_WhenTurnEndedCommandIsReceivedInEndPhase()
    {
        // Arrange
        var localPlayer1 = new Player(Guid.NewGuid(), "LocalPlayer1", PlayerControlType.Human);
        var localPlayer2 = new Player(Guid.NewGuid(), "LocalPlayer2", PlayerControlType.Human);
        var localPlayer3 = new Player(Guid.NewGuid(), "LocalPlayer3", PlayerControlType.Human);
        
        var unitData = MechFactoryTests.CreateDummyMechData();
        var player1Unit = unitData with { Id = Guid.NewGuid() };
        var player2Unit = unitData with { Id = Guid.NewGuid() };
        var player3Unit = unitData with { Id = Guid.NewGuid() };
        
        // Create a new client game with local players
        var battleMap = BattleMapTests.BattleMapFactory.GenerateMap(5, 5, new SingleTerrainGenerator(5,5, new ClearTerrain()));
        var commandPublisher = Substitute.For<ICommandPublisher>();
        var rulesProvider = new ClassicBattletechRulesProvider();
        var mechFactory = new MechFactory(
            rulesProvider,
            new ClassicBattletechComponentProvider(),
            Substitute.For<ILocalizationService>());
        var sut = new ClientGame(
            rulesProvider, 
            mechFactory,
            commandPublisher,
            Substitute.For<IToHitCalculator>(),
            Substitute.For<IPilotingSkillCalculator>(),
            Substitute.For<IConsciousnessCalculator>(),
            Substitute.For<IHeatEffectsCalculator>(),
            _mapFactory,
            _hashService);
        sut.JoinGameWithUnits(localPlayer1,[player1Unit],[]);
        sut.JoinGameWithUnits(localPlayer2,[player2Unit],[]);
        sut.JoinGameWithUnits(localPlayer3,[player3Unit],[]);
        sut.SetBattleMap(battleMap);
        
        // Add the local players to the game
        sut.HandleCommand(new JoinGameCommand
        {
            PlayerId = localPlayer1.Id,
            GameOriginId = Guid.NewGuid(),
            PlayerName = localPlayer1.Name,
            Units = [player1Unit],
            Tint = "#FF0000",
            PilotAssignments = []
        });
        
        sut.HandleCommand(new JoinGameCommand
        {
            PlayerId = localPlayer2.Id,
            GameOriginId = Guid.NewGuid(),
            PlayerName = localPlayer2.Name,
            Units = [player2Unit],
            Tint = "#00FF00",
            PilotAssignments = []
        });
        
        sut.HandleCommand(new JoinGameCommand
        {
            PlayerId = localPlayer3.Id,
            GameOriginId = Guid.NewGuid(),
            PlayerName = localPlayer3.Name,
            Units = [player3Unit],
            Tint = "#0000FF",
            PilotAssignments = []
        });
        
        // Set the game to End phase
        sut.HandleCommand(new ChangePhaseCommand
        {
            GameOriginId = Guid.NewGuid(),
            Phase = PhaseNames.End
        });
        
        // Set the first local player as active
        sut.HandleCommand(new ChangeActivePlayerCommand
        {
            GameOriginId = Guid.NewGuid(),
            PlayerId = localPlayer1.Id,
            UnitsToPlay = 0
        });
        
        // Verify initial state
        sut.PhaseStepState.ShouldNotBeNull();
        sut.PhaseStepState.Value.ActivePlayer.Id.ShouldBe(localPlayer1.Id);
        
        // Act - End turn for the first player
        sut.HandleCommand(new TurnEndedCommand
        {
            GameOriginId = Guid.NewGuid(),
            PlayerId = localPlayer1.Id,
            Timestamp = DateTime.UtcNow
        });
        
        // Assert - Second player should now be active
        sut.PhaseStepState.ShouldNotBeNull();
        sut.PhaseStepState.Value.ActivePlayer.Id.ShouldBe(localPlayer2.Id);
        
        // Act - End turn for the second player
        sut.HandleCommand(new TurnEndedCommand
        {
            GameOriginId = Guid.NewGuid(),
            PlayerId = localPlayer2.Id,
            Timestamp = DateTime.UtcNow
        });
        
        // Assert - Third player should now be active
        sut.PhaseStepState.ShouldNotBeNull();
        sut.PhaseStepState.Value.ActivePlayer.Id.ShouldBe(localPlayer3.Id);
        
        // Act - End turn for the third player
        sut.HandleCommand(new TurnEndedCommand
        {
            GameOriginId = Guid.NewGuid(),
            PlayerId = localPlayer3.Id,
            Timestamp = DateTime.UtcNow
        });
        
        // Assert - No more local players who haven't ended their turn, so ActivePlayer should be null
        sut.PhaseStepState.ShouldBeNull();
    }

    [Theory]
    [InlineData(PhaseNames.Deployment, true)]
    [InlineData(PhaseNames.Initiative, false)]
    [InlineData(PhaseNames.Movement, true)]
    [InlineData(PhaseNames.WeaponsAttack, true)]
    [InlineData(PhaseNames.WeaponAttackResolution, true)]
    [InlineData(PhaseNames.PhysicalAttack, true)]
    [InlineData(PhaseNames.Heat, true)]
    [InlineData(PhaseNames.End, true)]
    public void PhaseStepState_ShouldPublishChanges_WhenSet(PhaseNames phase, bool shouldPublish)
    {
        // Arrange
        var player1 = new Player(Guid.NewGuid(), "Player1", PlayerControlType.Human);
        // Join the game
        _sut.JoinGameWithUnits(player1, [],[]);
        _sut.HandleCommand(new JoinGameCommand
        {
            PlayerId = player1.Id,
            GameOriginId = Guid.NewGuid(),
            PlayerName = player1.Name,
            Units = [],
            Tint = "#FF0000",
            PilotAssignments = []
        });
        
        _sut.HandleCommand(new ChangePhaseCommand
        {
            GameOriginId = Guid.NewGuid(),
            Phase = phase
        });
        
        var stateChanged = false;
        _sut.PhaseStepChanges.Subscribe(state =>
        {
            stateChanged = state.HasValue;
        });
        
        // Act - Set PhaseStepState via ActivePlayer command
        _sut.HandleCommand(new ChangeActivePlayerCommand
        {
            GameOriginId = Guid.NewGuid(),
            PlayerId = player1.Id,
            UnitsToPlay = 0
        });
        
        // Assert
        //await Task.Delay(100);
        stateChanged.ShouldBe(shouldPublish);
    }
    
    [Fact]
    public void HandleCommand_ShouldUpdateTurn_WhenTurnIncrementedCommandIsReceived()
    {
        // Arrange
        var initialTurn = _sut.Turn;
        var turnIncrementedCommand = new TurnIncrementedCommand
        {
            GameOriginId = Guid.NewGuid(), // Different from client game ID
            TurnNumber = initialTurn + 1
        };

        // Act
        _sut.HandleCommand(turnIncrementedCommand);

        // Assert
        _sut.Turn.ShouldBe(initialTurn + 1);
    }

    [Fact]
    public void HandleCommand_ShouldNotUpdateTurn_WhenTurnIncrementedCommandHasInvalidTurnNumber()
    {
        // Arrange
        var initialTurn = _sut.Turn;
        var turnIncrementedCommand = new TurnIncrementedCommand
        {
            GameOriginId = Guid.NewGuid(), // Different from client game ID
            TurnNumber = initialTurn + 2 // Skipping a turn should be rejected
        };

        // Act
        _sut.HandleCommand(turnIncrementedCommand);

        // Assert
        _sut.Turn.ShouldBe(initialTurn); // Turn should not change
    }

    [Fact]
    public void HandleCommand_ShouldSetBattleMap_WhenSetBattleMapCommandIsReceived()
    {
        // Arrange
        List<HexData> mapData =
        [
            new()
            {
                Coordinates = new HexCoordinateData(1, 1),
                TerrainTypes = [MakaMekTerrains.LightWoods]
            },
            new()
            {
                Coordinates = new HexCoordinateData(2, 2),
                TerrainTypes = [MakaMekTerrains.Clear]
            },
            new()
            {
                Coordinates = new HexCoordinateData(3, 3),
                TerrainTypes = [MakaMekTerrains.HeavyWoods]
            }
        ];
        
        var newBattleMap = BattleMapTests.BattleMapFactory.CreateFromData(mapData);
        
        _mapFactory.CreateFromData(Arg.Is<List<HexData>>(data => 
            data.Count == mapData.Count)).Returns(newBattleMap);
        
        var setBattleMapCommand = new SetBattleMapCommand
        {
            GameOriginId = Guid.NewGuid(), // Different from client game ID
            MapData = mapData
        };
        
        // Act
        _sut.HandleCommand(setBattleMapCommand);
        
        // Assert
        _sut.BattleMap.ShouldBe(newBattleMap);
        _mapFactory.Received(1).CreateFromData(Arg.Is<List<HexData>>(data =>
            data.Count == mapData.Count));
    }

    [Fact]
    public void HandleCommand_ShouldShutDownUnit_WhenUnitShutdownCommandIsReceived()
    {
        // Arrange
        var playerId = Guid.NewGuid();
        var unitData = MechFactoryTests.CreateDummyMechData();
        unitData.Id = Guid.NewGuid();
        var joinCommand = new JoinGameCommand
        {
            PlayerId = playerId,
            PlayerName = "Player1",
            GameOriginId = Guid.NewGuid(),
            Units = [unitData],
            Tint = "#FF0000",
            PilotAssignments = []
        };
        _sut.HandleCommand(joinCommand);

        var unit = _sut.Players.First(p => p.Id == playerId).Units.First();
        unit.IsActive.ShouldBeTrue();

        var shutdownCommand = new UnitShutdownCommand
        {
            GameOriginId = Guid.NewGuid(),
            UnitId = unitData.Id.Value,
            ShutdownData = new ShutdownData { Reason = ShutdownReason.Heat, Turn = 1 },
            IsAutomaticShutdown = true,
            Timestamp = DateTime.UtcNow
        };

        // Act
        _sut.HandleCommand(shutdownCommand);

        // Assert
        unit.IsActive.ShouldBeFalse();
        unit.IsShutdown.ShouldBeTrue();
    }

    [Fact]
    public void HandleCommand_ShouldNotShutDownUnit_WhenUnitShutdownCommandIsReceived_WithSuccessfulAvoidRoll()
    {
        // Arrange
        var playerId = Guid.NewGuid();
        var unitData = MechFactoryTests.CreateDummyMechData();
        unitData.Id = Guid.NewGuid();
        var joinCommand = new JoinGameCommand
        {
            PlayerId = playerId,
            PlayerName = "Player1",
            GameOriginId = Guid.NewGuid(),
            Units = [unitData],
            Tint = "#FF0000",
            PilotAssignments = []
        };
        _sut.HandleCommand(joinCommand);

        var unit = _sut.Players.First(p => p.Id == playerId).Units.First();

        var shutdownCommand = new UnitShutdownCommand
        {
            GameOriginId = Guid.NewGuid(),
            UnitId = unitData.Id.Value,
            ShutdownData = new ShutdownData { Reason = ShutdownReason.Heat, Turn = 1 },
            AvoidShutdownRoll = new AvoidShutdownRollData
            {
                HeatLevel = 15,
                DiceResults = [5, 6],
                AvoidNumber = 8,
                IsSuccessful = true
            },
            IsAutomaticShutdown = false,
            Timestamp = DateTime.UtcNow
        };

        // Act
        _sut.HandleCommand(shutdownCommand);

        // Assert
        unit.IsActive.ShouldBeTrue();
        unit.IsShutdown.ShouldBeFalse();
    }

    [Fact]
    public void HandleCommand_ShouldStartUpUnit_WhenUnitStartupCommandIsReceived()
    {
        // Arrange
        var playerId = Guid.NewGuid();
        var unitData = MechFactoryTests.CreateDummyMechData();
        unitData.Id = Guid.NewGuid();
        var joinCommand = new JoinGameCommand
        {
            PlayerId = playerId,
            PlayerName = "Player1",
            GameOriginId = Guid.NewGuid(),
            Units = [unitData],
            Tint = "#FF0000",
            PilotAssignments = []
        };
        _sut.HandleCommand(joinCommand);

        var unit = _sut.Players.First(p => p.Id == playerId).Units.First();

        // First shut down the unit
        unit.Shutdown(new ShutdownData { Reason = ShutdownReason.Heat, Turn = 1 });
        unit.IsShutdown.ShouldBeTrue();

        var startupCommand = new UnitStartupCommand
        {
            GameOriginId = Guid.NewGuid(),
            UnitId = unitData.Id.Value,
            IsAutomaticRestart = false,
            IsRestartPossible = true,
            AvoidShutdownRoll = new AvoidShutdownRollData
            {
                HeatLevel = 10,
                DiceResults = [4, 5],
                AvoidNumber = 8,
                IsSuccessful = true
            },
            Timestamp = DateTime.UtcNow
        };

        // Act
        _sut.HandleCommand(startupCommand);

        // Assert
        unit.IsActive.ShouldBeTrue();
        unit.IsShutdown.ShouldBeFalse();
    }

    [Fact]
    public void HandleCommand_ShouldNotStartUpUnit_WhenUnitStartupCommandIsReceived_WithFailedRestartRoll()
    {
        // Arrange
        var playerId = Guid.NewGuid();
        var unitData = MechFactoryTests.CreateDummyMechData();
        unitData.Id = Guid.NewGuid();
        var joinCommand = new JoinGameCommand
        {
            PlayerId = playerId,
            PlayerName = "Player1",
            GameOriginId = Guid.NewGuid(),
            Units = [unitData],
            Tint = "#FF0000",
            PilotAssignments = []
        };
        _sut.HandleCommand(joinCommand);

        var unit = _sut.Players.First(p => p.Id == playerId).Units.First();

        // First shut down the unit
        unit.Shutdown(new ShutdownData { Reason = ShutdownReason.Heat, Turn = 1 });
        unit.IsShutdown.ShouldBeTrue();

        var startupCommand = new UnitStartupCommand
        {
            GameOriginId = Guid.NewGuid(),
            UnitId = unitData.Id.Value,
            IsAutomaticRestart = false,
            IsRestartPossible = true,
            AvoidShutdownRoll = new AvoidShutdownRollData
            {
                HeatLevel = 15,
                DiceResults = [2, 3],
                AvoidNumber = 10,
                IsSuccessful = false
            },
            Timestamp = DateTime.UtcNow
        };

        // Act
        _sut.HandleCommand(startupCommand);

        // Assert
        unit.IsActive.ShouldBeFalse();
        unit.IsShutdown.ShouldBeTrue();
    }

    [Fact]
    public void HandleCommand_ShouldNotProcessUnitShutdownCommand_WhenUnitNotFound()
    {
        // Arrange
        var shutdownCommand = new UnitShutdownCommand
        {
            GameOriginId = Guid.NewGuid(),
            UnitId = Guid.NewGuid(), // Non-existent unit
            ShutdownData = new ShutdownData { Reason = ShutdownReason.Heat, Turn = 1 },
            IsAutomaticShutdown = true,
            Timestamp = DateTime.UtcNow
        };

        // Act & Assert - Should not throw
        Should.NotThrow(() => _sut.HandleCommand(shutdownCommand));
    }

    [Fact]
    public void HandleCommand_ShouldNotProcessUnitStartupCommand_WhenUnitNotFound()
    {
        // Arrange
        var startupCommand = new UnitStartupCommand
        {
            GameOriginId = Guid.NewGuid(),
            UnitId = Guid.NewGuid(), // Non-existent unit
            IsAutomaticRestart = false,
            IsRestartPossible = true,
            Timestamp = DateTime.UtcNow
        };

        // Act & Assert - Should not throw
        Should.NotThrow(() => _sut.HandleCommand(startupCommand));
    }

    [Fact]
    public void HandleCommand_ShouldResetTotalPhaseDamageForMultipleUnits_WhenChangePhaseCommandIsReceived()
    {
        // Arrange
        var player1Id = Guid.NewGuid();
        var player2Id = Guid.NewGuid();
        var unit1Data = MechFactoryTests.CreateDummyMechData();
        var unit2Data = MechFactoryTests.CreateDummyMechData();
        unit1Data.Id = Guid.NewGuid();
        unit2Data.Id = Guid.NewGuid();

        // Add two players with units
        _sut.HandleCommand(new JoinGameCommand
        {
            PlayerId = player1Id,
            PlayerName = "Player1",
            GameOriginId = Guid.NewGuid(),
            Units = [unit1Data],
            Tint = "#FF0000",
            PilotAssignments = []
        });

        _sut.HandleCommand(new JoinGameCommand
        {
            PlayerId = player2Id,
            PlayerName = "Player2",
            GameOriginId = Guid.NewGuid(),
            Units = [unit2Data],
            Tint = "#00FF00",
            PilotAssignments = []
        });

        // Get the units and apply damage to both
        var player1 = _sut.Players.First(p => p.Id == player1Id);
        var player2 = _sut.Players.First(p => p.Id == player2Id);
        var unit1 = player1.Units.First();
        var unit2 = player2.Units.First();

        // Apply different amounts of damage to each unit
        unit1.ApplyDamage([CreateHitDataForLocation(PartLocation.CenterTorso, 9, [],[])], HitDirection.Front);
        unit2.ApplyDamage([CreateHitDataForLocation(PartLocation.LeftLeg, 5, [],[])], HitDirection.Front);

        // Verify damage was accumulated
        unit1.TotalPhaseDamage.ShouldBe(9);
        unit2.TotalPhaseDamage.ShouldBe(5);

        // Act - Change phase
        _sut.HandleCommand(new ChangePhaseCommand
        {
            GameOriginId = Guid.NewGuid(),
            Phase = PhaseNames.WeaponsAttack
        });

        // Assert - Both units should have TotalPhaseDamage reset to 0
        unit1.TotalPhaseDamage.ShouldBe(0);
        unit2.TotalPhaseDamage.ShouldBe(0);
        _sut.TurnPhase.ShouldBe(PhaseNames.WeaponsAttack);
    }

    [Fact]
    public void HandleCommand_ShouldProcessMechFallingCommand_WhenReceived()
    {
        // Arrange
        var playerId = Guid.NewGuid();
        var unitId = Guid.NewGuid();
        var player = new Player(playerId, "Player1", PlayerControlType.Human);
        var unitData = MechFactoryTests.CreateDummyMechData();
        unitData.Id = unitId;
        
        // Add player and unit to the game
        _sut.HandleCommand(new JoinGameCommand
        {
            PlayerId = player.Id,
            GameOriginId = Guid.NewGuid(),
            PlayerName = player.Name,
            Units = [unitData],
            Tint = "#FF0000",
            PilotAssignments = []
        });
        
        // Set up the map
        _sut.HandleCommand(new SetBattleMapCommand
        {
            GameOriginId = Guid.NewGuid(),
            MapData = []
        });
        
        // Deploy the unit
        _sut.HandleCommand(new DeployUnitCommand
        {
            GameOriginId = Guid.NewGuid(),
            PlayerId = playerId,
            UnitId = unitId,
            Position = new HexCoordinateData(1, 1),
            Direction = 0
        });
        
        // Create hit locations data for the falling damage
        var hitLocations = new List<LocationHitData>
        {
            CreateHitDataForLocation(
                PartLocation.CenterTorso, 
                5,
                [],
                [4])
        };
        
        var hitLocationsData = new HitLocationsData(hitLocations, 5);
        
        // Create the falling damage data
        var fallingDamageData = new FallingDamageData(
            HexDirection.Top,
            hitLocationsData,
            new DiceResult(4), HitDirection.Front);
        
        // Create the mech falling command
        var mechFallingCommand = new MechFallCommand
        {
            GameOriginId = Guid.NewGuid(),
            UnitId = unitId,
            LevelsFallen = 0,
            WasJumping = false,
            DamageData = fallingDamageData
        };
        var unit = _sut.Players.First().Units.First(u => u.Id == unitId);
        var initialArmor = unit.Parts[PartLocation.CenterTorso].CurrentArmor;
        
        // Act
        _sut.HandleCommand(mechFallingCommand);
        
        // Assert
        // Verify the command was added to the log
        _sut.CommandLog.ShouldContain(cmd => cmd is MechFallCommand);
        
        // Get the unit and verify it's prone
        unit.ShouldNotBeNull();
        unit.Status.ShouldHaveFlag(UnitStatus.Prone);
        
        // Verify damage was applied
        var currentArmor = unit.Parts[PartLocation.CenterTorso].CurrentArmor;
        currentArmor.ShouldBe(initialArmor - 5);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void HandleCommand_ShouldApplyPilotDamage_WhenMechFallsAndPilot(bool takesDamage)
    {
        // Arrange
        var playerId = Guid.NewGuid();
        var unitId = Guid.NewGuid();
        var player = new Player(playerId, "Player1", PlayerControlType.Human);
        var unitData = MechFactoryTests.CreateDummyMechData();
        unitData.Id = unitId;
        
        // Add player and unit to the game
        _sut.HandleCommand(new JoinGameCommand
        {
            PlayerId = player.Id,
            GameOriginId = Guid.NewGuid(),
            PlayerName = player.Name,
            Units = [unitData],
            Tint = "#FF0000",
            PilotAssignments = []
        });
        
        // Set up the map
        _sut.HandleCommand(new SetBattleMapCommand
        {
            GameOriginId = Guid.NewGuid(),
            MapData = []
        });
        
        // Deploy the unit
        _sut.HandleCommand(new DeployUnitCommand
        {
            GameOriginId = Guid.NewGuid(),
            PlayerId = playerId,
            UnitId = unitId,
            Position = new HexCoordinateData(1, 1),
            Direction = 0
        });
        
        // Create hit locations data for the falling damage
        var hitLocations = new List<LocationHitData>
        {
            CreateHitDataForLocation(
                PartLocation.CenterTorso, 
                5,
                [],
                [4])
        };
        
        var hitLocationsData = new HitLocationsData(hitLocations, 5);
        
        // Create the falling damage data
        var fallingDamageData = new FallingDamageData(
            HexDirection.Top,
            hitLocationsData,
            new DiceResult(4), HitDirection.Front);
        
        // Create the mech falling command
        var mechFallingCommand = new MechFallCommand
        {
            GameOriginId = Guid.NewGuid(),
            UnitId = unitId,
            LevelsFallen = 0,
            WasJumping = false,
            DamageData = fallingDamageData,
            PilotDamagePilotingSkillRoll = new PilotingSkillRollData
            {
                RollType = PilotingSkillRollType.GyroHit,
                DiceResults = [3,3],
                IsSuccessful = !takesDamage,
                PsrBreakdown = new PsrBreakdown
                {
                    BasePilotingSkill = 4,
                    Modifiers = []
                }
            }
        };
        var unit = _sut.Players.First().Units.First(u => u.Id == unitId);
        unit.AssignPilot(new MechWarrior("John", "Doe"));
        var initialInjuries = unit.Pilot!.Injuries;
        var expectedInjuries = takesDamage ? initialInjuries + 1 : initialInjuries;
        
        // Act
        _sut.HandleCommand(mechFallingCommand);
        
        // Assert
        unit.Pilot.Injuries.ShouldBe(expectedInjuries);
    }

    [Fact]
    public void HandleCommand_ShouldNotProcessMechFallingCommand_WhenUnitDoesNotExist()
    {
        // Arrange
        var nonExistentUnitId = Guid.NewGuid();
        
        // Create hit locations data for the falling damage
        var hitLocations = new List<LocationHitData>
        {
            CreateHitDataForLocation(
                PartLocation.CenterTorso, 
                5,
                [],
                [4])
        };
        
        var hitLocationsData = new HitLocationsData(hitLocations, 5);
        
        // Create the falling damage data
        var fallingDamageData = new FallingDamageData(
            HexDirection.Top,
            hitLocationsData,
            new DiceResult(4), HitDirection.Front);
        
        // Create the mech falling command
        var mechFallingCommand = new MechFallCommand
        {
            GameOriginId = Guid.NewGuid(),
            UnitId = nonExistentUnitId,
            LevelsFallen = 0,
            WasJumping = false,
            DamageData = fallingDamageData
        };
        
        // Act - this should not throw an exception
        var exception = Record.Exception(() => _sut.HandleCommand(mechFallingCommand));
        
        // Assert
        exception.ShouldBeNull();
        _sut.CommandLog.ShouldContain(cmd => cmd is MechFallCommand);
    }
    
    [Fact]
    public void HandleCommand_ShouldStandUpMech_WhenMechStandUpCommandIsReceived()
    {
        // Arrange
        // Add player and unit
        var playerId = Guid.NewGuid();
        var unitData = MechFactoryTests.CreateDummyMechData();
        unitData.Id = Guid.NewGuid();
        var joinCommand = new JoinGameCommand
        {
            PlayerId = playerId,
            PlayerName = "Player1",
            GameOriginId = Guid.NewGuid(),
            Units = [unitData],
            Tint = "#FF0000",
            PilotAssignments = []
        };
        _sut.HandleCommand(joinCommand);
        
        // Get the unit and check initial heat
        var unit = _sut.Players.First(p => p.Id == playerId).Units.First() as Mech;
        unit!.SetProne();
        unit.Status.ShouldHaveFlag(UnitStatus.Prone);
        
        // Create the standup command
        var heatUpdateCommand = new MechStandUpCommand
        {
            GameOriginId = Guid.NewGuid(),
            UnitId = unitData.Id.Value,
            Timestamp = DateTime.UtcNow,
            PilotingSkillRoll = new PilotingSkillRollData
            {
                RollType = PilotingSkillRollType.GyroHit,
                DiceResults = [3,4],
                IsSuccessful = true,
                PsrBreakdown = new PsrBreakdown
                {
                    BasePilotingSkill = 4,
                    Modifiers = []
                }
            }
        };
        
        // Act
        _sut.HandleCommand(heatUpdateCommand);
        
        // Assert
        unit.Status.ShouldNotHaveFlag(UnitStatus.Prone);
        unit.StandupAttempts.ShouldBe(1);
    }
    
    [Fact]
    public void HandleCommand_ShouldUpdatePilotConsciousness_WhenConsciousnessCommandIsReceived()
    {
        // Arrange
        var mechData = MechFactoryTests.CreateDummyMechData();
        var unitId = Guid.NewGuid();
        mechData.Id = unitId;
        var pilotId = Guid.NewGuid();
        
        var joinCommand = new JoinGameCommand
        {
            PlayerId = Guid.NewGuid(),
            PlayerName = "Player1",
            GameOriginId = Guid.NewGuid(),
            Units = [mechData],
            Tint = "#FF0000",
            PilotAssignments = [
                new PilotAssignmentData
                {
                    UnitId = unitId,
                    PilotData = new PilotData { Id = pilotId, IsConscious = true, Health = 6}
                }
            ]
        };
        
        _sut.OnPlayerJoined(joinCommand);
        var mech = _sut.Players.SelectMany(p => p.Units).First() as Mech;
        var pilot = mech?.Pilot;
        pilot!.Id.ShouldBe(pilotId);
        
        var command = new PilotConsciousnessRollCommand
        {
            GameOriginId = Guid.NewGuid(),
            PilotId = pilotId,
            UnitId = unitId,
            IsRecoveryAttempt = false,
            ConsciousnessNumber = 4,
            DiceResults = [1, 2],
            IsSuccessful = false // This should trigger KnockUnconscious
        };

        // Act
        _sut.HandleCommand(command);

        // Assert
        pilot.IsConscious.ShouldBeFalse();
    }
    
    [Fact]
    public void HandleCommand_ShouldProcessAmmoExplosionCommand_WhenReceived()
    {
        // Arrange
        var mechData = MechFactoryTests.CreateDummyMechData();
        var unitId = Guid.NewGuid();
        mechData.Id = unitId;
        var pilotId = Guid.NewGuid();
        
        var joinCommand = new JoinGameCommand
        {
            PlayerId = Guid.NewGuid(),
            PlayerName = "Player1",
            GameOriginId = Guid.NewGuid(),
            Units = [mechData],
            Tint = "#FF0000",
            PilotAssignments = [
                new PilotAssignmentData
                {
                    UnitId = unitId,
                    PilotData = new PilotData { Id = pilotId, IsConscious = true, Health = 6}
                }
            ]
        };
        
        _sut.HandleCommand(joinCommand);
        var mech = _sut.Players.SelectMany(p => p.Units).First() as Mech;
        var lrm5 = AmmoTests.CreateAmmo(Lrm5.Definition, 1);
        var ct = mech!.Parts[PartLocation.CenterTorso];
        ct.TryAddComponent(lrm5);
        var slot = lrm5.MountedAtFirstLocationSlots[0];

        var explosionCommand = new AmmoExplosionCommand
        {
            GameOriginId = Guid.NewGuid(),
            UnitId = mech.Id,
            AvoidExplosionRoll = new AvoidAmmoExplosionRollData
            {
                HeatLevel = 25,
                DiceResults = [2, 3],
                AvoidNumber = 6,
                IsSuccessful = false
            },
            CriticalHits =
            [
                new LocationCriticalHitsData(PartLocation.CenterTorso, [4, 4], 1,
                [
                    new ComponentHitData
                    {
                        Slot = slot,
                        Type = MakaMekComponent.ISAmmoLRM5,
                        ExplosionDamage = 5,
                        ExplosionDamageDistribution = [
                            new LocationDamageData(PartLocation.CenterTorso, 0, 5, false)
                        ]
                    }
                ],false)
            ]
        };

        // Act
        _sut.HandleCommand(explosionCommand);

        // Assert - Verify the unit took damage from the explosion
        var centerTorso = mech.Parts[PartLocation.CenterTorso];
        centerTorso.CurrentStructure.ShouldBe(centerTorso.MaxStructure - 5);
    }

    [Fact]
    public void HandleCommand_ShouldCallOnCriticalHitsResolution_WhenCriticalHitsResolutionCommandReceived()
    {
        // Arrange 
        var player = new Player(Guid.NewGuid(), "Player1", PlayerControlType.Human);
        var unitData = MechFactoryTests.CreateDummyMechData();
        unitData.Id = Guid.NewGuid();

        var joinCommand = new JoinGameCommand
        {
            PlayerId = player.Id,
            PlayerName = player.Name,
            GameOriginId = Guid.NewGuid(),
            Tint = player.Tint,
            Units = [unitData],
            PilotAssignments = []
        };

        _sut.HandleCommand(joinCommand);

        var criticalHitsCommand = new CriticalHitsResolutionCommand
        {
            GameOriginId = Guid.NewGuid(),
            TargetId = unitData.Id.Value,
            CriticalHits = [
                new LocationCriticalHitsData(
                    PartLocation.CenterTorso,
                    [4, 5],
                    1,
                    [new ComponentHitData { Type = MakaMekComponent.Engine, Slot = 1 }],
                    false)
            ]
        };

        var initialCommandLogCount = _sut.CommandLog.Count;

        // Act
        _sut.HandleCommand(criticalHitsCommand);

        // Assert
        _sut.CommandLog.Count.ShouldBe(initialCommandLogCount + 1);
        _sut.CommandLog.Last().ShouldBeEquivalentTo(criticalHitsCommand);

        // Verify the unit received the critical hits
        var unit = _sut.Players.SelectMany(p => p.Units).FirstOrDefault(u => u.Id == unitData.Id.Value);
        unit.ShouldNotBeNull();
        // The critical hits should have been applied to the unit
        var centerTorso = unit.Parts[PartLocation.CenterTorso];
        centerTorso.HitSlots.ShouldNotBeEmpty();
    }

    [Fact]
    public void HandleCommand_ShouldHandleInvalidTargetId_WhenCriticalHitsResolutionCommandReceived()
    {
        // Arrange - This tests lines 141-143 with invalid target ID
        var criticalHitsCommand = new CriticalHitsResolutionCommand
        {
            GameOriginId = Guid.NewGuid(),
            TargetId = Guid.NewGuid(), // Non-existent unit ID
            CriticalHits = [
                new LocationCriticalHitsData(
                    PartLocation.CenterTorso,
                    [4, 5],
                    1,
                    [new ComponentHitData { Type = MakaMekComponent.Engine, Slot = 1 }],
                    false)
            ]
        };

        var initialCommandLogCount = _sut.CommandLog.Count;

        // Act & Assert - Should not throw exception
        Should.NotThrow(() => _sut.HandleCommand(criticalHitsCommand));

        // Command should still be logged
        _sut.CommandLog.Count.ShouldBe(initialCommandLogCount + 1);
        _sut.CommandLog.Last().ShouldBeEquivalentTo(criticalHitsCommand);
    }

    [Fact]
    public void HandleCommand_ShouldProcessMultipleCriticalHitsLocations_WhenCriticalHitsResolutionCommandReceived()
    {
        // Arrange - This tests lines 141-143 with multiple critical hit locations
        var player = new Player(Guid.NewGuid(), "Player1", PlayerControlType.Human);
        var unitData = MechFactoryTests.CreateDummyMechData();
        unitData.Id = Guid.NewGuid();

        var joinCommand = new JoinGameCommand
        {
            PlayerId = player.Id,
            PlayerName = player.Name,
            GameOriginId = Guid.NewGuid(),
            Tint = player.Tint,
            Units = [unitData],
            PilotAssignments = []
        };

        _sut.HandleCommand(joinCommand);

        var criticalHitsCommand = new CriticalHitsResolutionCommand
        {
            GameOriginId = Guid.NewGuid(),
            TargetId = unitData.Id.Value,
            CriticalHits = [
                new LocationCriticalHitsData(
                    PartLocation.CenterTorso,
                    [4, 5],
                    1,
                    [new ComponentHitData { Type = MakaMekComponent.Engine, Slot = 1 }],
                    false),
                new LocationCriticalHitsData(
                    PartLocation.LeftArm,
                    [3, 4],
                    1,
                    [new ComponentHitData { Type = MakaMekComponent.MediumLaser, Slot = 2 }],
                    false)
            ]
        };

        var initialCommandLogCount = _sut.CommandLog.Count;

        // Act
        _sut.HandleCommand(criticalHitsCommand);

        // Assert
        _sut.CommandLog.Count.ShouldBe(initialCommandLogCount + 1);
        _sut.CommandLog.Last().ShouldBeEquivalentTo(criticalHitsCommand);

        // Verify both locations received critical hits
        var unit = _sut.Players.SelectMany(p => p.Units).FirstOrDefault(u => u.Id == unitData.Id.Value);
        unit.ShouldNotBeNull();

        var centerTorso = unit.Parts[PartLocation.CenterTorso];
        centerTorso.HitSlots.ShouldNotBeEmpty();

        var leftArm = unit.Parts[PartLocation.LeftArm];
        leftArm.HitSlots.ShouldNotBeEmpty();
    }
    
    [Fact]
    public void RequestLobbyStatus_ShouldPublishRequestLobbyStatusCommand_WhenCalled()
    {
        // Arrange
        _commandPublisher.ClearReceivedCalls();
        
        // Act
        _sut.RequestLobbyStatus(new RequestGameLobbyStatusCommand
        {
            GameOriginId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow
        });
        
        // Assert
        _commandPublisher.Received(1).PublishCommand(Arg.Any<RequestGameLobbyStatusCommand>());
    }
    
    [Fact]
    public void LeaveGame_ShouldNotPublishPlayerLeftCommand_WhenPlayerNotLocal()
    {
        // Arrange
        _commandPublisher.ClearReceivedCalls();
        
        // Act
        _sut.LeaveGame(Guid.NewGuid());
        
        // Assert
        _commandPublisher.DidNotReceive().PublishCommand(Arg.Any<PlayerLeftCommand>());
    }
    
    [Fact]
    public void LeaveGame_ShouldPublishPlayerLeftCommand_WhenPlayerLocal()
    {
        // Arrange
        _commandPublisher.ClearReceivedCalls();
        var playerId = Guid.NewGuid();
        _sut.JoinGameWithUnits(new Player(playerId, "Player1", PlayerControlType.Human), [],[]);
        _sut.HandleCommand(new JoinGameCommand
        {
            PlayerId = playerId,
            PlayerName = "Player1",
            GameOriginId = Guid.NewGuid(),
            Units = [],
            Tint = "#FF0000",
            PilotAssignments = [],
            IdempotencyKey = _idempotencyKey
        });
        
        // Act
        _sut.LeaveGame(playerId);
        
        // Assert
        _commandPublisher.Received(1).PublishCommand(Arg.Is<PlayerLeftCommand>(cmd => 
            cmd.PlayerId == playerId &&
            cmd.GameOriginId == _sut.Id
        ));
    }
    
    [Fact]
    public void Dispose_ShouldUnsubscribeFromCommandPublisher_WhenCalled()
    {
        // Arrange
        _commandPublisher.ClearReceivedCalls();
        
        // Act
        _sut.Dispose();
        
        // Assert
        _commandPublisher.Received(1).Unsubscribe(Arg.Any<Action<IGameCommand>>());
        _sut.IsDisposed.ShouldBeTrue();
    }
    
    [Fact]
    public void Dispose_ShouldUnsubscribeOnlyOnce_WhenCalledMultipleTimes()
    {
        // Arrange
        _commandPublisher.ClearReceivedCalls();

        // Act
        _sut.Dispose();
        _sut.Dispose(); // The second call should be no-op

        // Assert
        _commandPublisher.Received(1).Unsubscribe(Arg.Any<Action<IGameCommand>>());
        _sut.IsDisposed.ShouldBeTrue();
    }
    
    [Fact]
    public async Task SendPlayerAction_ShouldAssignIdempotencyKey_WhenSendingCommand()
    {
        // Arrange
        var player = new Player(Guid.NewGuid(), "Player1", PlayerControlType.Human);
        var unitData = MechFactoryTests.CreateDummyMechData();
        unitData.Id = Guid.NewGuid();

        await _sut.JoinGameWithUnits(player, [unitData],[]);
        var joinCommand = new JoinGameCommand
        {
            PlayerId = player.Id,
            PlayerName = player.Name,
            GameOriginId = Guid.NewGuid(),
            Tint = player.Tint,
            Units = [unitData],
            PilotAssignments = [],
            IdempotencyKey = _idempotencyKey
        };
        _sut.HandleCommand(joinCommand);

        _sut.HandleCommand(new ChangeActivePlayerCommand
        {
            GameOriginId = Guid.NewGuid(),
            PlayerId = player.Id,
            UnitsToPlay = 1
        });

        var deployCommand = new DeployUnitCommand
        {
            GameOriginId = _sut.Id,
            PlayerId = player.Id,
            UnitId = unitData.Id!.Value,
            Position = new HexCoordinateData(0, 0),
            Direction = 0
        };

        _commandPublisher.ClearReceivedCalls();

        // Act
        var deployTask = _sut.DeployUnit(deployCommand);

        // Wait for the command to be published (with retry to avoid race condition)
        DeployUnitCommand? capturedCommand = await WaitForPublishedCommand<DeployUnitCommand>(_commandPublisher);

        capturedCommand.ShouldNotBeNull("Command should have been published");
        capturedCommand.Value.IdempotencyKey.ShouldNotBeNull();

        // Simulate server rebroadcast - change GameOriginId to simulate server rebroadcast
        var rebroadcastCommand = capturedCommand.Value with { GameOriginId = Guid.NewGuid() };
        _sut.HandleCommand(rebroadcastCommand);

        // Assert - wait for the task to complete with a timeout
        var completedTask = await Task.WhenAny(deployTask, Task.Delay(1000));
        completedTask.ShouldBe(deployTask, "Task should complete when server rebroadcasts command");

        var result = await deployTask;
        result.ShouldBeTrue();
    }

    [Fact]
    public async Task SendPlayerAction_ShouldCompletePendingTask_WhenErrorCommandReceived()
    {
        // Arrange
        var player = new Player(Guid.NewGuid(), "Player1", PlayerControlType.Human);
        var unitData = MechFactoryTests.CreateDummyMechData();
        unitData.Id = Guid.NewGuid();

        _sut.JoinGameWithUnits(player, [unitData],[]).SafeFireAndForget();
        
        var joinCommand = new JoinGameCommand
        {
            PlayerId = player.Id,
            PlayerName = player.Name,
            GameOriginId = Guid.NewGuid(),
            Tint = player.Tint,
            Units = [unitData],
            PilotAssignments = [],
            IdempotencyKey = _idempotencyKey
        };

        _sut.HandleCommand(joinCommand);

        _sut.HandleCommand(new ChangeActivePlayerCommand
        {
            GameOriginId = Guid.NewGuid(),
            PlayerId = player.Id,
            UnitsToPlay = 1
        });

        var deployCommand = new DeployUnitCommand
        {
            GameOriginId = _sut.Id,
            PlayerId = player.Id,
            UnitId = unitData.Id!.Value,
            Position = new HexCoordinateData(0, 0),
            Direction = 0
        };

        _commandPublisher.ClearReceivedCalls();

        // Act
        var deployTask = _sut.DeployUnit(deployCommand);

        // Wait for the command to be published (with retry to avoid race condition)
        var capturedCommand = await WaitForPublishedCommand<DeployUnitCommand>(_commandPublisher);

        capturedCommand.ShouldNotBeNull("Command should have been published");

        // Simulate server error response - this should complete the task
        _sut.HandleCommand(new ErrorCommand
        {
            GameOriginId = Guid.NewGuid(),
            IdempotencyKey = capturedCommand.Value.IdempotencyKey!.Value,
            ErrorCode = ErrorCode.DuplicateCommand
        });

        // Assert - wait for the task to complete with a timeout
        var completedTask = await Task.WhenAny(deployTask, Task.Delay(1000));
        completedTask.ShouldBe(deployTask, "Task should complete when ErrorCommand is received");

        var result = await deployTask;
        result.ShouldBeFalse();
    }

    [Fact]
    public async Task SendPlayerAction_ShouldCompletePendingTask_WhenGameDisposed()
    {
        // Arrange
        var player = new Player(Guid.NewGuid(), "Player1", PlayerControlType.Human);
        var unitData = MechFactoryTests.CreateDummyMechData();
        unitData.Id = Guid.NewGuid();
        
        var task = _sut.JoinGameWithUnits(player, [unitData],[]);

        // Act
        _sut.Dispose();
        
        // Assert, a task should be canceled
        var totalDelay = 0;
        const int stepDelay = 20;
        while (!task.IsCanceled)
        {
            await Task.Delay(stepDelay);
            totalDelay+= stepDelay;
            if (totalDelay > 500) throw new TimeoutException("Task did not complete in time");
        }
        
        task.IsCanceled.ShouldBeTrue();
    }
    
    [Fact]
    public async Task SendPlayerAction_ShouldCompletePendingTask_WhenServerDoesNotAcknowledge()
    {
        // Arrange
        var player = new Player(Guid.NewGuid(), "Player1", PlayerControlType.Human);
        var unitData = MechFactoryTests.CreateDummyMechData();
        unitData.Id = Guid.NewGuid();

        var task = _sut.JoinGameWithUnits(player, [unitData],[]);
        
        var result = await task;
        result.ShouldBeFalse();
    }
    
    [Fact]
    public void TryStandup_ShouldSendTryStandupCommand_WhenCalled()
    {
        // Arrange
        var player = new Player(Guid.NewGuid(), "Player1", PlayerControlType.Human);
        var unitData = MechFactoryTests.CreateDummyMechData();
        unitData.Id = Guid.NewGuid();

        _sut.JoinGameWithUnits(player, [unitData],[]).SafeFireAndForget();
        var joinCommand = new JoinGameCommand
        {
            PlayerId = player.Id,
            PlayerName = player.Name,
            GameOriginId = Guid.NewGuid(),
            Tint = player.Tint,
            Units = [unitData],
            PilotAssignments = [],
            IdempotencyKey = _idempotencyKey
        };
        _sut.HandleCommand(joinCommand);

        _sut.HandleCommand(new ChangeActivePlayerCommand
        {
            GameOriginId = Guid.NewGuid(),
            PlayerId = player.Id,
            UnitsToPlay = 1
        });

        var standupCommand = new TryStandupCommand
        {
            GameOriginId = _sut.Id,
            PlayerId = player.Id,
            UnitId = unitData.Id!.Value,
            NewFacing = HexDirection.Bottom,
            MovementTypeAfterStandup = MovementType.Walk
        };

        _commandPublisher.ClearReceivedCalls();

        // Act
        _sut.TryStandupUnit(standupCommand);

        // Assert
        _commandPublisher.Received(1).PublishCommand(Arg.Is<TryStandupCommand>(cmd => 
            cmd.PlayerId == player.Id &&
            cmd.UnitId == unitData.Id!.Value &&
            cmd.NewFacing == HexDirection.Bottom &&
            cmd.MovementTypeAfterStandup == MovementType.Walk));
    }
    [Theory]
    [InlineData(PlayerControlType.Human)]
    [InlineData(PlayerControlType.Bot)]
    public void HandleCommand_ShouldSetCorrectControlType_WhenLocalPlayerJoins(PlayerControlType controlType)
    {
        // Arrange
        var player = new Player(Guid.NewGuid(), "Player1", controlType);
        var unitData = MechFactoryTests.CreateDummyMechData();
        unitData.Id = Guid.NewGuid();
        
        // Join the game locally first to register a control type
        _sut.JoinGameWithUnits(player, [unitData], []);

        var joinCommand = new JoinGameCommand
        {
            PlayerId = player.Id,
            GameOriginId = Guid.NewGuid(),
            PlayerName = player.Name,
            Units = [unitData],
            Tint = "#FF0000",
            PilotAssignments = []
        };

        // Act
        _sut.HandleCommand(joinCommand);

        // Assert
        _sut.LocalPlayers.ShouldContain(player.Id);
        _sut.LocalPlayers.Count.ShouldBe(1);
        var joinedPlayer = _sut.Players.FirstOrDefault(p => p.Id == player.Id);
        joinedPlayer.ShouldNotBeNull();
        joinedPlayer.ControlType.ShouldBe(controlType);
    }

    [Fact]
    public void OnTurnIncremented_ShouldResetUnitsState()
    {
        // Arrange
        var player = new Player(Guid.NewGuid(), "Player1", PlayerControlType.Human);
        var unitData = MechFactoryTests.CreateDummyMechData();
        unitData.Id = Guid.NewGuid();

        _sut.HandleCommand(new JoinGameCommand
        {
            PlayerId = player.Id,
            GameOriginId = Guid.NewGuid(),
            PlayerName = player.Name,
            Units = [unitData],
            Tint = "#FF0000",
            PilotAssignments = []
        });

        // Deploy the unit
        var deployCommand = new DeployUnitCommand
        {
            GameOriginId = Guid.NewGuid(),
            PlayerId = player.Id,
            Position = new HexCoordinateData(1, 1),
            Direction = 0,
            UnitId = unitData.Id.Value
        };
        _sut.HandleCommand(deployCommand);

        // Move the unit to simulate state change
        // We use a path that starts at the current position (1,1) and moves to (2,2)
        var startPos = new HexPosition(1, 1, HexDirection.Top);
        var endPos = new HexPosition(2, 2, HexDirection.Top);
        var movementPath = new MovementPath([
            new PathSegment(startPos, endPos, 1)
        ], MovementType.Walk);
        
        var moveCommand = new MoveUnitCommand
        {
            GameOriginId = Guid.NewGuid(),
            PlayerId = player.Id,
            MovementType = MovementType.Walk,
            UnitId = unitData.Id.Value,
            MovementPath = movementPath.ToData()
        };
        _sut.HandleCommand(moveCommand);

        var unit = _sut.Players.First().Units.First();
        
        // Assert initial state - MovementTaken should be set
        unit.MovementTaken.ShouldNotBeNull();
        unit.MovementTaken.Start.ShouldBe(startPos);
        unit.MovementTaken.Destination.ShouldBe(endPos);

        // Act - Increment Turn
        var turnIncrementedCommand = new TurnIncrementedCommand
        {
            GameOriginId = Guid.NewGuid(),
            TurnNumber = _sut.Turn + 1
        };
        _sut.HandleCommand(turnIncrementedCommand);

        // Assert - State should be reset
        unit.MovementTaken.ShouldBeNull();
        _sut.Turn.ShouldBe(turnIncrementedCommand.TurnNumber);
    }
}
