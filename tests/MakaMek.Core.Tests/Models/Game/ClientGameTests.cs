using Shouldly;
using NSubstitute;
using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Data.Map;
using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Models.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Mechs.Falling;
using Sanet.MakaMek.Core.Models.Game.Phases;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Map.Factory;
using Sanet.MakaMek.Core.Models.Map.Terrains;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Core.Models.Units.Pilots;
using Sanet.MakaMek.Core.Services.Localization;
using Sanet.MakaMek.Core.Services.Transport;
using Sanet.MakaMek.Core.Tests.Data.Community;
using Sanet.MakaMek.Core.Tests.Models.Map;
using Sanet.MakaMek.Core.Utils;
using Sanet.MakaMek.Core.Utils.Generators;
using Sanet.MakaMek.Core.Utils.TechRules;
using Shouldly.ShouldlyExtensionMethods;

namespace Sanet.MakaMek.Core.Tests.Models.Game;

public class ClientGameTests
{
    private readonly ClientGame _sut;
    private readonly ICommandPublisher _commandPublisher;
    private readonly IBattleMapFactory _mapFactory = Substitute.For<IBattleMapFactory>();
    public ClientGameTests()
    {
        var battleMap = BattleMapTests.BattleMapFactory.GenerateMap(5, 5, new SingleTerrainGenerator(5,5, new ClearTerrain()));
        _commandPublisher = Substitute.For<ICommandPublisher>();
        var rulesProvider = new ClassicBattletechRulesProvider();
        var mechFactory = new MechFactory(rulesProvider,Substitute.For<ILocalizationService>());
        _mapFactory.CreateFromData(Arg.Any<IList<HexData>>()).Returns(battleMap); 
        _sut = new ClientGame(
            rulesProvider,
            mechFactory,
            _commandPublisher,
            Substitute.For<IToHitCalculator>(),
            Substitute.For<IPilotingSkillCalculator>(),
            Substitute.For<IConsciousnessCalculator>(),
            Substitute.For<IHeatEffectsCalculator>(),
            _mapFactory);
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
        var player = new Player(Guid.NewGuid(), "Player1");

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
        var player = new Player(playerId, "Player1");
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
        var player = new Player(Guid.NewGuid(), "Player1");
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
        var player = new Player(Guid.NewGuid(), "Player1");
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
        var player = new Player(Guid.NewGuid(), "Player1");
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
        _sut.ActivePlayer.ShouldBe(actualPlayer);
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
        var player = new Player(Guid.NewGuid(), "Player1");
        var unitData = MechFactoryTests.CreateDummyMechData();
        unitData.Id= Guid.NewGuid();
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
            PilotAssignments = []
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
        var player = new Player(Guid.NewGuid(), "Player1");
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
            PilotAssignments = []
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
        var player = new Player(Guid.NewGuid(), "Player1");
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
        var player = new Player(Guid.NewGuid(), "Player1");
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
        var player = new Player(Guid.NewGuid(), "Player1");
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
        var player = new Player(Guid.NewGuid(), "Player1");
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
        var player = new Player(Guid.NewGuid(), "Player1");
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
            PilotAssignments = []
        });

        _sut.HandleCommand(new ChangeActivePlayerCommand
        {
            GameOriginId = Guid.NewGuid(),
            PlayerId = player.Id,
            UnitsToPlay = 1
        });

        var command = new WeaponConfigurationCommand
        {
            GameOriginId = Guid.NewGuid(),
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
            _commandPublisher.Received(1).PublishCommand(command);
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
        var player = new Player(Guid.NewGuid(), "Player1");
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
        var player = new Player(Guid.NewGuid(), "Player1");
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
            PilotAssignments = []
        });

        _sut.HandleCommand(new ChangeActivePlayerCommand
        {
            GameOriginId = Guid.NewGuid(),
            PlayerId = player.Id,
            UnitsToPlay = 1
        });

        var targetPlayer = new Player(Guid.NewGuid(), "Player2");
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
            AttackerId = unitData.Id.Value,
            WeaponTargets =
            [
                new WeaponTargetData()
                {
                    Weapon = new WeaponData
                    {
                        Name = "Medium Laser",
                        Location = PartLocation.RightArm,
                        Slots = [1, 2]
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
            _commandPublisher.Received(1).PublishCommand(command);
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
            AttackerId = Guid.NewGuid(),
            WeaponTargets =
            [
                new WeaponTargetData
                {
                    Weapon = new WeaponData
                    {
                        Name = "Medium Laser",
                        Location = PartLocation.RightArm,
                        Slots = [1, 2]
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
        var attackerPlayer = new Player(Guid.NewGuid(), "Attacker");
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
        var targetPlayer = new Player(Guid.NewGuid(), "Target");
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
            AttackerId = attackerUnitData.Id.Value,
            WeaponTargets =
            [
                new WeaponTargetData
                {
                    Weapon = new WeaponData
                    {
                        Name = "Medium Laser",
                        Location = PartLocation.RightArm,
                        Slots = [1, 2]
                    },
                    TargetId = targetUnitData.Id.Value,
                    IsPrimaryTarget = true
                }
            ]
        };

        // Act
        _sut.HandleCommand(weaponAttackCommand);

        // Assert
        var attackerUnit = _sut.Players.First(p => p.Id == attackerPlayer.Id).Units.First();
        
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
        var hitLocations = new List<HitLocationData>
        {
            new(PartLocation.CenterTorso, 5, [],[]),
            new(PartLocation.LeftArm, 3, [],[])
        };
        
        // Create the attack resolution command
        var attackResolutionCommand = new WeaponAttackResolutionCommand
        {
            GameOriginId = Guid.NewGuid(),
            PlayerId = Guid.NewGuid(),
            AttackerId = targetMech.Id,
            TargetId = targetMech.Id,
            WeaponData = new WeaponData
            {
                Name = "Test Weapon",
                Location = PartLocation.RightArm,
                Slots = [0, 1]
            },
            ResolutionData = new AttackResolutionData(
                10,
                [],
                true,
                null,
                new AttackHitLocationsData(hitLocations, 8, [], 0))
        };

        // Get initial armor values for verification
        var centerTorsoPart = targetMech.Parts.First(p => p.Location == PartLocation.CenterTorso);
        var leftArmPart = targetMech.Parts.First(p => p.Location == PartLocation.LeftArm);
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
        var player = new Player(Guid.NewGuid(), "Player1");
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
            PilotAssignments = []
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
        var player = new Player(Guid.NewGuid(), "Player1");
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

        if (isLocalPlayer)
        {
            _sut.LocalPlayers.Add(player.Id);
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

    [Fact]
    public void HandleCommand_ShouldClearPlayersEndedTurnAndSetFirstLocalPlayerAsActive_WhenEnteringEndPhase()
    {
        // Arrange
        var localPlayer1 = new Player(Guid.NewGuid(), "LocalPlayer1");
        var localPlayer2 = new Player(Guid.NewGuid(), "LocalPlayer2");
        
        // Create a new client game with local players
        var battleMap = BattleMapTests.BattleMapFactory.GenerateMap(5, 5, new SingleTerrainGenerator(5,5, new ClearTerrain()));
        var commandPublisher = Substitute.For<ICommandPublisher>();
        var rulesProvider = new ClassicBattletechRulesProvider();
        var clientGame = new ClientGame(
            rulesProvider, 
            new MechFactory(rulesProvider,Substitute.For<ILocalizationService>()),
            commandPublisher,
            Substitute.For<IToHitCalculator>(),
            Substitute.For<IPilotingSkillCalculator>(),
            Substitute.For<IConsciousnessCalculator>(),
            Substitute.For<IHeatEffectsCalculator>(),
            _mapFactory);
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
        
        // Act - Change to End phase
        clientGame.HandleCommand(new ChangePhaseCommand
        {
            GameOriginId = Guid.NewGuid(),
            Phase = PhaseNames.End
        });
        
        // Assert
        clientGame.TurnPhase.ShouldBe(PhaseNames.End);
        clientGame.ActivePlayer.ShouldNotBeNull();
        clientGame.ActivePlayer!.Id.ShouldBe(localPlayer1.Id); // The first local player should be active
        
        // Verify that the player can end turn again (previous end turn state was cleared)
        clientGame.HandleCommand(new TurnEndedCommand
        {
            GameOriginId = Guid.NewGuid(),
            PlayerId = localPlayer1.Id,
            Timestamp = DateTime.UtcNow
        });
        
        // After the first local player ends turn, the second local player should become active
        clientGame.ActivePlayer.ShouldNotBeNull();
        clientGame.ActivePlayer!.Id.ShouldBe(localPlayer2.Id);
    }
    
    [Fact]
    public void HandleCommand_ShouldUpdateActivePlayer_WhenTurnEndedCommandIsReceivedInEndPhase()
    {
        // Arrange
        var localPlayer1 = new Player(Guid.NewGuid(), "LocalPlayer1");
        var localPlayer2 = new Player(Guid.NewGuid(), "LocalPlayer2");
        var localPlayer3 = new Player(Guid.NewGuid(), "LocalPlayer3");
        
        // Create a new client game with local players
        var battleMap = BattleMapTests.BattleMapFactory.GenerateMap(5, 5, new SingleTerrainGenerator(5,5, new ClearTerrain()));
        var commandPublisher = Substitute.For<ICommandPublisher>();
        var rulesProvider = new ClassicBattletechRulesProvider();
        var clientGame = new ClientGame(
            rulesProvider, 
            Substitute.For<IMechFactory>(),
            commandPublisher,
            Substitute.For<IToHitCalculator>(),
            Substitute.For<IPilotingSkillCalculator>(),
            Substitute.For<IConsciousnessCalculator>(),
            Substitute.For<IHeatEffectsCalculator>(),
            _mapFactory);
        clientGame.JoinGameWithUnits(localPlayer1,[],[]);
        clientGame.JoinGameWithUnits(localPlayer2,[],[]);
        clientGame.JoinGameWithUnits(localPlayer3,[],[]);
        clientGame.SetBattleMap(battleMap);
        
        // Add the local players to the game
        clientGame.HandleCommand(new JoinGameCommand
        {
            PlayerId = localPlayer1.Id,
            GameOriginId = Guid.NewGuid(),
            PlayerName = localPlayer1.Name,
            Units = [],
            Tint = "#FF0000",
            PilotAssignments = []
        });
        
        clientGame.HandleCommand(new JoinGameCommand
        {
            PlayerId = localPlayer2.Id,
            GameOriginId = Guid.NewGuid(),
            PlayerName = localPlayer2.Name,
            Units = [],
            Tint = "#00FF00",
            PilotAssignments = []
        });
        
        clientGame.HandleCommand(new JoinGameCommand
        {
            PlayerId = localPlayer3.Id,
            GameOriginId = Guid.NewGuid(),
            PlayerName = localPlayer3.Name,
            Units = [],
            Tint = "#0000FF",
            PilotAssignments = []
        });
        
        // Set the game to End phase
        clientGame.HandleCommand(new ChangePhaseCommand
        {
            GameOriginId = Guid.NewGuid(),
            Phase = PhaseNames.End
        });
        
        // Set the first local player as active
        clientGame.HandleCommand(new ChangeActivePlayerCommand
        {
            GameOriginId = Guid.NewGuid(),
            PlayerId = localPlayer1.Id,
            UnitsToPlay = 0
        });
        
        // Verify initial state
        clientGame.ActivePlayer.ShouldNotBeNull();
        clientGame.ActivePlayer!.Id.ShouldBe(localPlayer1.Id);
        
        // Act - End turn for the first player
        clientGame.HandleCommand(new TurnEndedCommand
        {
            GameOriginId = Guid.NewGuid(),
            PlayerId = localPlayer1.Id,
            Timestamp = DateTime.UtcNow
        });
        
        // Assert - Second player should now be active
        clientGame.ActivePlayer.ShouldNotBeNull();
        clientGame.ActivePlayer!.Id.ShouldBe(localPlayer2.Id);
        
        // Act - End turn for the second player
        clientGame.HandleCommand(new TurnEndedCommand
        {
            GameOriginId = Guid.NewGuid(),
            PlayerId = localPlayer2.Id,
            Timestamp = DateTime.UtcNow
        });
        
        // Assert - Third player should now be active
        clientGame.ActivePlayer.ShouldNotBeNull();
        clientGame.ActivePlayer!.Id.ShouldBe(localPlayer3.Id);
        
        // Act - End turn for the third player
        clientGame.HandleCommand(new TurnEndedCommand
        {
            GameOriginId = Guid.NewGuid(),
            PlayerId = localPlayer3.Id,
            Timestamp = DateTime.UtcNow
        });
        
        // Assert - No more local players who haven't ended their turn, so ActivePlayer should be null
        clientGame.ActivePlayer.ShouldBeNull();
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
        unit1.ApplyDamage([new HitLocationData(PartLocation.CenterTorso, 9, [],[])]);
        unit2.ApplyDamage([new HitLocationData(PartLocation.LeftLeg, 5, [],[])]);

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
        var player = new Player(playerId, "Player1");
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
        var hitLocations = new List<HitLocationData>
        {
            new(
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
            new DiceResult(4));
        
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
        var initialArmor = unit.Parts.First(p => p.Location == PartLocation.CenterTorso).CurrentArmor;
        
        // Act
        _sut.HandleCommand(mechFallingCommand);
        
        // Assert
        // Verify the command was added to the log
        _sut.CommandLog.ShouldContain(cmd => cmd is MechFallCommand);
        
        // Get the unit and verify it's prone
        unit.ShouldNotBeNull();
        unit.Status.ShouldHaveFlag(UnitStatus.Prone);
        
        // Verify damage was applied
        var currentArmor = unit.Parts.First(p => p.Location == PartLocation.CenterTorso).CurrentArmor;
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
        var player = new Player(playerId, "Player1");
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
        var hitLocations = new List<HitLocationData>
        {
            new(
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
            new DiceResult(4));
        
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
        var hitLocations = new List<HitLocationData>
        {
            new(
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
            new DiceResult(4));
        
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
}