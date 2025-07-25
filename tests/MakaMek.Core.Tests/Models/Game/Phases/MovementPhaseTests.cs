using NSubstitute;
using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Mechs.Falling;
using Sanet.MakaMek.Core.Models.Game.Phases;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components.Internal;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Core.Models.Units.Pilots;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Phases;

public class MovementPhaseTests : GamePhaseTestsBase
{
    private readonly MovementPhase _sut;
    private readonly Guid _player1Id = Guid.NewGuid();
    private readonly Guid _player2Id = Guid.NewGuid();
    private readonly Guid _unit1Id;
    private readonly IGamePhase _mockNextPhase;

    public MovementPhaseTests()
    {
        // Create mock next phase and configure the phase manager
        _mockNextPhase = Substitute.For<IGamePhase>();
        MockPhaseManager.GetNextPhase(PhaseNames.Movement, Game).Returns(_mockNextPhase);
        
        _sut = new MovementPhase(Game);

        // Add two players with units
        Game.HandleCommand(CreateJoinCommand(_player1Id, "Player 1",2));
        Game.HandleCommand(CreateJoinCommand(_player2Id, "Player 2"));
        Game.HandleCommand(CreateStatusCommand(_player1Id, PlayerStatus.Ready));
        Game.HandleCommand(CreateStatusCommand(_player2Id, PlayerStatus.Ready));

        // Add units to players
        var player1 = Game.Players[0];
        _unit1Id=player1.Units[0].Id;

        var player2 = Game.Players[1];

        // Set initiative order (player2 won, player1 lost)
        Game.SetInitiativeOrder(new List<IPlayer> { player2, player1 });
    }

    [Fact]
    public void Enter_ShouldSetFirstPlayerActive()
    {
        // Act
        _sut.Enter();
    
        // Assert
        Game.ActivePlayer.ShouldBe(Game.Players[0]); // Player who lost initiative moves first
    }

    [Fact]
    public void HandleCommand_WhenValidMove_ShouldPublishAndUpdateTurn()
    {
        // Arrange
        _sut.Enter();
        var unit = Game.ActivePlayer!.Units.Single(u => u.Id == _unit1Id);
        unit.Deploy(new HexPosition(1,2,HexDirection.Top));
        
        // Act
        _sut.HandleCommand(new MoveUnitCommand
        {
            MovementType = MovementType.Walk,
            GameOriginId = Game.Id,
            PlayerId = Game.ActivePlayer!.Id,
            UnitId = _unit1Id,
            MovementPath =
            [
                new PathSegment(new HexPosition(1, 2, HexDirection.Top), new HexPosition(3, 1, HexDirection.Bottom), 1)
                    .ToData()
            ]
        });
    
        // Assert
        unit.Position?.Coordinates.ToString().ShouldBe("0301");
    }

    [Fact]
    public void HandleCommand_WhenWrongPlayer_ShouldIgnoreCommand()
    {
        // Arrange
        _sut.Enter();
        var wrongPlayerId = Guid.NewGuid();
    
        // Act
        _sut.HandleCommand(new MoveUnitCommand
        {
            MovementType = MovementType.Walk,
            GameOriginId = Game.Id,
            PlayerId = wrongPlayerId,
            UnitId = _unit1Id,
            MovementPath =
            [
                new PathSegment(new HexPosition(1, 2, HexDirection.Top), new HexPosition(1, 1, HexDirection.Bottom), 1)
                    .ToData()
            ]
        });
    
        // Assert
        foreach (var unit in Game.ActivePlayer!.Units)
        {
            unit.Position.ShouldBeNull();
        }
    }
    
