using NSubstitute;
using Sanet.MakaMek.Core.Data.Community;
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
        // Create a mock next phase and configure the phase manager
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
    
        // Move all units of the first player
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
    public void ProcessStandupCommand_ShouldPublishMechStandUpCommand_WhenSuccessful()
    {
        // Arrange
        _sut.Enter();
        var unit = Game.ActivePlayer!.Units.Single(u => u.Id == _unit1Id) as Mech;
        // Make sure the unit is a Mech and is prone
        unit!.Deploy(new HexPosition(1, 2, HexDirection.Top));
        unit.SetProne();
        
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
    public void ProcessStandupCommand_ShouldPublishMechFallCommand_WhenFailed()
    {
        // Arrange
        _sut.Enter();
        var unit = Game.ActivePlayer!.Units.Single(u => u.Id == _unit1Id) as Mech;
        // Make sure the unit is a Mech and is prone
        unit!.Deploy(new HexPosition(1, 2, HexDirection.Top));
        unit.SetProne();

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
    public void ProcessStandupCommand_ShouldPublishConsciousnessCommand_WhenFailedAndPilotTookDamage()
    {
        // Arrange
        _sut.Enter();
        var unit = Game.ActivePlayer!.Units.Single(u => u.Id == _unit1Id) as Mech;
        // Make sure the unit is a Mech and is prone
        unit!.Deploy(new HexPosition(1, 2, HexDirection.Top));
        unit.SetProne();

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

        var consciousnessCommand = new PilotConsciousnessRollCommand
        {
            GameOriginId = Guid.NewGuid(),
            PilotId = unit.Pilot!.Id,
            UnitId = unit.Id,
            IsRecoveryAttempt = false,
            ConsciousnessNumber = 4,
            DiceResults = [7, 2],
            IsSuccessful = false 
        };
        MockConsciousnessCalculator.MakeConsciousnessRolls(unit.Pilot!).Returns([consciousnessCommand]);
        
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
        CommandPublisher.Received(1).PublishCommand(
            Arg.Is<PilotConsciousnessRollCommand>(cmd => 
                cmd.GameOriginId == Game.Id &&
                cmd.IsRecoveryAttempt == false &&
                cmd.IsSuccessful == false));
    }
    
    [Fact]
    public void ProcessStandupCommand_ShouldNotPublishCommand_WhenUnitNotFound()
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
    public void ProcessStandupCommand_ShouldNotPublishCommand_WhenUnitCannotStandup()
    {
        // Arrange
        _sut.Enter();
        var unit = Game.ActivePlayer!.Units.Single(u => u.Id == _unit1Id) as Mech;
        // Make sure the unit is a Mech and is prone
        unit!.SetProne();
        
        // Make the unit unable to stand up by setting its status to shut down
        var shutdownData = new ShutdownData
        {
            Reason = ShutdownReason.Voluntary,
            Turn = 1
        };
        unit.Shutdown(shutdownData);
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
    public void ProcessStandupCommand_ShouldCompleteUitMovement_WhenNoMpAfterStandup()
    {
        // Arrange
        _sut.Enter();
        var unit = Game.ActivePlayer!.Units.Single(u => u.Id == _unit1Id) as Mech;
        // Make sure the unit is a Mech and is prone
        unit!.Deploy(new HexPosition(1, 2, HexDirection.Top));
        // destroy the left leg to remove MP
        var leg = unit.Parts.First(p => p.Location == PartLocation.LeftLeg);
        leg.ApplyDamage(20, HitDirection.Front);
        unit.SetProne();

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
        Game.FallProcessor.ProcessMovementAttempt(unit, FallReasonType.StandUpAttempt, Game)
            .Returns(successfulStandupData);

        CommandPublisher.ClearReceivedCalls();

        // Act
        _sut.HandleCommand(new TryStandupCommand
        {
            GameOriginId = Game.Id,
            PlayerId = _player1Id,
            UnitId = _unit1Id,
            Timestamp = DateTime.UtcNow,
            MovementTypeAfterStandup = MovementType.Run
        });

        // Assert
        CommandPublisher.Received().PublishCommand(Arg.Is<MoveUnitCommand>(cmd =>
            cmd.UnitId == _unit1Id && cmd.MovementType == MovementType.Run));

        CommandPublisher.Received().PublishCommand(Arg.Is<ChangeActivePlayerCommand>(cmd =>
            cmd.GameOriginId == Game.Id));
    }

    [Fact]
    public void HandleCommand_ShouldPublishMoveCommand_WhenJumpWithDamagedGyroSucceeds()
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
                new DiceResult(3), HitDirection.Front
            )
        };

        MockFallProcessor.ProcessMovementAttempt(unit, FallReasonType.JumpWithDamage, Game).Returns(failedFallContext);

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
    
    [Fact]
    public void HandleCommand_WhenJumpWithDamagedGyroFails_ShouldPublishConsciousnessCommand_WhenPilotTakesDamage()
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
                new DiceResult(3), HitDirection.Front
            )
        };

        MockFallProcessor.ProcessMovementAttempt(unit, FallReasonType.JumpWithDamage, Game).Returns(failedFallContext);

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
        
        var consciousnessCommand = new PilotConsciousnessRollCommand
        {
            GameOriginId = Guid.NewGuid(),
            PilotId = unit.Pilot!.Id,
            UnitId = unit.Id,
            IsRecoveryAttempt = false,
            ConsciousnessNumber = 4,
            DiceResults = [7, 2],
            IsSuccessful = false 
        };
        MockConsciousnessCalculator.MakeConsciousnessRolls(unit.Pilot!).Returns([consciousnessCommand]);

        // Act
        _sut.HandleCommand(moveCommand);

        // Assert
        CommandPublisher.Received(1).PublishCommand(
            Arg.Is<PilotConsciousnessRollCommand>(cmd => 
                cmd.GameOriginId == Game.Id &&
                cmd.IsRecoveryAttempt == false &&
                cmd.IsSuccessful == false));
    }

    [Fact]
    public void ProcessFallCommand_ShouldApplyCriticalHits_WhenFallDamageCausesStructureDamage()
    {
        // Arrange
        _sut.Enter();
        var unit = Game.ActivePlayer!.Units.Single(u => u.Id == _unit1Id) as Mech;
        unit!.Deploy(new HexPosition(1, 2, HexDirection.Top));
        unit.SetProne();

        // Configure the FallProcessor to return failed standup data with damage
        var fallDamageData = new FallingDamageData(
            HexDirection.Bottom,
            new HitLocationsData(
                HitLocations: [new LocationHitData(
                    [new LocationDamageData(PartLocation.CenterTorso, 3, 2, false)], // Structure damage
                    [],
                    [3, 4],
                    PartLocation.CenterTorso)],
                TotalDamage: 5),
            new DiceResult(3),
            HitDirection.Front);

        var fallContextData = new FallContextData
        {
            UnitId = unit.Id,
            GameId = Game.Id,
            IsFalling = true,
            ReasonType = FallReasonType.StandUpAttempt,
            PilotingSkillRoll = new PilotingSkillRollData
            {
                RollType = PilotingSkillRollType.StandupAttempt,
                DiceResults = [1, 1],
                IsSuccessful = false,
                PsrBreakdown = new PsrBreakdown { BasePilotingSkill = 4, Modifiers = [] }
            },
            FallingDamageData = fallDamageData,
            LevelsFallen = 0,
            WasJumping = false
        };

        Game.FallProcessor.ProcessMovementAttempt(unit, FallReasonType.StandUpAttempt, Game).Returns(fallContextData);

        // Setup critical hits calculator to return critical hits for fall damage
        var fallCriticalHitsCommand = new CriticalHitsResolutionCommand
        {
            GameOriginId = Game.Id,
            TargetId = unit.Id,
            CriticalHits = [new LocationCriticalHitsData(
                PartLocation.CenterTorso,
                [5, 4],
                2,
                [new ComponentHitData { Type = MakaMekComponent.Engine, Slot = 1 }],
                false)]
        };

        MockCriticalHitsCalculator.CalculateAndApplyCriticalHits(
                Arg.Is<Unit>(u => u.Id == unit.Id),
                Arg.Any<List<LocationDamageData>>())
            .Returns(fallCriticalHitsCommand);

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
        // Verify that critical hits calculator was called with structure damage
        MockCriticalHitsCalculator.Received().CalculateAndApplyCriticalHits(
            Arg.Is<Unit>(u => u.Id == unit.Id),
            Arg.Is<List<LocationDamageData>>(list =>
                list.Any(d => d.Location == PartLocation.CenterTorso && d.StructureDamage > 0)));

        // Verify that the critical hits command was published
        CommandPublisher.Received().PublishCommand(
            Arg.Is<CriticalHitsResolutionCommand>(cmd =>
                cmd.TargetId == unit.Id &&
                cmd.GameOriginId == Game.Id &&
                cmd.CriticalHits.Any(ch => ch.Location == PartLocation.CenterTorso)));

        // Verify that fall command was also published
        CommandPublisher.Received().PublishCommand(
            Arg.Is<MechFallCommand>(cmd =>
                cmd.UnitId == unit.Id &&
                cmd.DamageData != null));
    }

    [Fact]
    public void ProcessFallCommand_ShouldNotApplyCriticalHits_WhenFallDamageOnlyAffectsArmor()
    {
        // Arrange
        _sut.Enter();
        var unit = Game.ActivePlayer!.Units.Single(u => u.Id == _unit1Id) as Mech;
        unit!.Deploy(new HexPosition(1, 2, HexDirection.Top));
        unit.SetProne();

        // Configure the FallProcessor to return failed standup data with armor-only damage
        var fallDamageData = new FallingDamageData(
            HexDirection.Bottom,
            new HitLocationsData(
                HitLocations: [new LocationHitData(
                    [new LocationDamageData(PartLocation.CenterTorso, 5, 0, false)], // Only armor damage
                    [],
                    [3, 4],
                    PartLocation.CenterTorso)],
                TotalDamage: 5),
            new DiceResult(3),
            HitDirection.Front);

        var fallContextData = new FallContextData
        {
            UnitId = unit.Id,
            GameId = Game.Id,
            IsFalling = true,
            ReasonType = FallReasonType.StandUpAttempt,
            PilotingSkillRoll = new PilotingSkillRollData
            {
                RollType = PilotingSkillRollType.StandupAttempt,
                DiceResults = [1, 1],
                IsSuccessful = false,
                PsrBreakdown = new PsrBreakdown { BasePilotingSkill = 4, Modifiers = [] }
            },
            FallingDamageData = fallDamageData,
            LevelsFallen = 0,
            WasJumping = false
        };

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
        // Verify that critical hits calculator was not called since no structure damage
        MockCriticalHitsCalculator.DidNotReceive().CalculateAndApplyCriticalHits(
            Arg.Any<Unit>(),
            Arg.Any<List<LocationDamageData>>());

        // Verify that no critical hits command was published
        CommandPublisher.DidNotReceive().PublishCommand(
            Arg.Any<CriticalHitsResolutionCommand>());

        // Verify that fall command was still published
        CommandPublisher.Received().PublishCommand(
            Arg.Is<MechFallCommand>(cmd =>
                cmd.UnitId == unit.Id &&
                cmd.DamageData != null));
    }
}
