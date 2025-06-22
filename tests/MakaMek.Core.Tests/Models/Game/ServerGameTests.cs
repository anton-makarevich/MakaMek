using Shouldly;
using NSubstitute;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Data.Map;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Models.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Mechs.Falling;
using Sanet.MakaMek.Core.Models.Game.Phases;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Map.Terrains;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Services.Localization;
using Sanet.MakaMek.Core.Services.Transport;
using Sanet.MakaMek.Core.Tests.Data.Community;
using Sanet.MakaMek.Core.Tests.Models.Map;
using Sanet.MakaMek.Core.Utils;
using Sanet.MakaMek.Core.Utils.Generators;
using Sanet.MakaMek.Core.Utils.TechRules;

namespace Sanet.MakaMek.Core.Tests.Models.Game;

public class ServerGameTests
{
    private readonly ServerGame _sut;
    private readonly ICommandPublisher _commandPublisher;
    public ServerGameTests()
    {
        var battleMap = BattleMapTests.BattleMapFactory.GenerateMap(5, 5,
            new SingleTerrainGenerator(5,5, new ClearTerrain()));
        
        _commandPublisher = Substitute.For<ICommandPublisher>();
        var diceRoller = Substitute.For<IDiceRoller>();
        var rulesProvider = Substitute.For<IRulesProvider>();
        rulesProvider.GetStructureValues(20).Returns(new Dictionary<PartLocation, int>
        {
            { PartLocation.Head, 8 },
            { PartLocation.CenterTorso, 10 },
            { PartLocation.LeftTorso, 8 },
            { PartLocation.RightTorso, 8 },
            { PartLocation.LeftArm, 4 },
            { PartLocation.RightArm, 4 },
            { PartLocation.LeftLeg, 8 },
            { PartLocation.RightLeg, 8 }
        });
        _sut = new ServerGame(rulesProvider,
            new MechFactory(rulesProvider, Substitute.For<ILocalizationService>()),
            _commandPublisher, diceRoller,
            Substitute.For<IToHitCalculator>(),
            Substitute.For<ICriticalHitsCalculator>(),
            Substitute.For<IPilotingSkillCalculator>(),
            Substitute.For<IFallProcessor>());
        _sut.SetBattleMap(battleMap);
    }

    [Fact]
    public void IncrementTurn_ShouldPublishTurnIncrementedCommand_WhenCalled()
    {
        // Arrange
        var initialTurn = _sut.Turn;

        // Act
        _sut.IncrementTurn();

        // Assert
        _sut.Turn.ShouldBe(initialTurn + 1);
        _commandPublisher.Received(1).PublishCommand(Arg.Is<TurnIncrementedCommand>(cmd => 
            cmd.TurnNumber == initialTurn + 1 &&
            cmd.GameOriginId == _sut.Id
        ));
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
        _sut.HandleCommand(joinCommand);

        // Assert
        _sut.Players.Count.ShouldBe(1);
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
            Tint = "#FF0000"
        };

        // Act
        _sut.HandleCommand(command);