    [Fact]
    public void HandleCommand_WhenAllUnitsOfPlayerMoved_ShouldActivateNextPlayer()
    {
        // Arrange
        _sut.Enter();
        var player2 = Game.Players[1];
        CommandPublisher.ClearReceivedCalls();
    
        // Move all units of first player
        foreach (var unit in Game.ActivePlayer!.Units)
        {
            unit.Deploy(new HexPosition(1,2,HexDirection.Top));
            _sut.HandleCommand(new MoveUnitCommand
            {
                MovementType = MovementType.Walk,
                GameOriginId = Game.Id,
                PlayerId = Game.ActivePlayer!.Id,
                UnitId = unit.Id,
                MovementPath =
                [
                    new PathSegment(new HexPosition(1, 2, HexDirection.Top), new HexPosition(1, 1, HexDirection.Bottom),
                        1).ToData()
                ]
            });
        }

        // Assert
        CommandPublisher.Received().PublishCommand(Arg.Is<ChangeActivePlayerCommand>(cmd =>
        cmd.GameOriginId == Game.Id &&
        cmd.PlayerId == player2.Id ));
    }

    [Fact]
    public void HandleCommand_WhenAllUnitsMoved_ShouldTransitionToNextPhase()
    {
        // Arrange
        _sut.Enter();
    
        // Move all units
        foreach (var player in Game.Players)
        {
            foreach (var unit in player.Units)
            {
                unit.Deploy(new HexPosition(1,2,HexDirection.Top));
                _sut.HandleCommand(new MoveUnitCommand
                {
                    MovementType = MovementType.Walk,
                    GameOriginId = Game.Id,
                    PlayerId = Game.ActivePlayer!.Id,
                    UnitId = unit.Id,
                    MovementPath =
                    [
                        new PathSegment(new HexPosition(1, 2, HexDirection.Top),
                            new HexPosition(1, 1, HexDirection.Bottom), 1).ToData()
                    ]
                });
            }
        }
    
        // Assert
        MockPhaseManager.Received(1).GetNextPhase(PhaseNames.Movement, Game);
        _mockNextPhase.Received(1).Enter();
    }
    
    [Fact]
    public void ProcessStandupCommand_WhenSuccessful_ShouldPublishMechStandUpCommand()
    {
        // Arrange
        _sut.Enter();
        var unit = Game.ActivePlayer!.Units.Single(u => u.Id == _unit1Id) as Mech;
        // Make sure the unit is a Mech and is prone
        unit!.SetProne();
        
        // Configure the FallProcessor to return successful standup data
        var psrBreakdown = new PsrBreakdown
        {
            BasePilotingSkill = 4,
            Modifiers = [],
        };
        
        var successfulPsrData = new PilotingSkillRollData
        {
            RollType = PilotingSkillRollType.StandupAttempt,
            DiceResults = [3, 3],
            IsSuccessful = true,
            PsrBreakdown = psrBreakdown
        };
        
        var successfulStandupData = new FallContextData
        {
            UnitId = unit.Id,
            GameId = Game.Id,
            IsFalling = false, // Not falling = successful standup
            ReasonType = FallReasonType.StandUpAttempt,
            PilotingSkillRoll = successfulPsrData,
            LevelsFallen = 0,
            WasJumping = false
        };
        
        // Set up the Mock for ProcessStandupAttempt
        Game.FallProcessor.ProcessMovementAttempt(unit, FallReasonType.StandUpAttempt, Game).Returns(successfulStandupData);
        
        CommandPublisher.ClearReceivedCalls();
        
        // Act
        _sut.HandleCommand(new TryStandupCommand
        {
            GameOriginId = Game.Id,
            PlayerId = _player1Id,
            UnitId = _unit1Id,
            Timestamp = DateTime.UtcNow
        });
        
        // Assert
        CommandPublisher.Received().PublishCommand(Arg.Is<MechStandUpCommand>(cmd =>
            cmd.GameOriginId == Game.Id &&
            cmd.UnitId == _unit1Id &&
            cmd.PilotingSkillRoll.IsSuccessful));
        CommandPublisher.DidNotReceive().PublishCommand(Arg.Any<MechFallCommand>());
        unit.StandupAttempts.ShouldBe(1);
    }

    [Fact]
    public void ProcessStandupCommand_WhenFailed_ShouldPublishMechFallCommand()
    {
        // Arrange
        _sut.Enter();
        var unit = Game.ActivePlayer!.Units.Single(u => u.Id == _unit1Id) as Mech;
        // Make sure the unit is a Mech and is prone
        unit!.SetProne();

        // Configure the FallProcessor to return failed standup data
        var psrBreakdown = new PsrBreakdown
        {
            BasePilotingSkill = 4,
            Modifiers = [],
        };
        
        var failedPsrData = new PilotingSkillRollData
        {
            RollType = PilotingSkillRollType.StandupAttempt,
            DiceResults = [1, 1],
            IsSuccessful = false,
            PsrBreakdown = psrBreakdown
        };
        
        var fallContextData = new FallContextData
        {
            UnitId = unit.Id,
            GameId = Game.Id,
            IsFalling = true, // failed standup
            ReasonType = FallReasonType.StandUpAttempt,
            PilotingSkillRoll = failedPsrData,
            LevelsFallen = 0,
            WasJumping = false
        };
        
        // Set up the Mock for ProcessStandupAttempt
        Game.FallProcessor.ProcessMovementAttempt(unit, FallReasonType.StandUpAttempt, Game).Returns(fallContextData);

        CommandPublisher.ClearReceivedCalls();

        // Act
        _sut.HandleCommand(new TryStandupCommand
        {
            GameOriginId = Game.Id,
            PlayerId = _player1Id,
            UnitId = _unit1Id,
            Timestamp = DateTime.UtcNow
        });

        // Assert
        CommandPublisher.DidNotReceive().PublishCommand(Arg.Any<MechStandUpCommand>());
        CommandPublisher.Received(1).PublishCommand(Arg.Is<MechFallCommand>(cmd =>
            cmd.GameOriginId == Game.Id &&
            cmd.UnitId == _unit1Id &&
            cmd.FallPilotingSkillRoll == failedPsrData));
        unit.StandupAttempts.ShouldBe(1);
    }
    
    [Fact]
    public void ProcessStandupCommand_WhenUnitNotFound_ShouldNotPublishCommand()
    {
        // Arrange
        _sut.Enter();
        var invalidUnitId = Guid.NewGuid();
        
        CommandPublisher.ClearReceivedCalls();
        
        // Act
        _sut.HandleCommand(new TryStandupCommand
        {
            GameOriginId = Game.Id,
            PlayerId = _player1Id,
            UnitId = invalidUnitId,
            Timestamp = DateTime.UtcNow
        });
        
        // Assert
        CommandPublisher.DidNotReceive().PublishCommand(Arg.Any<MechStandUpCommand>());
    }

    [Fact]
    public void ProcessStandupCommand_WhenUnitCannotStandup_ShouldNotPublishCommand()
    {
        // Arrange
        _sut.Enter();
        var unit = Game.ActivePlayer!.Units.Single(u => u.Id == _unit1Id) as Mech;
        // Make sure the unit is a Mech and is prone
        unit!.SetProne();
        
        // Make the unit unable to stand up by setting its status to shut down
        unit.Shutdown();
        unit.CanStandup().ShouldBeFalse();
        
        CommandPublisher.ClearReceivedCalls();
        
        // Act
        _sut.HandleCommand(new TryStandupCommand
        {
            GameOriginId = Game.Id,
            PlayerId = _player1Id,
            UnitId = _unit1Id,
            Timestamp = DateTime.UtcNow
        });
        
        // Assert
        CommandPublisher.DidNotReceive().PublishCommand(Arg.Any<MechStandUpCommand>());
        CommandPublisher.DidNotReceive().PublishCommand(Arg.Any<MechFallCommand>());
        unit.StandupAttempts.ShouldBe(0);
    }