        // Assert
        // Verify that no players were added since the command was from this game instance
        _sut.Players.ShouldBeEmpty();
    }
    
    [Fact]
    public void HandleCommand_ShouldProcessPlayerStatusCommand_WhenReceived()
    {
        // Arrange
        var playerId = Guid.NewGuid();
        _sut.HandleCommand(new JoinGameCommand
        {
            PlayerId = playerId,
            GameOriginId = Guid.NewGuid(),
            PlayerName = "Player1",
            Units=[],
            Tint = "#FF0000"
        });

        var statusCommand = new UpdatePlayerStatusCommand
        {
            PlayerId = playerId,
            GameOriginId = Guid.NewGuid(),
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
    public void UpdatePhase_ShouldPublishPhaseChangedEvent_WhenCalled()
    {
        // Arrange
        var playerId = Guid.NewGuid();
        _sut.HandleCommand(new JoinGameCommand
        {
            PlayerId = playerId,
            GameOriginId = Guid.NewGuid(),
            PlayerName = "Player1",
            Units=[],
            Tint = "#FF0000"
        });
        _sut.HandleCommand(new UpdatePlayerStatusCommand
        {
            PlayerId = playerId,
            GameOriginId = Guid.NewGuid(),
            PlayerStatus = PlayerStatus.Ready
        });

        // Assert
        _sut.TurnPhase.ShouldBe(PhaseNames.Deployment);
        _commandPublisher.Received(1).PublishCommand(Arg.Is<ChangePhaseCommand>(cmd => 
            cmd.Phase == PhaseNames.Deployment &&
            cmd.GameOriginId == _sut.Id
        ));
    }
    
    [Fact]
    public void StartDeploymentPhase_ShouldRandomizeOrderAndSetActivePlayer_WhenAllPlayersReady()
    {
        // Arrange
        var playerId1 = Guid.NewGuid();
        var playerId2 = Guid.NewGuid();
    
        _sut.HandleCommand(new JoinGameCommand
        {
            PlayerId = playerId1,
            PlayerName = "Player1",
            GameOriginId = Guid.NewGuid(),
            Units = [],
            Tint = "#FF0000"
        });

        _sut.HandleCommand(new JoinGameCommand
        {
            PlayerId = playerId2,
            PlayerName = "Player2",
            GameOriginId = Guid.NewGuid(),
            Units = [],
            Tint = "#FF0000"
        });

        _sut.HandleCommand(new UpdatePlayerStatusCommand
        {
            PlayerId = playerId1,
            GameOriginId = Guid.NewGuid(),
            PlayerStatus = PlayerStatus.Ready
        });

        _sut.HandleCommand(new UpdatePlayerStatusCommand
        {
            PlayerId = playerId2,
            GameOriginId = Guid.NewGuid(),
            PlayerStatus = PlayerStatus.Ready
        });
        
        // Assert
        _sut.ActivePlayer.ShouldNotBeNull();
        var expectedIds = new List<Guid> { playerId1, playerId2 };
        expectedIds.ShouldContain(_sut.ActivePlayer.Id);
    }
    
    [Fact]
    public void DeployUnit_ShouldDeployUnitAndSetNextPhase_WhenCalled()
    {
        // Arrange
        _sut.IsAutoRoll = false;
        var playerId = Guid.NewGuid();
        var unitId = Guid.NewGuid();
        var unitData = MechFactoryIntegrationTests.LoadMechFromMtfFile("Resources/Mechs/LCT-1V.mtf");
        unitData.Id = unitId;
    
        _sut.HandleCommand(new JoinGameCommand
        {
            PlayerId = playerId,
            PlayerName = "Player1",
            GameOriginId = Guid.NewGuid(),
            Units = [unitData],
            Tint = "#FF0000"
        });
    
        _sut.HandleCommand(new UpdatePlayerStatusCommand
        {
            PlayerId = playerId,
            GameOriginId = Guid.NewGuid(),
            PlayerStatus = PlayerStatus.Ready
        });
    
        // Act
        _sut.HandleCommand(new DeployUnitCommand
        {
            PlayerId = playerId,
            UnitId = unitId,
            GameOriginId = Guid.NewGuid(),
            Position = new HexCoordinateData(2, 3) ,
            Direction = 0
        });
    
        // Assert
        _sut.Players.All(p=>p.Units.All(u=>u.IsDeployed)).ShouldBeTrue();
        _sut.TurnPhase.ShouldBe(PhaseNames.Initiative);
    }
    
    [Fact]
    public void IncrementTurn_ShouldIncrementTurn_WhenCalled()
    {
        // Arrange
        // Act
        _sut.IncrementTurn();
    
        // Assert
        _sut.Turn.ShouldBe(2);
    }

    [Fact]
    public void HandleCommand_ShouldProcessRequestGameLobbyStatusCommand_WhenReceived()
    {
        // Arrange
        var mockPhase = Substitute.For<IGamePhase>();
        mockPhase.Name.Returns(PhaseNames.Start);
        
        var phaseManager = Substitute.For<IPhaseManager>();
        
        var commandPublisher = Substitute.For<ICommandPublisher>();
        var diceRoller = Substitute.For<IDiceRoller>();
        var rulesProvider = new ClassicBattletechRulesProvider();
        
        // Set up the phase manager to return our mock phase
                phaseManager.GetNextPhase(PhaseNames.Start, Arg.Any<ServerGame>()).Returns(mockPhase);
        
        // Create the game with a mocked phase manager
        var sut = new ServerGame(
            rulesProvider,
            Substitute.For<IMechFactory>(),
            commandPublisher, 
            diceRoller,
            Substitute.For<IToHitCalculator>(),
            Substitute.For<ICriticalHitsCalculator>(),
            Substitute.For<IPilotingSkillCalculator>(),
            Substitute.For<IFallProcessor>(),
            phaseManager);
        
        sut.TransitionToNextPhase(PhaseNames.Start);

        // Create the command to test
        var requestCommand = new RequestGameLobbyStatusCommand
        {
            GameOriginId = Guid.NewGuid(), // Different from the game's ID
            Timestamp = DateTime.UtcNow
        };

        // Act
        sut.HandleCommand(requestCommand);

        // Assert
        // Verify that the command was passed to the current phase
        mockPhase.Received(1).HandleCommand(Arg.Is<RequestGameLobbyStatusCommand>(cmd => 
            cmd.GameOriginId == requestCommand.GameOriginId));
    }

    [Fact]
    public void SetBattleMap_ShouldPublishSetBattleMapCommand_WhenCalled()
    {
        // Arrange
        var battleMap = BattleMapTests.BattleMapFactory.GenerateMap(5, 5,
            new SingleTerrainGenerator(5, 5, new ClearTerrain()));
        _commandPublisher.ClearReceivedCalls();
        
        // Act
        _sut.SetBattleMap(battleMap);
        
        // Assert
        _commandPublisher.Received(1).PublishCommand(Arg.Is<SetBattleMapCommand>(cmd => 
            cmd.GameOriginId == _sut.Id && 
            cmd.MapData.Count == battleMap.ToData().Count));
    }
}