    [Fact]
    public void HandleCommand_WhenJumpWithDamagedGyroSucceeds_ShouldPublishMoveCommand()
    {
        // Arrange
        _sut.Enter();
        var unit = Game.ActivePlayer!.Units.Single(u => u.Id == _unit1Id) as Mech;
        unit!.Deploy(new HexPosition(1, 2, HexDirection.Top));

        // Damage the gyro to require PSR
        var gyro = unit.GetAllComponents<Gyro>().First();
        gyro.Hit();

        // Mock successful PSR
        var successfulFallContext = new FallContextData
        {
            UnitId = _unit1Id,
            GameId = Game.Id,
            IsFalling = false,
            ReasonType = FallReasonType.JumpWithDamage,
            PilotingSkillRoll = new PilotingSkillRollData
            {
                RollType = PilotingSkillRollType.JumpWithDamage,
                DiceResults = [6, 6],
                IsSuccessful = true,
                PsrBreakdown = new PsrBreakdown { BasePilotingSkill = 4, Modifiers = [] }
            }
        };

        Game.FallProcessor.ProcessMovementAttempt(unit, FallReasonType.JumpWithDamage, Game).Returns(successfulFallContext);

        var moveCommand = new MoveUnitCommand
        {
            MovementType = MovementType.Jump,
            GameOriginId = Game.Id,
            PlayerId = Game.ActivePlayer!.Id,
            UnitId = _unit1Id,
            MovementPath = [
                new PathSegment(new HexPosition(1, 2, HexDirection.Top), new HexPosition(3, 1, HexDirection.Bottom), 1)
                    .ToData()
            ]
        };

        // Act
        _sut.HandleCommand(moveCommand);

        // Assert
        CommandPublisher.Received().PublishCommand(Arg.Is<MoveUnitCommand>(cmd =>
            cmd.UnitId == _unit1Id && cmd.MovementType == MovementType.Jump));
        CommandPublisher.DidNotReceive().PublishCommand(Arg.Any<MechFallCommand>());
    }

    [Fact]
    public void HandleCommand_WhenJumpWithDamagedGyroFails_ShouldPublishFallCommand()
    {
        // Arrange
        _sut.Enter();
        var unit = Game.ActivePlayer!.Units.Single(u => u.Id == _unit1Id) as Mech;
        unit!.Deploy(new HexPosition(1, 2, HexDirection.Top));

        // Damage the gyro to require PSR
        var gyro = unit.GetAllComponents<Gyro>().First();
        gyro.Hit();

        // Mock failed PSR
        var failedFallContext = new FallContextData
        {
            UnitId = _unit1Id,
            GameId = Game.Id,
            IsFalling = true,
            ReasonType = FallReasonType.JumpWithDamage,
            PilotingSkillRoll = new PilotingSkillRollData
            {
                RollType = PilotingSkillRollType.JumpWithDamage,
                DiceResults = [2, 2],
                IsSuccessful = false,
                PsrBreakdown = new PsrBreakdown { BasePilotingSkill = 4, Modifiers = [] }
            },
            FallingDamageData = new FallingDamageData(
                HexDirection.Top,
                new HitLocationsData([], 5),
                new DiceResult(3)
            )
        };

        Game.FallProcessor.ProcessMovementAttempt(unit, FallReasonType.JumpWithDamage, Game).Returns(failedFallContext);

        var moveCommand = new MoveUnitCommand
        {
            MovementType = MovementType.Jump,
            GameOriginId = Game.Id,
            PlayerId = Game.ActivePlayer!.Id,
            UnitId = _unit1Id,
            MovementPath = [
                new PathSegment(new HexPosition(1, 2, HexDirection.Top), new HexPosition(3, 1, HexDirection.Bottom), 1)
                    .ToData()
            ]
        };

        // Act
        _sut.HandleCommand(moveCommand);

        // Assert
        CommandPublisher.Received().PublishCommand(Arg.Is<MechFallCommand>(cmd =>
            cmd.UnitId == _unit1Id && cmd.DamageData != null));
        CommandPublisher.Received().PublishCommand(Arg.Is<MoveUnitCommand>(cmd =>
            cmd.UnitId == _unit1Id && cmd.MovementType == MovementType.Jump));
    }
}
