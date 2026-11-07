using NSubstitute;
using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Data.Game.Mechanics.PhysicalAttacks;
using Sanet.MakaMek.Core.Data.Game.Mechanics.PilotingSkillRollContexts;
using Sanet.MakaMek.Core.Data.Units.Components;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Models.Game.Phases;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components;
using Sanet.MakaMek.Core.Models.Units.Components.Internal;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Core.Models.Units.Pilots;
using Sanet.MakaMek.Map.Data;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Models.MovementCosts;
using Sanet.MakaMek.Map.Models.Terrains;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Phases;

public class MovementPhaseTests : GamePhaseTestsBase
{
    private MovementPhase _sut = null!;
    private readonly Guid _player1Id = Guid.NewGuid();
    private readonly Guid _player2Id = Guid.NewGuid();
    private  Guid _unit1Id;
    private IGamePhase _mockNextPhase = null!;

    protected override void SetupSut()
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
        Game.PhaseStepState!.Value.ActivePlayer.ShouldBe(Game.Players[0]); // Player who lost initiative moves first
    }

    [Fact]
    public void HandleCommand_WhenValidMove_ShouldPublishAndUpdateTurn()
    {
        // Arrange
        _sut.Enter();
        var unit = Game.PhaseStepState!.Value.ActivePlayer.Units.Single(u => u.Id == _unit1Id);
        unit.Deploy(new HexPosition(1,2,HexDirection.Top), null);
        
        // Act
        _sut.HandleCommand(new MoveUnitCommand
        {
            MovementType = MovementType.Walk,
            GameOriginId = Game.Id,
            PlayerId = Game.PhaseStepState!.Value.ActivePlayer.Id,
            UnitId = _unit1Id,
            MovementPath =
            [
                new PathSegment(new HexPosition(1, 2, HexDirection.Top), new HexPosition(3, 1, HexDirection.Bottom), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }])
                    .ToData()
            ]
        });
    
        // Assert
        unit.Position?.Coordinates.ToString().ShouldBe("0301");
        CommandPublisher.Received().PublishCommand(Arg.Is<MoveUnitCommand>(cmd => cmd.UnitId == _unit1Id && cmd.IsCompleted));
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
                new PathSegment(new HexPosition(1, 2, HexDirection.Top), new HexPosition(1, 1, HexDirection.Bottom), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }])
                    .ToData()
            ]
        });
    
        // Assert
        foreach (var unit in Game.PhaseStepState!.Value.ActivePlayer.Units)
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
        foreach (var unit in Game.PhaseStepState!.Value.ActivePlayer.Units)
        {
            unit.Deploy(new HexPosition(1,2,HexDirection.Top), null);
            _sut.HandleCommand(new MoveUnitCommand
            {
                MovementType = MovementType.Walk,
                GameOriginId = Game.Id,
                PlayerId = Game.PhaseStepState!.Value.ActivePlayer.Id,
                UnitId = unit.Id,
                MovementPath =
                [
                    new PathSegment(new HexPosition(1, 2, HexDirection.Top), new HexPosition(1, 1, HexDirection.Bottom),
                        [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }]).ToData()
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
                unit.Deploy(new HexPosition(1,2,HexDirection.Top), null);
                _sut.HandleCommand(new MoveUnitCommand
                {
                    MovementType = MovementType.Walk,
                    GameOriginId = Game.Id,
                    PlayerId = Game.PhaseStepState!.Value.ActivePlayer.Id,
                    UnitId = unit.Id,
                    MovementPath =
                    [
                        new PathSegment(new HexPosition(1, 2, HexDirection.Top),
                            new HexPosition(1, 1, HexDirection.Bottom), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }]).ToData()
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
        var unit = Game.PhaseStepState!.Value.ActivePlayer.Units.Single(u => u.Id == _unit1Id) as Mech;
        // Make sure the unit is a Mech and is prone
        unit!.Deploy(new HexPosition(1, 2, HexDirection.Top), null);
        unit.SetProne();
        
        // Configure the FallProcessor to return successful standup data
        var psrBreakdown = new PsrBreakdown
        {
            BasePilotingSkill = 4,
            Modifiers = [],
        };
        
        var successfulPsrData = new PilotingSkillRollData
        {
            RollContext = new PilotingSkillRollContext(PilotingSkillRollType.StandupAttempt),
            DiceResults = [3, 3],
            IsSuccessful = true,
            PsrBreakdown = psrBreakdown
        };

        var successfulStandupData = new FallContextData
        {
            UnitId = unit.Id,
            GameId = Game.Id,
            IsFalling = false, // Not falling = successful standup
            PilotingSkillRoll = successfulPsrData,
            LevelsFallen = 0,
            WasJumping = false
        };

        // Set up the Mock for ProcessStandupAttempt
        Game.FallProcessor.ProcessMovementAttempt(unit, new PilotingSkillRollContext(PilotingSkillRollType.StandupAttempt), Game, MovementType.StandingStill).Returns(successfulStandupData);
        
        CommandPublisher.ClearReceivedCalls();
        
        // Act
        _sut.HandleCommand(new TryStandupCommand
        {
            GameOriginId = Game.Id,
            PlayerId = _player1Id,
            UnitId = _unit1Id,
            Timestamp = DateTime.UtcNow,
            NewFacing = HexDirection.Bottom,
            MovementTypeAfterStandup = MovementType.Run
        });
        
        // Assert
        CommandPublisher.Received().PublishCommand(Arg.Is<MechStandUpCommand>(cmd =>
            cmd.GameOriginId == Game.Id &&
            cmd.UnitId == _unit1Id &&
            cmd.PilotingSkillRoll.IsSuccessful &&
            cmd.NewFacing == HexDirection.Bottom &&
            cmd.MovementTypeAfterStandup == MovementType.Run));
        CommandPublisher.DidNotReceive().PublishCommand(Arg.Any<MechFallCommand>());
        unit.StandupAttempts.ShouldBe(1);
        unit.MovementTaken.ShouldNotBeNull();
        unit.MovementTaken.MovementType.ShouldBe(MovementType.Run);
    }

    [Fact]
    public void ProcessStandupCommand_ShouldPublishMechFallCommand_WhenFailed()
    {
        // Arrange
        _sut.Enter();
        var unit = Game.PhaseStepState!.Value.ActivePlayer.Units.Single(u => u.Id == _unit1Id) as Mech;
        // Make sure the unit is a Mech and is prone
        unit!.Deploy(new HexPosition(1, 2, HexDirection.Top), null);
        unit.SetProne();

        // Configure the FallProcessor to return failed standup data
        var psrBreakdown = new PsrBreakdown
        {
            BasePilotingSkill = 4,
            Modifiers = [],
        };
        
        var failedPsrData = new PilotingSkillRollData
        {
            RollContext = new PilotingSkillRollContext(PilotingSkillRollType.StandupAttempt),
            DiceResults = [1, 1],
            IsSuccessful = false,
            PsrBreakdown = psrBreakdown
        };

        var fallContextData = new FallContextData
        {
            UnitId = unit.Id,
            GameId = Game.Id,
            IsFalling = true, // failed standup
            PilotingSkillRoll = failedPsrData,
            LevelsFallen = 0,
            WasJumping = false
        };

        // Set up the Mock for ProcessStandupAttempt
        Game.FallProcessor.ProcessMovementAttempt(unit, new PilotingSkillRollContext(PilotingSkillRollType.StandupAttempt), Game, Arg.Any<MovementType>()).Returns(fallContextData);

        CommandPublisher.ClearReceivedCalls();

        // Act
        _sut.HandleCommand(new TryStandupCommand
        {
            GameOriginId = Game.Id,
            PlayerId = _player1Id,
            UnitId = _unit1Id,
            Timestamp = DateTime.UtcNow,
            NewFacing = HexDirection.Bottom,
            MovementTypeAfterStandup = MovementType.Run
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
        var unit = Game.PhaseStepState!.Value.ActivePlayer.Units.Single(u => u.Id == _unit1Id) as Mech;
        // Make sure the unit is a Mech and is prone
        unit!.Deploy(new HexPosition(1, 2, HexDirection.Top), null);
        unit.SetProne();

        // Configure the FallProcessor to return failed standup data
        var psrBreakdown = new PsrBreakdown
        {
            BasePilotingSkill = 4,
            Modifiers = [],
        };
        
        var failedPsrData = new PilotingSkillRollData
        {
            RollContext = new PilotingSkillRollContext(PilotingSkillRollType.StandupAttempt),
            DiceResults = [1, 1],
            IsSuccessful = false,
            PsrBreakdown = psrBreakdown
        };

        var fallContextData = new FallContextData
        {
            UnitId = unit.Id,
            GameId = Game.Id,
            IsFalling = true, // failed standup
            PilotingSkillRoll = failedPsrData,
            LevelsFallen = 0,
            WasJumping = false
        };

        // Set up the Mock for ProcessStandupAttempt
        Game.FallProcessor.ProcessMovementAttempt(unit, new PilotingSkillRollContext(PilotingSkillRollType.StandupAttempt), Game, MovementType.StandingStill).Returns(fallContextData);

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
        CommandPublisher.Received(1).PublishCommand(Arg.Is<ErrorCommand>(cmd =>
            cmd.ErrorCode == ErrorCode.InvalidGameState));
    }

    [Fact]
    public void ProcessStandupCommand_ShouldNotPublishCommand_WhenUnitCannotStandup()
    {
        // Arrange
        _sut.Enter();
        var unit = Game.PhaseStepState!.Value.ActivePlayer.Units.Single(u => u.Id == _unit1Id) as Mech;
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
    public void HandleCommand_ShouldPublishMoveCommand_WhenJumpWithDamagedGyroSucceeds()
    {
        // Arrange
        _sut.Enter();
        var unit = Game.PhaseStepState!.Value.ActivePlayer.Units.Single(u => u.Id == _unit1Id) as Mech;
        unit!.Deploy(new HexPosition(1, 2, HexDirection.Top), null);

        // Damage the gyro to require PSR
        var gyro = unit.GetAllComponents<Gyro>().First();
        gyro.Hit();

        // Mock successful PSR
        var successfulFallContext = new FallContextData
        {
            UnitId = _unit1Id,
            GameId = Game.Id,
            IsFalling = false,
            PilotingSkillRoll = new PilotingSkillRollData
            {
                RollContext = new PilotingSkillRollContext(PilotingSkillRollType.JumpWithDamage),
                DiceResults = [6, 6],
                IsSuccessful = true,
                PsrBreakdown = new PsrBreakdown { BasePilotingSkill = 4, Modifiers = [] }
            }
        };

        Game.FallProcessor.ProcessMovementAttempt(unit, new PilotingSkillRollContext(PilotingSkillRollType.JumpWithDamage), Game, MovementType.Jump).Returns(successfulFallContext);

        var moveCommand = new MoveUnitCommand
        {
            MovementType = MovementType.Jump,
            GameOriginId = Game.Id,
            PlayerId = Game.PhaseStepState!.Value.ActivePlayer.Id,
            UnitId = _unit1Id,
            MovementPath = [
                new PathSegment(new HexPosition(1, 2, HexDirection.Top), new HexPosition(3, 1, HexDirection.Bottom), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }])
                    .ToData()
            ]
        };

        // Act
        _sut.HandleCommand(moveCommand);

        // Assert
        CommandPublisher.Received().PublishCommand(Arg.Is<MoveUnitCommand>(cmd =>
            cmd.UnitId == _unit1Id && cmd.MovementType == MovementType.Jump && cmd.IsCompleted));
        CommandPublisher.Received().PublishCommand(Arg.Is<MechFallCommand>(cmd =>
            cmd.UnitId == _unit1Id && cmd.DamageData == null && cmd.FallPilotingSkillRoll!.IsSuccessful));
    }

    [Fact]
    public void HandleCommand_WhenJumpWithDamagedGyroFails_ShouldPublishFallCommand()
    {
        // Arrange
        _sut.Enter();
        var unit = Game.PhaseStepState!.Value.ActivePlayer.Units.Single(u => u.Id == _unit1Id) as Mech;
        unit!.Deploy(new HexPosition(1, 2, HexDirection.Top), null);

        // Damage the gyro to require PSR
        var gyro = unit.GetAllComponents<Gyro>().First();
        gyro.Hit();

        // Mock failed PSR
        var failedFallContext = new FallContextData
        {
            UnitId = _unit1Id,
            GameId = Game.Id,
            IsFalling = true,
            PilotingSkillRoll = new PilotingSkillRollData
            {
                RollContext = new PilotingSkillRollContext(PilotingSkillRollType.JumpWithDamage),
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

        MockFallProcessor.ProcessMovementAttempt(unit, new PilotingSkillRollContext(PilotingSkillRollType.JumpWithDamage), Game, MovementType.Jump).Returns(failedFallContext);

        var moveCommand = new MoveUnitCommand
        {
            MovementType = MovementType.Jump,
            GameOriginId = Game.Id,
            PlayerId = Game.PhaseStepState!.Value.ActivePlayer.Id,
            UnitId = _unit1Id,
            MovementPath = [
                new PathSegment(new HexPosition(1, 2, HexDirection.Top), new HexPosition(3, 1, HexDirection.Bottom), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }])
                    .ToData()
            ]
        };

        // Act
        _sut.HandleCommand(moveCommand);

        // Assert
        CommandPublisher.Received().PublishCommand(Arg.Is<MechFallCommand>(cmd =>
            cmd.UnitId == _unit1Id && cmd.DamageData != null));
        CommandPublisher.Received().PublishCommand(Arg.Is<MoveUnitCommand>(cmd =>
            cmd.UnitId == _unit1Id && cmd.MovementType == MovementType.Jump && cmd.IsCompleted));
    }
    
    [Fact]
    public void HandleCommand_WhenJumpWithDamagedGyroFails_ShouldPublishConsciousnessCommand_WhenPilotTakesDamage()
    {
        // Arrange
        _sut.Enter();
        var unit = Game.PhaseStepState!.Value.ActivePlayer.Units.Single(u => u.Id == _unit1Id) as Mech;
        unit!.Deploy(new HexPosition(1, 2, HexDirection.Top), null);

        // Damage the gyro to require PSR
        var gyro = unit.GetAllComponents<Gyro>().First();
        gyro.Hit();

        // Mock failed PSR
        var failedFallContext = new FallContextData
        {
            UnitId = _unit1Id,
            GameId = Game.Id,
            IsFalling = true,
            PilotingSkillRoll = new PilotingSkillRollData
            {
                RollContext = new PilotingSkillRollContext(PilotingSkillRollType.JumpWithDamage),
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

        MockFallProcessor.ProcessMovementAttempt(unit, new PilotingSkillRollContext(PilotingSkillRollType.JumpWithDamage), Game, MovementType.Jump).Returns(failedFallContext);

        var moveCommand = new MoveUnitCommand
        {
            MovementType = MovementType.Jump,
            GameOriginId = Game.Id,
            PlayerId = Game.PhaseStepState!.Value.ActivePlayer.Id,
            UnitId = _unit1Id,
            MovementPath = [
                new PathSegment(new HexPosition(1, 2, HexDirection.Top), new HexPosition(3, 1, HexDirection.Bottom), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }])
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
        var unit = Game.PhaseStepState!.Value.ActivePlayer.Units.Single(u => u.Id == _unit1Id) as Mech;
        unit!.Deploy(new HexPosition(1, 2, HexDirection.Top), null);
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
            PilotingSkillRoll = new PilotingSkillRollData
            {
                RollContext = new PilotingSkillRollContext(PilotingSkillRollType.StandupAttempt),
                DiceResults = [1, 1],
                IsSuccessful = false,
                PsrBreakdown = new PsrBreakdown { BasePilotingSkill = 4, Modifiers = [] }
            },
            FallingDamageData = fallDamageData,
            LevelsFallen = 0,
            WasJumping = false
        };

        Game.FallProcessor.ProcessMovementAttempt(unit, new PilotingSkillRollContext(PilotingSkillRollType.StandupAttempt), Game, MovementType.StandingStill).Returns(fallContextData);

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

        MockCriticalHitsCalculator.CalculateCriticalHits(
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
        MockCriticalHitsCalculator.Received().CalculateCriticalHits(
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
        var unit = Game.PhaseStepState!.Value.ActivePlayer.Units.Single(u => u.Id == _unit1Id) as Mech;
        unit!.Deploy(new HexPosition(1, 2, HexDirection.Top), null);
        unit.SetProne();

        // Configure the FallProcessor to return failed standup data with armor-only damage
        var fallDamageData = new FallingDamageData(
            HexDirection.Bottom,
            new HitLocationsData(
                HitLocations:
                [
                    new LocationHitData(
                        [new LocationDamageData(PartLocation.CenterTorso, 5, 0, false)], // Only armor damage
                        [],
                        [3, 4],
                        PartLocation.CenterTorso)
                ],
                TotalDamage: 5),
            new DiceResult(3),
            HitDirection.Front);

        var fallContextData = new FallContextData
        {
            UnitId = unit.Id,
            GameId = Game.Id,
            IsFalling = true,
            PilotingSkillRoll = new PilotingSkillRollData
            {
                RollContext = new PilotingSkillRollContext(PilotingSkillRollType.StandupAttempt),
                DiceResults = [1, 1],
                IsSuccessful = false,
                PsrBreakdown = new PsrBreakdown { BasePilotingSkill = 4, Modifiers = [] }
            },
            FallingDamageData = fallDamageData,
            LevelsFallen = 0,
            WasJumping = false
        };

        Game.FallProcessor
            .ProcessMovementAttempt(unit, new PilotingSkillRollContext(PilotingSkillRollType.StandupAttempt), Game, MovementType.StandingStill)
            .Returns(fallContextData);

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
        MockCriticalHitsCalculator.DidNotReceive().CalculateCriticalHits(
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

    [Fact]
    public void ProcessMoveCommand_ShouldProcessWaterEntry_AndTruncateOnFall()
    {
        // Arrange
        SetMap();
        _sut.Enter();
        var unit = Game.PhaseStepState!.Value.ActivePlayer.Units.Single(u => u.Id == _unit1Id) as Mech;
        unit!.Deploy(new HexPosition(1, 2, HexDirection.Top), null);

        var hex = Game.BattleMap!.GetHex(new HexCoordinates(2, 2));
        hex!.AddTerrain(new WaterTerrain(-1));

        var moveCommand = new MoveUnitCommand
        {
            MovementType = MovementType.Walk,
            GameOriginId = Game.Id,
            PlayerId = Game.PhaseStepState!.Value.ActivePlayer.Id,
            UnitId = _unit1Id,
            MovementPath = [
                new PathSegment(new HexPosition(1, 2, HexDirection.Top), new HexPosition(2, 2, HexDirection.Top), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 2 }]).ToData(),
                new PathSegment(new HexPosition(2, 2, HexDirection.Top), new HexPosition(3, 2, HexDirection.Top), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }]).ToData()
            ]
        };

        var fallContextData = new FallContextData
        {
            UnitId = unit.Id,
            GameId = Game.Id,
            IsFalling = true,
            PilotingSkillRoll = new PilotingSkillRollData
            {
                RollContext = new EnteringDeepWaterRollContext(1),
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

        MockFallProcessor.ProcessMovementAttempt(unit, Arg.Is<EnteringDeepWaterRollContext>(c => c.WaterDepth == 1), Game, MovementType.Walk, Arg.Any<HexPosition?>()).Returns(fallContextData);

        CommandPublisher.ClearReceivedCalls();

        // Act
        _sut.HandleCommand(moveCommand);

        // Assert
        CommandPublisher.Received().PublishCommand(Arg.Is<MoveUnitCommand>(cmd => 
            cmd.UnitId == _unit1Id && cmd.MovementPath.Count == 1 && cmd.MovementPath[0].To.Coordinates.Q == 2 && !cmd.IsCompleted));
        CommandPublisher.Received().PublishCommand(Arg.Is<MechFallCommand>(cmd => cmd.UnitId == _unit1Id));
    }

    [Fact]
    public void HandleCommand_WhenWalkWaterFall_AndCanStandup_ShouldNotPublishChangeActivePlayer()
    {
        SetMap();
        _sut.Enter();
        MockConsciousnessCalculator.MakeConsciousnessRolls(Arg.Any<IPilot>()).Returns([]);

        var unit = Game.PhaseStepState!.Value.ActivePlayer.Units.Single(u => u.Id == _unit1Id) as Mech;
        unit!.Deploy(new HexPosition(1, 2, HexDirection.Top), null);

        var hex = Game.BattleMap!.GetHex(new HexCoordinates(2, 2));
        hex!.AddTerrain(new WaterTerrain(-1));

        var fallContextData = new FallContextData
        {
            UnitId = unit.Id,
            GameId = Game.Id,
            IsFalling = true,
            PilotingSkillRoll = new PilotingSkillRollData
            {
                RollContext = new EnteringDeepWaterRollContext(1),
                DiceResults = [2, 2],
                IsSuccessful = false,
                PsrBreakdown = new PsrBreakdown { BasePilotingSkill = 4, Modifiers = [] }
            },
            FallingDamageData = new FallingDamageData(
                HexDirection.Top,
                new HitLocationsData([], 5),
                new DiceResult(3), HitDirection.Front)
        };

        MockFallProcessor.ProcessMovementAttempt(unit, Arg.Is<EnteringDeepWaterRollContext>(c => c.WaterDepth == 1), Game, MovementType.Walk, Arg.Any<HexPosition?>())
            .Returns(fallContextData);

        CommandPublisher.ClearReceivedCalls();

        _sut.HandleCommand(new MoveUnitCommand
        {
            MovementType = MovementType.Walk,
            GameOriginId = Game.Id,
            PlayerId = Game.PhaseStepState!.Value.ActivePlayer.Id,
            UnitId = _unit1Id,
            MovementPath =
            [
                new PathSegment(new HexPosition(1, 2, HexDirection.Top), new HexPosition(2, 2, HexDirection.Top), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 2 }]).ToData(),
                new PathSegment(new HexPosition(2, 2, HexDirection.Top), new HexPosition(3, 2, HexDirection.Top), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }]).ToData()
            ]
        });

        CommandPublisher.DidNotReceive().PublishCommand(Arg.Any<ChangeActivePlayerCommand>());
        CommandPublisher.Received().PublishCommand(Arg.Is<MoveUnitCommand>(cmd => cmd.UnitId == _unit1Id && !cmd.IsCompleted));
    }

    [Fact]
    public void HandleCommand_WhenWalkWaterFall_AndCannotStandup_ShouldPublishChangeActivePlayer()
    {
        SetMap();
        _sut.Enter();

        var unit = Game.PhaseStepState!.Value.ActivePlayer.Units.Single(u => u.Id == _unit1Id) as Mech;
        unit!.Deploy(new HexPosition(1, 2, HexDirection.Top), null);

        var hex = Game.BattleMap!.GetHex(new HexCoordinates(2, 2));
        hex!.AddTerrain(new WaterTerrain(-1));

        var pilotDamagePsr = new PilotingSkillRollData
        {
            RollContext = new PilotDamageFromFallRollContext(1),
            DiceResults = [2, 2],
            IsSuccessful = false,
            PsrBreakdown = new PsrBreakdown { BasePilotingSkill = 4, Modifiers = [] }
        };

        var fallContextData = new FallContextData
        {
            UnitId = unit.Id,
            GameId = Game.Id,
            IsFalling = true,
            PilotingSkillRoll = new PilotingSkillRollData
            {
                RollContext = new EnteringDeepWaterRollContext(1),
                DiceResults = [2, 2],
                IsSuccessful = false,
                PsrBreakdown = new PsrBreakdown { BasePilotingSkill = 4, Modifiers = [] }
            },
            PilotDamagePilotingSkillRoll = pilotDamagePsr,
            FallingDamageData = new FallingDamageData(
                HexDirection.Top,
                new HitLocationsData([], 5),
                new DiceResult(3), HitDirection.Front)
        };

        MockFallProcessor.ProcessMovementAttempt(unit, Arg.Is<EnteringDeepWaterRollContext>(c => c.WaterDepth == 1), Game, MovementType.Walk, Arg.Any<HexPosition?>())
            .Returns(fallContextData);

        var consciousnessCommand = new PilotConsciousnessRollCommand
        {
            GameOriginId = Guid.Empty,
            PilotId = unit.Pilot!.Id,
            UnitId = unit.Id,
            IsRecoveryAttempt = false,
            ConsciousnessNumber = 4,
            DiceResults = [2, 2],
            IsSuccessful = false,
            Timestamp = DateTime.UtcNow
        };
        MockConsciousnessCalculator.MakeConsciousnessRolls(Arg.Any<IPilot>()).Returns([consciousnessCommand]);

        CommandPublisher.ClearReceivedCalls();

        _sut.HandleCommand(new MoveUnitCommand
        {
            MovementType = MovementType.Walk,
            GameOriginId = Game.Id,
            PlayerId = Game.PhaseStepState!.Value.ActivePlayer.Id,
            UnitId = _unit1Id,
            MovementPath =
            [
                new PathSegment(new HexPosition(1, 2, HexDirection.Top), new HexPosition(2, 2, HexDirection.Top), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 2 }]).ToData(),
                new PathSegment(new HexPosition(2, 2, HexDirection.Top), new HexPosition(3, 2, HexDirection.Top), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }]).ToData()
            ]
        });

        CommandPublisher.Received().PublishCommand(Arg.Any<ChangeActivePlayerCommand>());
        CommandPublisher.DidNotReceive().PublishCommand(Arg.Is<MoveUnitCommand>(cmd => cmd.UnitId == _unit1Id && !cmd.IsCompleted));
        CommandPublisher.Received().PublishCommand(Arg.Is<MoveUnitCommand>(cmd => cmd.UnitId == _unit1Id && cmd.IsCompleted));
    }

    [Fact]
    public void HandleCommand_WhenWalkRubbleFall_AndCannotStandup_ShouldPublishChangeActivePlayer()
    {
        SetMap();
        _sut.Enter();

        var unit = Game.PhaseStepState!.Value.ActivePlayer.Units.Single(u => u.Id == _unit1Id) as Mech;
        unit!.Deploy(new HexPosition(1, 2, HexDirection.Top), null);

        var hex = Game.BattleMap!.GetHex(new HexCoordinates(2, 2));
        hex!.AddTerrain(new RubbleTerrain());

        var pilotDamagePsr = new PilotingSkillRollData
        {
            RollContext = new PilotDamageFromFallRollContext(1),
            DiceResults = [2, 2],
            IsSuccessful = false,
            PsrBreakdown = new PsrBreakdown { BasePilotingSkill = 4, Modifiers = [] }
        };

        var fallContextData = new FallContextData
        {
            UnitId = unit.Id,
            GameId = Game.Id,
            IsFalling = true,
            PilotingSkillRoll = new PilotingSkillRollData
            {
                RollContext = new RubbleEntryRollContext(),
                DiceResults = [2, 2],
                IsSuccessful = false,
                PsrBreakdown = new PsrBreakdown { BasePilotingSkill = 4, Modifiers = [] }
            },
            PilotDamagePilotingSkillRoll = pilotDamagePsr,
            FallingDamageData = new FallingDamageData(
                HexDirection.Top,
                new HitLocationsData([], 5),
                new DiceResult(3), HitDirection.Front)
        };

        MockFallProcessor.ProcessMovementAttempt(unit, Arg.Any<RubbleEntryRollContext>(), Game, MovementType.Walk, Arg.Any<HexPosition?>())
            .Returns(fallContextData);

        var consciousnessCommand = new PilotConsciousnessRollCommand
        {
            GameOriginId = Guid.Empty,
            PilotId = unit.Pilot!.Id,
            UnitId = unit.Id,
            IsRecoveryAttempt = false,
            ConsciousnessNumber = 4,
            DiceResults = [2, 2],
            IsSuccessful = false,
            Timestamp = DateTime.UtcNow
        };
        MockConsciousnessCalculator.MakeConsciousnessRolls(Arg.Any<IPilot>()).Returns([consciousnessCommand]);

        CommandPublisher.ClearReceivedCalls();

        _sut.HandleCommand(new MoveUnitCommand
        {
            MovementType = MovementType.Walk,
            GameOriginId = Game.Id,
            PlayerId = Game.PhaseStepState!.Value.ActivePlayer.Id,
            UnitId = _unit1Id,
            MovementPath =
            [
                new PathSegment(new HexPosition(1, 2, HexDirection.Top), new HexPosition(2, 2, HexDirection.Top), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 2 }]).ToData(),
                new PathSegment(new HexPosition(2, 2, HexDirection.Top), new HexPosition(3, 2, HexDirection.Top), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }]).ToData()
            ]
        });

        CommandPublisher.Received().PublishCommand(Arg.Any<ChangeActivePlayerCommand>());
        CommandPublisher.DidNotReceive().PublishCommand(Arg.Is<MoveUnitCommand>(cmd => cmd.UnitId == _unit1Id && !cmd.IsCompleted));
        CommandPublisher.Received().PublishCommand(Arg.Is<MoveUnitCommand>(cmd => cmd.UnitId == _unit1Id && cmd.IsCompleted));
    }

    [Fact]
    public void HandleCommand_WhenDeferredWaterFall_ShouldIgnoreMoveForOtherUnit()
    {
        SetMap();
        _sut.Enter();
        MockConsciousnessCalculator.MakeConsciousnessRolls(Arg.Any<IPilot>()).Returns([]);

        var activePlayer = Game.PhaseStepState!.Value.ActivePlayer;
        var unit1 = activePlayer.Units.Single(u => u.Id == _unit1Id) as Mech;
        var unit2 = activePlayer.Units.First(u => u.Id != _unit1Id) as Mech;
        unit1!.Deploy(new HexPosition(1, 2, HexDirection.Top), null);
        unit2!.Deploy(new HexPosition(5, 2, HexDirection.Top), null);

        var waterHex = Game.BattleMap!.GetHex(new HexCoordinates(2, 2));
        waterHex!.AddTerrain(new WaterTerrain(-1));

        var fallContextData = new FallContextData
        {
            UnitId = unit1.Id,
            GameId = Game.Id,
            IsFalling = true,
            PilotingSkillRoll = new PilotingSkillRollData
            {
                RollContext = new EnteringDeepWaterRollContext(1),
                DiceResults = [2, 2],
                IsSuccessful = false,
                PsrBreakdown = new PsrBreakdown { BasePilotingSkill = 4, Modifiers = [] }
            },
            FallingDamageData = new FallingDamageData(
                HexDirection.Top,
                new HitLocationsData([], 5),
                new DiceResult(3), HitDirection.Front)
        };

        MockFallProcessor.ProcessMovementAttempt(unit1, Arg.Is<EnteringDeepWaterRollContext>(c => c.WaterDepth == 1), Game, MovementType.Walk, Arg.Any<HexPosition?>())
            .Returns(fallContextData);

        _sut.HandleCommand(new MoveUnitCommand
        {
            MovementType = MovementType.Walk,
            GameOriginId = Game.Id,
            PlayerId = activePlayer.Id,
            UnitId = _unit1Id,
            MovementPath =
            [
                new PathSegment(new HexPosition(1, 2, HexDirection.Top), new HexPosition(2, 2, HexDirection.Top), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 2 }]).ToData()
            ]
        });

        var unit2PositionBefore = unit2.Position;

        _sut.HandleCommand(new MoveUnitCommand
        {
            MovementType = MovementType.Walk,
            GameOriginId = Game.Id,
            PlayerId = activePlayer.Id,
            UnitId = unit2.Id,
            MovementPath =
            [
                new PathSegment(new HexPosition(5, 2, HexDirection.Top), new HexPosition(5, 1, HexDirection.Bottom), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }]).ToData()
            ]
        });

        unit2.Position.ShouldBe(unit2PositionBefore);
    }

    [Fact]
    public void HandleCommand_WhenDeferredWaterFall_CompletionMove_ShouldConsumeStepOnce()
    {
        SetMap();
        _sut.Enter();
        MockConsciousnessCalculator.MakeConsciousnessRolls(Arg.Any<IPilot>()).Returns([]);

        var activePlayer = Game.PhaseStepState!.Value.ActivePlayer;
        var unit = activePlayer.Units.Single(u => u.Id == _unit1Id) as Mech;
        unit!.Deploy(new HexPosition(1, 2, HexDirection.Top), null);

        var waterHex = Game.BattleMap!.GetHex(new HexCoordinates(2, 2));
        waterHex!.AddTerrain(new WaterTerrain(-1));

        var waterFallData = new FallContextData
        {
            UnitId = unit.Id,
            GameId = Game.Id,
            IsFalling = true,
            PilotingSkillRoll = new PilotingSkillRollData
            {
                RollContext = new EnteringDeepWaterRollContext(1),
                DiceResults = [2, 2],
                IsSuccessful = false,
                PsrBreakdown = new PsrBreakdown { BasePilotingSkill = 4, Modifiers = [] }
            },
            FallingDamageData = new FallingDamageData(
                HexDirection.Top,
                new HitLocationsData([], 5),
                new DiceResult(3), HitDirection.Front)
        };

        MockFallProcessor.ProcessMovementAttempt(
                unit,
                Arg.Is<EnteringDeepWaterRollContext>(c => c.WaterDepth == 1),
                Game,
                MovementType.Walk, Arg.Any<HexPosition?>())
            .Returns(waterFallData);

        var psrBreakdown = new PsrBreakdown { BasePilotingSkill = 4, Modifiers = [] };
        var successfulPsrData = new PilotingSkillRollData
        {
            RollContext = new PilotingSkillRollContext(PilotingSkillRollType.StandupAttempt),
            DiceResults = [6, 6],
            IsSuccessful = true,
            PsrBreakdown = psrBreakdown
        };
        var successfulStandupData = new FallContextData
        {
            UnitId = unit.Id,
            GameId = Game.Id,
            IsFalling = false,
            PilotingSkillRoll = successfulPsrData,
            LevelsFallen = 0,
            WasJumping = false
        };
        MockFallProcessor.ProcessMovementAttempt(
                unit,
                Arg.Is<PilotingSkillRollContext>(c => c.RollType == PilotingSkillRollType.StandupAttempt),
                Game,
                MovementType.StandingStill)
            .Returns(successfulStandupData);

        _sut.HandleCommand(new MoveUnitCommand
        {
            MovementType = MovementType.Walk,
            GameOriginId = Game.Id,
            PlayerId = activePlayer.Id,
            UnitId = _unit1Id,
            MovementPath =
            [
                new PathSegment(new HexPosition(1, 2, HexDirection.Top), new HexPosition(2, 2, HexDirection.Top), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 2 }]).ToData()
            ]
        });

        _sut.HandleCommand(new TryStandupCommand
        {
            GameOriginId = Game.Id,
            PlayerId = activePlayer.Id,
            UnitId = _unit1Id,
            Timestamp = DateTime.UtcNow,
            NewFacing = HexDirection.Top,
            MovementTypeAfterStandup = MovementType.Walk
        });

        CommandPublisher.ClearReceivedCalls();

        _sut.HandleCommand(new MoveUnitCommand
        {
            MovementType = MovementType.Walk,
            GameOriginId = Game.Id,
            PlayerId = activePlayer.Id,
            UnitId = _unit1Id,
            MovementPath =
            [
                new PathSegment(new HexPosition(2, 2, HexDirection.Top), new HexPosition(3, 2, HexDirection.Top), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }]).ToData()
            ]
        });

        CommandPublisher.Received(1).PublishCommand(Arg.Any<ChangeActivePlayerCommand>());
        CommandPublisher.Received(1).PublishCommand(Arg.Is<MoveUnitCommand>(cmd => cmd.UnitId == _unit1Id && cmd.IsCompleted));
    }

    [Fact]
    public void ProcessMoveCommand_WhenWaterFallAndStandupAndContinue_ShouldPreserveCombinedPath()
    {
        // Arrange
        SetMap();
        _sut.Enter();
        MockConsciousnessCalculator.MakeConsciousnessRolls(Arg.Any<IPilot>()).Returns([]);

        var unit = Game.PhaseStepState!.Value.ActivePlayer.Units.Single(u => u.Id == _unit1Id) as Mech;
        unit!.Deploy(new HexPosition(1, 2, HexDirection.Top), null);

        var waterHex = Game.BattleMap!.GetHex(new HexCoordinates(2, 2));
        waterHex!.AddTerrain(new WaterTerrain(-1));

        // Setup water fall
        var waterFallData = new FallContextData
        {
            UnitId = unit.Id,
            GameId = Game.Id,
            IsFalling = true,
            PilotingSkillRoll = new PilotingSkillRollData
            {
                RollContext = new EnteringDeepWaterRollContext(1),
                DiceResults = [2, 2],
                IsSuccessful = false,
                PsrBreakdown = new PsrBreakdown { BasePilotingSkill = 4, Modifiers = [] }
            },
            FallingDamageData = new FallingDamageData(
                HexDirection.Top,
                new HitLocationsData([], 5),
                new DiceResult(3), HitDirection.Front)
        };

        MockFallProcessor.ProcessMovementAttempt(
                unit,
                Arg.Is<EnteringDeepWaterRollContext>(c => c.WaterDepth == 1),
                Game,
                MovementType.Walk, Arg.Any<HexPosition?>())
            .Returns(waterFallData);

        // Setup successful standup
        var successfulPsrData = new PilotingSkillRollData
        {
            RollContext = new PilotingSkillRollContext(PilotingSkillRollType.StandupAttempt),
            DiceResults = [5, 5],
            IsSuccessful = true,
            PsrBreakdown = new PsrBreakdown { BasePilotingSkill = 4, Modifiers = [] }
        };
        var successfulStandupData = new FallContextData
        {
            UnitId = unit.Id,
            GameId = Game.Id,
            IsFalling = false,
            PilotingSkillRoll = successfulPsrData,
            LevelsFallen = 0,
            WasJumping = false
        };
        MockFallProcessor.ProcessMovementAttempt(
                unit,
                Arg.Is<PilotingSkillRollContext>(c => c.RollType == PilotingSkillRollType.StandupAttempt),
                Game,
                MovementType.StandingStill)
            .Returns(successfulStandupData);

        // Act - Step 1: First move into water (triggers fall + truncation)
        _sut.HandleCommand(new MoveUnitCommand
        {
            MovementType = MovementType.Walk,
            GameOriginId = Game.Id,
            PlayerId = _player1Id,
            UnitId = _unit1Id,
            MovementPath =
            [
                new PathSegment(new HexPosition(1, 2, HexDirection.Top), new HexPosition(2, 2, HexDirection.Top), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 2 }]).ToData()
            ]
        });
        unit.Position!.Coordinates.ShouldBe(new HexCoordinates(2, 2));

        // Step 2: Successful standup
        CommandPublisher.ClearReceivedCalls();
        _sut.HandleCommand(new TryStandupCommand
        {
            GameOriginId = Game.Id,
            PlayerId = _player1Id,
            UnitId = _unit1Id,
            Timestamp = DateTime.UtcNow,
            NewFacing = HexDirection.Top,
            MovementTypeAfterStandup = MovementType.Walk
        });

        // Step 3: Continue movement to a new hex
        _sut.HandleCommand(new MoveUnitCommand
        {
            MovementType = MovementType.Walk,
            GameOriginId = Game.Id,
            PlayerId = _player1Id,
            UnitId = _unit1Id,
            MovementPath =
            [
                new PathSegment(new HexPosition(2, 2, HexDirection.Top), new HexPosition(3, 2, HexDirection.Top), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }]).ToData()
            ]
        });

        // Assert - combined path preserves both segments
        unit.MovementTaken.ShouldNotBeNull();
        unit.MovementTaken.Segments.Count.ShouldBe(2, "Combined path should contain segments from both movement legs");
        unit.MovementTaken.Segments[0].To.Coordinates.ShouldBe(new HexCoordinates(2, 2), "First segment should end at water hex");
        unit.MovementTaken.Segments[1].To.Coordinates.ShouldBe(new HexCoordinates(3, 2), "Second segment should end at final hex");
        unit.MovementPointsSpent.ShouldBe(5, "Combined cost of both legs plus standup");
        unit.MovementTaken.HexesTraveled.ShouldBe(2, "Both legs contributed hexes traveled");
        unit.Position!.Coordinates.ShouldBe(new HexCoordinates(3, 2), "Unit should end at final destination");
    }

    [Fact]
    public void ProcessMoveCommand_WhenRunIntoWaterFallAndStandup_RemainingRunMpReducedByFullStandupCost()
    {
        // Reproduces the ticket scenario: the mech runs into depth-1 water, falls, and then stands up.
        // After a 4 MP run, only 1 walk MP remains, but 4 run MP are still available. The standup cost
        // (2 MP) must still be fully deducted so that remaining run MP = runMp - preFallCost - standupCost.
        // Arrange
        SetMap();
        _sut.Enter();
        MockConsciousnessCalculator.MakeConsciousnessRolls(Arg.Any<IPilot>()).Returns([]);

        var unit = Game.PhaseStepState!.Value.ActivePlayer.Units.Single(u => u.Id == _unit1Id) as Mech;
        unit!.Deploy(new HexPosition(1, 2, HexDirection.Top), null);

        var runMp = unit.GetMovementPoints(MovementType.Run);

        var waterHex = Game.BattleMap!.GetHex(new HexCoordinates(3, 2));
        waterHex!.AddTerrain(new WaterTerrain(-1));

        // Setup water fall
        var waterFallData = new FallContextData
        {
            UnitId = unit.Id,
            GameId = Game.Id,
            IsFalling = true,
            PilotingSkillRoll = new PilotingSkillRollData
            {
                RollContext = new EnteringDeepWaterRollContext(1),
                DiceResults = [2, 2],
                IsSuccessful = false,
                PsrBreakdown = new PsrBreakdown { BasePilotingSkill = 4, Modifiers = [] }
            },
            FallingDamageData = new FallingDamageData(
                HexDirection.Top,
                new HitLocationsData([], 5),
                new DiceResult(3), HitDirection.Front)
        };

        MockFallProcessor.ProcessMovementAttempt(
                unit,
                Arg.Is<EnteringDeepWaterRollContext>(c => c.WaterDepth == 1),
                Game,
                MovementType.Run, Arg.Any<HexPosition?>())
            .Returns(waterFallData);

        // Setup successful standup
        var successfulPsrData = new PilotingSkillRollData
        {
            RollContext = new PilotingSkillRollContext(PilotingSkillRollType.StandupAttempt),
            DiceResults = [5, 5],
            IsSuccessful = true,
            PsrBreakdown = new PsrBreakdown { BasePilotingSkill = 4, Modifiers = [] }
        };
        var successfulStandupData = new FallContextData
        {
            UnitId = unit.Id,
            GameId = Game.Id,
            IsFalling = false,
            PilotingSkillRoll = successfulPsrData,
            LevelsFallen = 0,
            WasJumping = false
        };
        MockFallProcessor.ProcessMovementAttempt(
                unit,
                Arg.Is<PilotingSkillRollContext>(c => c.RollType == PilotingSkillRollType.StandupAttempt),
                Game,
                MovementType.StandingStill)
            .Returns(successfulStandupData);

        // Act - Step 1: Run into water (triggers fall + truncation after 2 hexes, cost 4)
        _sut.HandleCommand(new MoveUnitCommand
        {
            MovementType = MovementType.Run,
            GameOriginId = Game.Id,
            PlayerId = _player1Id,
            UnitId = _unit1Id,
            MovementPath =
            [
                new PathSegment(new HexPosition(1, 2, HexDirection.Top), new HexPosition(2, 2, HexDirection.Top),
                    [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 2 }]).ToData(),
                new PathSegment(new HexPosition(2, 2, HexDirection.Top), new HexPosition(3, 2, HexDirection.Top),
                    [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 2 }]).ToData(),
                new PathSegment(new HexPosition(3, 2, HexDirection.Top), new HexPosition(4, 2, HexDirection.Top),
                    [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }]).ToData()
            ]
        });

        unit.MovementPointsSpent.ShouldBe(4, "Expected cost: 4 (two hexes run into the water)");
        unit.GetMovementPoints(MovementType.Walk).ShouldBe(1,
            "Only 1 walk MP remains after running, so the standup cost must not be capped by walk MP");
        unit.IsProne.ShouldBeTrue("Unit should be prone after falling into the water");

        // Act - Step 2: Successful standup, then continue with Run
        var standupCommand = new TryStandupCommand
        {
            GameOriginId = Game.Id,
            PlayerId = _player1Id,
            UnitId = _unit1Id,
            NewFacing = HexDirection.Bottom,
            MovementTypeAfterStandup = MovementType.Run
        };
        _sut.HandleCommand(standupCommand);

        // Assert - remaining run MP is reduced by both the pre-fall move cost and the full standup cost
        unit.IsProne.ShouldBeFalse("Standup should succeed");
        unit.MovementPointsSpent.ShouldBe(6, "Combined cost: 4 (run into water) + 2 (standup)");
        unit.MovementTaken!.TotalCost.ShouldBe(6, "MovementTaken should include both the run cost and the standup cost");
        unit.GetMovementPoints(MovementType.Run).ShouldBe(
            runMp - 4 - 2, "Remaining run MP must equal runMp - preFallCost - standupCost");
    }

    [Fact]
    public void ProcessMoveCommand_ShouldProcessJumpWaterEntry()
    {
        // Arrange
        SetMap();
        _sut.Enter();
        var unit = Game.PhaseStepState!.Value.ActivePlayer.Units.Single(u => u.Id == _unit1Id) as Mech;
        unit!.Deploy(new HexPosition(1, 2, HexDirection.Top), null);

        var hex = Game.BattleMap!.GetHex(new HexCoordinates(3, 2));
        hex!.AddTerrain(new WaterTerrain(-1));

        var moveCommand = new MoveUnitCommand
        {
            MovementType = MovementType.Jump,
            GameOriginId = Game.Id,
            PlayerId = Game.PhaseStepState!.Value.ActivePlayer.Id,
            UnitId = _unit1Id,
            MovementPath = [
                new PathSegment(new HexPosition(1, 2, HexDirection.Top), new HexPosition(3, 2, HexDirection.Top), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 2 }]).ToData()
            ]
        };

        var fallContextData = new FallContextData
        {
            UnitId = unit.Id,
            GameId = Game.Id,
            IsFalling = true,
            PilotingSkillRoll = new PilotingSkillRollData
            {
                RollContext = new EnteringDeepWaterRollContext(1),
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

        MockFallProcessor.ProcessMovementAttempt(unit, Arg.Is<EnteringDeepWaterRollContext>(c => c.WaterDepth == 1), Game, MovementType.Jump, Arg.Any<HexPosition?>()).Returns(fallContextData);

        CommandPublisher.ClearReceivedCalls();

        // Act
        _sut.HandleCommand(moveCommand);

        // Assert
        CommandPublisher.Received().PublishCommand(Arg.Is<MoveUnitCommand>(cmd => cmd.UnitId == _unit1Id && cmd.MovementPath.Count == 1 && cmd.IsCompleted));
        CommandPublisher.Received().PublishCommand(Arg.Is<MechFallCommand>(cmd => cmd.UnitId == _unit1Id));
    }

    [Fact]
    public void ProcessMoveCommand_ShouldPublishPsrCommand_WhenWaterEntrySucceeds_DuringWalk()
    {
        // Arrange
        SetMap();
        _sut.Enter();
        var unit = Game.PhaseStepState!.Value.ActivePlayer.Units.Single(u => u.Id == _unit1Id) as Mech;
        unit!.Deploy(new HexPosition(1, 2, HexDirection.Top), null);

        var hex = Game.BattleMap!.GetHex(new HexCoordinates(2, 2));
        hex!.AddTerrain(new WaterTerrain(-1));

        var moveCommand = new MoveUnitCommand
        {
            MovementType = MovementType.Walk,
            GameOriginId = Game.Id,
            PlayerId = Game.PhaseStepState!.Value.ActivePlayer.Id,
            UnitId = _unit1Id,
            MovementPath = [
                new PathSegment(new HexPosition(1, 2, HexDirection.Top), new HexPosition(2, 2, HexDirection.Top), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 2 }]).ToData()
            ]
        };

        var fallContextData = new FallContextData
        {
            UnitId = unit.Id,
            GameId = Game.Id,
            IsFalling = false,
            PilotingSkillRoll = new PilotingSkillRollData
            {
                RollContext = new EnteringDeepWaterRollContext(1),
                DiceResults = [6, 6],
                IsSuccessful = true,
                PsrBreakdown = new PsrBreakdown { BasePilotingSkill = 4, Modifiers = [] }
            }
        };

        MockFallProcessor.ProcessMovementAttempt(unit, Arg.Is<EnteringDeepWaterRollContext>(c => c.WaterDepth == 1), Game, MovementType.Walk, Arg.Any<HexPosition?>()).Returns(fallContextData);

        CommandPublisher.ClearReceivedCalls();

        // Act
        _sut.HandleCommand(moveCommand);

        // Assert
        CommandPublisher.Received().PublishCommand(Arg.Is<MoveUnitCommand>(cmd => cmd.UnitId == _unit1Id && cmd.MovementPath.Count == 1 && cmd.IsCompleted));
        CommandPublisher.Received().PublishCommand(Arg.Is<MechFallCommand>(cmd => 
            cmd.UnitId == _unit1Id && cmd.DamageData == null && cmd.FallPilotingSkillRoll!.IsSuccessful));
    }

    [Fact]
    public void ProcessMoveCommand_ShouldPublishPsrCommand_WhenWaterEntrySucceeds_DuringJump()
    {
        // Arrange
        SetMap();
        _sut.Enter();
        var unit = Game.PhaseStepState!.Value.ActivePlayer.Units.Single(u => u.Id == _unit1Id) as Mech;
        unit!.Deploy(new HexPosition(1, 2, HexDirection.Top), null);

        var hex = Game.BattleMap!.GetHex(new HexCoordinates(3, 2));
        hex!.AddTerrain(new WaterTerrain(-1));

        var moveCommand = new MoveUnitCommand
        {
            MovementType = MovementType.Jump,
            GameOriginId = Game.Id,
            PlayerId = Game.PhaseStepState!.Value.ActivePlayer.Id,
            UnitId = _unit1Id,
            MovementPath = [
                new PathSegment(new HexPosition(1, 2, HexDirection.Top), new HexPosition(3, 2, HexDirection.Top), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 2 }]).ToData()
            ]
        };

        var fallContextData = new FallContextData
        {
            UnitId = unit.Id,
            GameId = Game.Id,
            IsFalling = false,
            PilotingSkillRoll = new PilotingSkillRollData
            {
                RollContext = new EnteringDeepWaterRollContext(1),
                DiceResults = [6, 6],
                IsSuccessful = true,
                PsrBreakdown = new PsrBreakdown { BasePilotingSkill = 4, Modifiers = [] }
            }
        };

        MockFallProcessor.ProcessMovementAttempt(unit, Arg.Is<EnteringDeepWaterRollContext>(c => c.WaterDepth == 1), Game, MovementType.Jump, Arg.Any<HexPosition?>()).Returns(fallContextData);

        CommandPublisher.ClearReceivedCalls();

        // Act
        _sut.HandleCommand(moveCommand);

        // Assert
        CommandPublisher.Received().PublishCommand(Arg.Is<MoveUnitCommand>(cmd => cmd.UnitId == _unit1Id && cmd.MovementPath.Count == 1 && cmd.IsCompleted));
        CommandPublisher.Received().PublishCommand(Arg.Is<MechFallCommand>(cmd => 
            cmd.UnitId == _unit1Id && cmd.DamageData == null && cmd.FallPilotingSkillRoll!.IsSuccessful));
    }

    [Fact]
    public void HandleCommand_WhenUnitAlreadyMoved_ShouldIgnoreMoveCommand()
    {
        // Arrange
        _sut.Enter();
        var unit = Game.PhaseStepState!.Value.ActivePlayer.Units.Single(u => u.Id == _unit1Id) as Mech;
        unit!.Deploy(new HexPosition(1, 2, HexDirection.Top), null);

        // First move completes normally
        _sut.HandleCommand(new MoveUnitCommand
        {
            MovementType = MovementType.Walk,
            GameOriginId = Game.Id,
            PlayerId = Game.PhaseStepState!.Value.ActivePlayer.Id,
            UnitId = _unit1Id,
            IsCompleted = true,
            MovementPath =
            [
                new PathSegment(new HexPosition(1, 2, HexDirection.Top), new HexPosition(3, 1, HexDirection.Bottom), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }])
                    .ToData()
            ]
        });
        unit.HasMoved.ShouldBeTrue();

        CommandPublisher.ClearReceivedCalls();

        // Act - send another move command for the same unit
        _sut.HandleCommand(new MoveUnitCommand
        {
            MovementType = MovementType.Walk,
            GameOriginId = Game.Id,
            PlayerId = Game.PhaseStepState!.Value.ActivePlayer.Id,
            UnitId = _unit1Id,
            IsCompleted = true,
            MovementPath =
            [
                new PathSegment(new HexPosition(3, 1, HexDirection.Bottom), new HexPosition(4, 1, HexDirection.Bottom), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }])
                    .ToData()
            ]
        });

        // Assert - no additional move command should be published, unit position unchanged
        CommandPublisher.DidNotReceive().PublishCommand(Arg.Is<MoveUnitCommand>(cmd =>
            cmd.UnitId == _unit1Id && cmd.MovementPath[0].To.Coordinates.Q == 4));
        CommandPublisher.Received(1).PublishCommand(Arg.Is<ErrorCommand>(cmd =>
            cmd.ErrorCode == ErrorCode.InvalidGameState));
        unit.Position!.Coordinates.ShouldBe(new HexCoordinates(3, 1));
    }

    [Fact]
    public void HandleCommand_WhenUnitAlreadyMoved_ShouldIgnoreStandupCommand()
    {
        // Arrange
        _sut.Enter();
        var unit = Game.PhaseStepState!.Value.ActivePlayer.Units.Single(u => u.Id == _unit1Id) as Mech;
        unit!.Deploy(new HexPosition(1, 2, HexDirection.Top), null);

        // Complete movement first
        _sut.HandleCommand(new MoveUnitCommand
        {
            MovementType = MovementType.Walk,
            GameOriginId = Game.Id,
            PlayerId = Game.PhaseStepState!.Value.ActivePlayer.Id,
            UnitId = _unit1Id,
            IsCompleted = true,
            MovementPath =
            [
                new PathSegment(new HexPosition(1, 2, HexDirection.Top), new HexPosition(3, 1, HexDirection.Bottom), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }])
                    .ToData()
            ]
        });
        unit.HasMoved.ShouldBeTrue();

        CommandPublisher.ClearReceivedCalls();

        // Act - try standup after movement
        _sut.HandleCommand(new TryStandupCommand
        {
            GameOriginId = Game.Id,
            PlayerId = Game.PhaseStepState!.Value.ActivePlayer.Id,
            UnitId = _unit1Id,
            Timestamp = DateTime.UtcNow
        });

        // Assert - no standup or fall commands should be published
        CommandPublisher.DidNotReceive().PublishCommand(Arg.Any<MechStandUpCommand>());
        CommandPublisher.DidNotReceive().PublishCommand(Arg.Any<MechFallCommand>());
        CommandPublisher.Received(1).PublishCommand(Arg.Is<ErrorCommand>(cmd =>
            cmd.ErrorCode == ErrorCode.InvalidGameState));
        unit.StandupAttempts.ShouldBe(0);
    }

    [Fact]
    public void Exit_ShouldClearDeferralState()
    {
        SetMap();
        _sut.Enter();
        MockConsciousnessCalculator.MakeConsciousnessRolls(Arg.Any<IPilot>()).Returns([]);

        var activePlayer = Game.PhaseStepState!.Value.ActivePlayer;
        var unit1 = activePlayer.Units.Single(u => u.Id == _unit1Id) as Mech;
        var unit2 = activePlayer.Units.First(u => u.Id != _unit1Id) as Mech;
        unit1!.Deploy(new HexPosition(1, 2, HexDirection.Top), null);
        unit2!.Deploy(new HexPosition(5, 2, HexDirection.Top), null);

        var waterHex = Game.BattleMap!.GetHex(new HexCoordinates(2, 2));
        waterHex!.AddTerrain(new WaterTerrain(-1));

        var fallContextData = new FallContextData
        {
            UnitId = unit1.Id,
            GameId = Game.Id,
            IsFalling = true,
            PilotingSkillRoll = new PilotingSkillRollData
            {
                RollContext = new EnteringDeepWaterRollContext(1),
                DiceResults = [2, 2],
                IsSuccessful = false,
                PsrBreakdown = new PsrBreakdown { BasePilotingSkill = 4, Modifiers = [] }
            },
            FallingDamageData = new FallingDamageData(
                HexDirection.Top,
                new HitLocationsData([], 5),
                new DiceResult(3), HitDirection.Front)
        };

        MockFallProcessor.ProcessMovementAttempt(unit1, Arg.Is<EnteringDeepWaterRollContext>(c => c.WaterDepth == 1), Game, MovementType.Walk, Arg.Any<HexPosition?>())
            .Returns(fallContextData);

        _sut.HandleCommand(new MoveUnitCommand
        {
            MovementType = MovementType.Walk,
            GameOriginId = Game.Id,
            PlayerId = activePlayer.Id,
            UnitId = _unit1Id,
            MovementPath =
            [
                new PathSegment(new HexPosition(1, 2, HexDirection.Top), new HexPosition(2, 2, HexDirection.Top), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 2 }]).ToData()
            ]
        });

        _sut.Exit();

        var unit2PositionBefore = unit2.Position;
        var newPosition = new HexPosition(5, 1, HexDirection.Bottom);

        _sut.HandleCommand(new MoveUnitCommand
        {
            MovementType = MovementType.Walk,
            GameOriginId = Game.Id,
            PlayerId = activePlayer.Id,
            UnitId = unit2.Id,
            MovementPath =
            [
                new PathSegment(new HexPosition(5, 2, HexDirection.Top), newPosition, [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }]).ToData()
            ]
        });

        unit2.Position.ShouldNotBe(unit2PositionBefore);
        unit2.Position!.Coordinates.ShouldBe(newPosition.Coordinates);
        CommandPublisher.Received().PublishCommand(Arg.Is<MoveUnitCommand>(cmd => cmd.UnitId == unit2.Id && cmd.IsCompleted));
    }

    [Fact]
    public void ProcessMoveCommand_WhenRunSkidOnRoad_ShouldPublishFallWithSkidPath()
    {
        SetMap();
        _sut.Enter();
        var unit = Game.PhaseStepState!.Value.ActivePlayer.Units.Single(u => u.Id == _unit1Id) as Mech;
        unit!.Deploy(new HexPosition(1, 2, HexDirection.Top), null);

        var roadHex = Game.BattleMap!.GetHex(new HexCoordinates(2, 2));
        roadHex!.AddTerrain(new RoadTerrain());

        var moveCommand = new MoveUnitCommand
        {
            MovementType = MovementType.Run,
            GameOriginId = Game.Id,
            PlayerId = Game.PhaseStepState!.Value.ActivePlayer.Id,
            UnitId = _unit1Id,
            MovementPath =
            [
                new PathSegment(new HexPosition(1, 2, HexDirection.Top), new HexPosition(2, 2, HexDirection.Top), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }]).ToData(),
                new PathSegment(new HexPosition(2, 2, HexDirection.Top), new HexPosition(2, 2, HexDirection.Bottom), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 0 }]).ToData(),
                new PathSegment(new HexPosition(2, 2, HexDirection.Bottom), new HexPosition(3, 2, HexDirection.Bottom), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }]).ToData()
            ]
        };

        var fallContextData = new FallContextData
        {
            UnitId = unit.Id,
            GameId = Game.Id,
            IsFalling = true,
            PilotingSkillRoll = new PilotingSkillRollData
            {
                RollContext = new SkidCheckRollContext(1, 3),
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

        MockFallProcessor.ProcessMovementAttempt(unit, Arg.Is<SkidCheckRollContext>(c => c.SkidDistance == 1 && c.HexesMoved == 1), Game, MovementType.Run, Arg.Any<HexPosition?>()).Returns(fallContextData);

        CommandPublisher.ClearReceivedCalls();

        _sut.HandleCommand(moveCommand);

        CommandPublisher.Received().PublishCommand(Arg.Is<MoveUnitCommand>(cmd =>
            cmd.UnitId == _unit1Id && cmd.IsCompleted && cmd.MovementPath.Count == 3));
        CommandPublisher.Received().PublishCommand(Arg.Is<MechFallCommand>(cmd => cmd.UnitId == _unit1Id));

        unit.Position!.Coordinates.ShouldBe(new HexCoordinates(2, 1));
    }

    [Fact]
    public void ProcessMoveCommand_WhenSkidFall_AndCanStandup_ShouldNotDeferStep()
    {
        SetMap();
        _sut.Enter();
        MockConsciousnessCalculator.MakeConsciousnessRolls(Arg.Any<IPilot>()).Returns([]);

        var unit = Game.PhaseStepState!.Value.ActivePlayer.Units.Single(u => u.Id == _unit1Id) as Mech;
        unit!.Deploy(new HexPosition(1, 2, HexDirection.Top), null);

        var roadHex = Game.BattleMap!.GetHex(new HexCoordinates(2, 2));
        roadHex!.AddTerrain(new RoadTerrain());

        var moveCommand = new MoveUnitCommand
        {
            MovementType = MovementType.Run,
            GameOriginId = Game.Id,
            PlayerId = Game.PhaseStepState!.Value.ActivePlayer.Id,
            UnitId = _unit1Id,
            MovementPath =
            [
                new PathSegment(new HexPosition(1, 2, HexDirection.Top), new HexPosition(2, 2, HexDirection.Top), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }]).ToData(),
                new PathSegment(new HexPosition(2, 2, HexDirection.Top), new HexPosition(2, 2, HexDirection.Bottom), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 0 }]).ToData(),
                new PathSegment(new HexPosition(2, 2, HexDirection.Bottom), new HexPosition(3, 2, HexDirection.Bottom), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }]).ToData()
            ]
        };

        var fallingDamageData = new FallingDamageData(
            HexDirection.Top,
            new HitLocationsData([], 5),
            new DiceResult(3), HitDirection.Front
        );
        var fallContextData = new FallContextData
        {
            UnitId = unit.Id,
            GameId = Game.Id,
            IsFalling = true,
            PilotingSkillRoll = new PilotingSkillRollData
            {
                RollContext = new SkidCheckRollContext(1, 3),
                DiceResults = [2, 2],
                IsSuccessful = false,
                PsrBreakdown = new PsrBreakdown { BasePilotingSkill = 4, Modifiers = [] }
            },
            FallingDamageData = fallingDamageData,
            // A skid command is only emitted when the fall carries skid damage. Without it the
            // ApplySkidAction is never created and the deferral rule is not exercised.
            SkidDamageData = fallingDamageData
        };

        MockFallProcessor.ProcessMovementAttempt(unit, Arg.Is<SkidCheckRollContext>(c => c.SkidDistance == 1 && c.HexesMoved == 1), Game, MovementType.Run, Arg.Any<HexPosition?>()).Returns(fallContextData);

        CommandPublisher.ClearReceivedCalls();

        // Act
        _sut.HandleCommand(moveCommand);

        // Assert - ApplySkidAction forces deferAfterFall = false, so the turn is finalized
        // instead of waiting for a stand-up move
        unit.HasMoved.ShouldBeTrue();
        unit.Position!.Coordinates.ShouldBe(new HexCoordinates(2, 1));
        CommandPublisher.Received().PublishCommand(Arg.Is<MoveUnitCommand>(cmd =>
            cmd.UnitId == _unit1Id && cmd.IsCompleted && cmd.MovementPath.Count == 3));
        CommandPublisher.Received().PublishCommand(Arg.Is<MechFallCommand>(cmd => cmd.UnitId == _unit1Id));
        CommandPublisher.Received().PublishCommand(Arg.Is<MechSkidCommand>(cmd => cmd.UnitId == _unit1Id));
        CommandPublisher.Received().PublishCommand(Arg.Any<ChangeActivePlayerCommand>());
    }

    [Fact]
    public void ProcessMoveCommand_WhenRunSkidOnRoadPasses_ShouldPublishPsrResult()
    {
        SetMap();
        _sut.Enter();
        var unit = Game.PhaseStepState!.Value.ActivePlayer.Units.Single(u => u.Id == _unit1Id) as Mech;
        unit!.Deploy(new HexPosition(1, 2, HexDirection.Top), null);

        var roadHex = Game.BattleMap!.GetHex(new HexCoordinates(2, 2));
        roadHex!.AddTerrain(new RoadTerrain());

        var moveCommand = new MoveUnitCommand
        {
            MovementType = MovementType.Run,
            GameOriginId = Game.Id,
            PlayerId = Game.PhaseStepState!.Value.ActivePlayer.Id,
            UnitId = _unit1Id,
            MovementPath =
            [
                new PathSegment(new HexPosition(1, 2, HexDirection.Top), new HexPosition(2, 2, HexDirection.Top), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }]).ToData(),
                new PathSegment(new HexPosition(2, 2, HexDirection.Top), new HexPosition(2, 2, HexDirection.Bottom), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 0 }]).ToData(),
                new PathSegment(new HexPosition(2, 2, HexDirection.Bottom), new HexPosition(3, 2, HexDirection.Bottom), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }]).ToData()
            ]
        };

        var successContext = new FallContextData
        {
            UnitId = unit.Id,
            GameId = Game.Id,
            IsFalling = false,
            PilotingSkillRoll = new PilotingSkillRollData
            {
                RollContext = new SkidCheckRollContext(1, 3),
                DiceResults = [6, 6],
                IsSuccessful = true,
                PsrBreakdown = new PsrBreakdown { BasePilotingSkill = 4, Modifiers = [] }
            }
        };

        MockFallProcessor.ProcessMovementAttempt(unit, Arg.Is<SkidCheckRollContext>(c => c.SkidDistance == 1 && c.HexesMoved == 1), Game, MovementType.Run, Arg.Any<HexPosition?>()).Returns(successContext);

        CommandPublisher.ClearReceivedCalls();

        _sut.HandleCommand(moveCommand);

        CommandPublisher.Received().PublishCommand(Arg.Is<MoveUnitCommand>(cmd =>
            cmd.UnitId == _unit1Id && cmd.IsCompleted && cmd.MovementPath.Count == 3));
        CommandPublisher.Received().PublishCommand(Arg.Is<MechFallCommand>(cmd =>
            cmd.UnitId == _unit1Id && cmd.DamageData == null && cmd.FallPilotingSkillRoll!.IsSuccessful));
    }

    [Fact]
    public void ProcessMoveCommand_WhenWalkOnRoadTurn_ShouldNotTriggerSkid()
    {
        SetMap();
        _sut.Enter();
        var unit = Game.PhaseStepState!.Value.ActivePlayer.Units.Single(u => u.Id == _unit1Id) as Mech;
        unit!.Deploy(new HexPosition(1, 2, HexDirection.Top), null);

        var roadHex = Game.BattleMap!.GetHex(new HexCoordinates(2, 2));
        roadHex!.AddTerrain(new RoadTerrain());

        var moveCommand = new MoveUnitCommand
        {
            MovementType = MovementType.Walk,
            GameOriginId = Game.Id,
            PlayerId = Game.PhaseStepState!.Value.ActivePlayer.Id,
            UnitId = _unit1Id,
            MovementPath =
            [
                new PathSegment(new HexPosition(1, 2, HexDirection.Top), new HexPosition(2, 2, HexDirection.Top), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }]).ToData(),
                new PathSegment(new HexPosition(2, 2, HexDirection.Top), new HexPosition(2, 2, HexDirection.Bottom), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 0 }]).ToData(),
                new PathSegment(new HexPosition(2, 2, HexDirection.Bottom), new HexPosition(3, 2, HexDirection.Bottom), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }]).ToData()
            ]
        };

        CommandPublisher.ClearReceivedCalls();

        _sut.HandleCommand(moveCommand);

        CommandPublisher.Received().PublishCommand(Arg.Is<MoveUnitCommand>(cmd =>
            cmd.UnitId == _unit1Id && cmd.IsCompleted && cmd.MovementPath.Count == 3));
        CommandPublisher.DidNotReceive().PublishCommand(Arg.Any<MechFallCommand>());
    }

    [Fact]
    public void ProcessMoveCommand_WhenRunOnClearTurn_ShouldNotTriggerSkid()
    {
        SetMap();
        _sut.Enter();
        var unit = Game.PhaseStepState!.Value.ActivePlayer.Units.Single(u => u.Id == _unit1Id) as Mech;
        unit!.Deploy(new HexPosition(1, 2, HexDirection.Top), null);

        var moveCommand = new MoveUnitCommand
        {
            MovementType = MovementType.Run,
            GameOriginId = Game.Id,
            PlayerId = Game.PhaseStepState!.Value.ActivePlayer.Id,
            UnitId = _unit1Id,
            MovementPath =
            [
                new PathSegment(new HexPosition(1, 2, HexDirection.Top), new HexPosition(2, 2, HexDirection.Top), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }]).ToData(),
                new PathSegment(new HexPosition(2, 2, HexDirection.Top), new HexPosition(2, 2, HexDirection.Bottom), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 0 }]).ToData(),
                new PathSegment(new HexPosition(2, 2, HexDirection.Bottom), new HexPosition(3, 2, HexDirection.Bottom), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }]).ToData()
            ]
        };

        CommandPublisher.ClearReceivedCalls();

        _sut.HandleCommand(moveCommand);

        CommandPublisher.Received().PublishCommand(Arg.Is<MoveUnitCommand>(cmd =>
            cmd.UnitId == _unit1Id && cmd.IsCompleted && cmd.MovementPath.Count == 3));
        CommandPublisher.DidNotReceive().PublishCommand(Arg.Any<MechFallCommand>());
    }

    [Fact]
    public void ProcessMoveCommand_WhenRunSkidOnPavement_ShouldTriggerSkid()
    {
        SetMap();
        _sut.Enter();
        var unit = Game.PhaseStepState!.Value.ActivePlayer.Units.Single(u => u.Id == _unit1Id) as Mech;
        unit!.Deploy(new HexPosition(1, 2, HexDirection.Top), null);

        var pavementHex = Game.BattleMap!.GetHex(new HexCoordinates(2, 2));
        pavementHex!.AddTerrain(new PavementTerrain());

        var moveCommand = new MoveUnitCommand
        {
            MovementType = MovementType.Run,
            GameOriginId = Game.Id,
            PlayerId = Game.PhaseStepState!.Value.ActivePlayer.Id,
            UnitId = _unit1Id,
            MovementPath =
            [
                new PathSegment(new HexPosition(1, 2, HexDirection.Top), new HexPosition(2, 2, HexDirection.Top), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }]).ToData(),
                new PathSegment(new HexPosition(2, 2, HexDirection.Top), new HexPosition(2, 2, HexDirection.Bottom), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 0 }]).ToData(),
                new PathSegment(new HexPosition(2, 2, HexDirection.Bottom), new HexPosition(3, 2, HexDirection.Bottom), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }]).ToData()
            ]
        };

        var fallContextData = new FallContextData
        {
            UnitId = unit.Id,
            GameId = Game.Id,
            IsFalling = true,
            PilotingSkillRoll = new PilotingSkillRollData
            {
                RollContext = new SkidCheckRollContext(1, 3),
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

        MockFallProcessor.ProcessMovementAttempt(unit, Arg.Is<SkidCheckRollContext>(c => c.SkidDistance == 1 && c.HexesMoved == 1), Game, MovementType.Run, Arg.Any<HexPosition?>()).Returns(fallContextData);

        CommandPublisher.ClearReceivedCalls();

        _sut.HandleCommand(moveCommand);

        CommandPublisher.Received().PublishCommand(Arg.Is<MoveUnitCommand>(cmd =>
            cmd.UnitId == _unit1Id && cmd.IsCompleted));
        CommandPublisher.Received().PublishCommand(Arg.Is<MechFallCommand>(cmd => cmd.UnitId == _unit1Id));
    }

    [Fact]
    public void ProcessMoveCommand_WhenRunSkidOnBridge_ShouldTriggerSkid()
    {
        SetMap();
        _sut.Enter();
        var unit = Game.PhaseStepState!.Value.ActivePlayer.Units.Single(u => u.Id == _unit1Id) as Mech;
        unit!.Deploy(new HexPosition(1, 2, HexDirection.Top), null);

        var bridgeHex = Game.BattleMap!.GetHex(new HexCoordinates(2, 2));
        bridgeHex!.AddTerrain(new BridgeTerrain(0, 100));

        var moveCommand = new MoveUnitCommand
        {
            MovementType = MovementType.Run,
            GameOriginId = Game.Id,
            PlayerId = Game.PhaseStepState!.Value.ActivePlayer.Id,
            UnitId = _unit1Id,
            MovementPath =
            [
                new PathSegment(new HexPosition(1, 2, HexDirection.Top), new HexPosition(2, 2, HexDirection.Top), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }]).ToData(),
                new PathSegment(new HexPosition(2, 2, HexDirection.Top), new HexPosition(2, 2, HexDirection.Bottom), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 0 }]).ToData(),
                new PathSegment(new HexPosition(2, 2, HexDirection.Bottom), new HexPosition(3, 2, HexDirection.Bottom), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }]).ToData()
            ]
        };

        var fallContextData = new FallContextData
        {
            UnitId = unit.Id,
            GameId = Game.Id,
            IsFalling = true,
            PilotingSkillRoll = new PilotingSkillRollData
            {
                RollContext = new SkidCheckRollContext(1, 3),
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

        MockFallProcessor.ProcessMovementAttempt(unit, Arg.Is<SkidCheckRollContext>(c => c.SkidDistance == 1 && c.HexesMoved == 1), Game, MovementType.Run, Arg.Any<HexPosition?>()).Returns(fallContextData);

        CommandPublisher.ClearReceivedCalls();

        _sut.HandleCommand(moveCommand);

        CommandPublisher.Received().PublishCommand(Arg.Is<MoveUnitCommand>(cmd =>
            cmd.UnitId == _unit1Id && cmd.IsCompleted));
        CommandPublisher.Received().PublishCommand(Arg.Is<MechFallCommand>(cmd => cmd.UnitId == _unit1Id));
    }

    [Fact]
    public void ProcessMoveCommand_WhenRunSkidAndTurnAtEnd_ShouldNotTriggerSkid()
    {
        SetMap();
        _sut.Enter();
        var unit = Game.PhaseStepState!.Value.ActivePlayer.Units.Single(u => u.Id == _unit1Id) as Mech;
        unit!.Deploy(new HexPosition(1, 2, HexDirection.Top), null);

        var roadHex = Game.BattleMap!.GetHex(new HexCoordinates(2, 2));
        roadHex!.AddTerrain(new RoadTerrain());

        var moveCommand = new MoveUnitCommand
        {
            MovementType = MovementType.Run,
            GameOriginId = Game.Id,
            PlayerId = Game.PhaseStepState!.Value.ActivePlayer.Id,
            UnitId = _unit1Id,
            MovementPath =
            [
                new PathSegment(new HexPosition(1, 2, HexDirection.Top), new HexPosition(2, 2, HexDirection.Top), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }]).ToData(),
                new PathSegment(new HexPosition(2, 2, HexDirection.Top), new HexPosition(2, 2, HexDirection.Bottom), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 0 }]).ToData()
            ]
        };

        CommandPublisher.ClearReceivedCalls();

        _sut.HandleCommand(moveCommand);

        CommandPublisher.Received().PublishCommand(Arg.Is<MoveUnitCommand>(cmd =>
            cmd.UnitId == _unit1Id && cmd.IsCompleted && cmd.MovementPath.Count == 2));
        CommandPublisher.DidNotReceive().PublishCommand(Arg.Any<MechFallCommand>());
    }

    [Fact]
    public void ProcessMoveCommand_WhenWaterEntryFall_ShouldNotProcessSkid()
    {
        SetMap();
        _sut.Enter();
        var unit = Game.PhaseStepState!.Value.ActivePlayer.Units.Single(u => u.Id == _unit1Id) as Mech;
        unit!.Deploy(new HexPosition(1, 2, HexDirection.Top), null);

        var waterHex = Game.BattleMap!.GetHex(new HexCoordinates(2, 2));
        waterHex!.AddTerrain(new WaterTerrain(-1));

        var roadHex = Game.BattleMap!.GetHex(new HexCoordinates(3, 2));
        roadHex!.AddTerrain(new RoadTerrain());

        var moveCommand = new MoveUnitCommand
        {
            MovementType = MovementType.Run,
            GameOriginId = Game.Id,
            PlayerId = Game.PhaseStepState!.Value.ActivePlayer.Id,
            UnitId = _unit1Id,
            MovementPath =
            [
                new PathSegment(new HexPosition(1, 2, HexDirection.Top), new HexPosition(2, 2, HexDirection.Top), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }]).ToData(),
                new PathSegment(new HexPosition(2, 2, HexDirection.Top), new HexPosition(3, 2, HexDirection.Top), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }]).ToData(),
                new PathSegment(new HexPosition(3, 2, HexDirection.Top), new HexPosition(3, 2, HexDirection.Bottom), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 0 }]).ToData(),
                new PathSegment(new HexPosition(3, 2, HexDirection.Bottom), new HexPosition(4, 2, HexDirection.Bottom), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }]).ToData()
            ]
        };

        var waterFallData = new FallContextData
        {
            UnitId = unit.Id,
            GameId = Game.Id,
            IsFalling = true,
            PilotingSkillRoll = new PilotingSkillRollData
            {
                RollContext = new EnteringDeepWaterRollContext(1),
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

        MockFallProcessor.ProcessMovementAttempt(unit, Arg.Is<EnteringDeepWaterRollContext>(c => c.WaterDepth == 1), Game, MovementType.Run, Arg.Any<HexPosition?>()).Returns(waterFallData);

        CommandPublisher.ClearReceivedCalls();

        _sut.HandleCommand(moveCommand);

        CommandPublisher.Received().PublishCommand(Arg.Is<MoveUnitCommand>(cmd =>
            cmd.UnitId == _unit1Id && !cmd.IsCompleted));
        CommandPublisher.Received(1).PublishCommand(Arg.Is<MechFallCommand>(cmd => cmd.UnitId == _unit1Id));
    }

    [Fact]
    public void ProcessMoveCommand_WhenWaterEntryPasses_ShouldProcessSkid()
    {
        SetMap();
        _sut.Enter();
        var unit = Game.PhaseStepState!.Value.ActivePlayer.Units.Single(u => u.Id == _unit1Id) as Mech;
        unit!.Deploy(new HexPosition(1, 2, HexDirection.Top), null);

        var waterHex = Game.BattleMap!.GetHex(new HexCoordinates(2, 2));
        waterHex!.AddTerrain(new WaterTerrain(-1));

        var roadHex = Game.BattleMap!.GetHex(new HexCoordinates(3, 2));
        roadHex!.AddTerrain(new RoadTerrain());

        var moveCommand = new MoveUnitCommand
        {
            MovementType = MovementType.Run,
            GameOriginId = Game.Id,
            PlayerId = Game.PhaseStepState!.Value.ActivePlayer.Id,
            UnitId = _unit1Id,
            MovementPath =
            [
                new PathSegment(new HexPosition(1, 2, HexDirection.Top), new HexPosition(2, 2, HexDirection.Top), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }]).ToData(),
                new PathSegment(new HexPosition(2, 2, HexDirection.Top), new HexPosition(3, 2, HexDirection.Top), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }]).ToData(),
                new PathSegment(new HexPosition(3, 2, HexDirection.Top), new HexPosition(3, 2, HexDirection.Bottom), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 0 }]).ToData(),
                new PathSegment(new HexPosition(3, 2, HexDirection.Bottom), new HexPosition(4, 2, HexDirection.Bottom), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }]).ToData()
            ]
        };

        var waterSuccessData = new FallContextData
        {
            UnitId = unit.Id,
            GameId = Game.Id,
            IsFalling = false,
            PilotingSkillRoll = new PilotingSkillRollData
            {
                RollContext = new EnteringDeepWaterRollContext(1),
                DiceResults = [6, 6],
                IsSuccessful = true,
                PsrBreakdown = new PsrBreakdown { BasePilotingSkill = 4, Modifiers = [] }
            }
        };

        var skidFallData = new FallContextData
        {
            UnitId = unit.Id,
            GameId = Game.Id,
            IsFalling = true,
            PilotingSkillRoll = new PilotingSkillRollData
            {
                RollContext = new SkidCheckRollContext(2, 3),
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

        MockFallProcessor.ProcessMovementAttempt(unit, Arg.Is<EnteringDeepWaterRollContext>(c => c.WaterDepth == 1), Game, MovementType.Run, Arg.Any<HexPosition?>()).Returns(waterSuccessData);
        MockFallProcessor.ProcessMovementAttempt(unit, Arg.Is<SkidCheckRollContext>(c => c.SkidDistance == 1 && c.HexesMoved == 2), Game, MovementType.Run, Arg.Any<HexPosition?>()).Returns(skidFallData);

        CommandPublisher.ClearReceivedCalls();

        _sut.HandleCommand(moveCommand);

        CommandPublisher.Received().PublishCommand(Arg.Is<MechFallCommand>(cmd => cmd.UnitId == _unit1Id));
        CommandPublisher.Received().PublishCommand(Arg.Is<MoveUnitCommand>(cmd =>
            cmd.UnitId == _unit1Id && cmd.IsCompleted));
    }

    [Fact]
    public void WhenSkidEntersLightWoods_ShouldConsumeExtraSkidDistance()
    {
        SetMap();
        _sut.Enter();
        var unit = Game.PhaseStepState!.Value.ActivePlayer.Units.Single(u => u.Id == _unit1Id) as Mech;
        unit!.Deploy(new HexPosition(1, 5, HexDirection.Top), null);

        var turnHex = Game.BattleMap!.GetHex(new HexCoordinates(8, 5));
        turnHex!.AddTerrain(new PavementTerrain());

        var lightWoodsHex = Game.BattleMap!.GetHex(new HexCoordinates(8, 3));
        lightWoodsHex!.AddTerrain(new LightWoodsTerrain());

        var moveCommand = new MoveUnitCommand
        {
            MovementType = MovementType.Run,
            GameOriginId = Game.Id,
            PlayerId = Game.PhaseStepState!.Value.ActivePlayer.Id,
            UnitId = _unit1Id,
            MovementPath =
            [
                new PathSegment(new HexPosition(1, 5, HexDirection.Top), new HexPosition(2, 5, HexDirection.Top), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }]).ToData(),
                new PathSegment(new HexPosition(2, 5, HexDirection.Top), new HexPosition(3, 5, HexDirection.Top), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }]).ToData(),
                new PathSegment(new HexPosition(3, 5, HexDirection.Top), new HexPosition(4, 5, HexDirection.Top), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }]).ToData(),
                new PathSegment(new HexPosition(4, 5, HexDirection.Top), new HexPosition(5, 5, HexDirection.Top), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }]).ToData(),
                new PathSegment(new HexPosition(5, 5, HexDirection.Top), new HexPosition(6, 5, HexDirection.Top), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }]).ToData(),
                new PathSegment(new HexPosition(6, 5, HexDirection.Top), new HexPosition(7, 5, HexDirection.Top), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }]).ToData(),
                new PathSegment(new HexPosition(7, 5, HexDirection.Top), new HexPosition(8, 5, HexDirection.Top), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }]).ToData(),
                new PathSegment(new HexPosition(8, 5, HexDirection.Top), new HexPosition(8, 5, HexDirection.Bottom), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 0 }]).ToData(),
                new PathSegment(new HexPosition(8, 5, HexDirection.Bottom), new HexPosition(9, 5, HexDirection.Bottom), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }]).ToData()
            ]
        };

        var fallContextData = new FallContextData
        {
            UnitId = unit.Id,
            GameId = Game.Id,
            IsFalling = true,
            PilotingSkillRoll = new PilotingSkillRollData
            {
                RollContext = new SkidCheckRollContext(7, 3),
                DiceResults = [2, 2],
                IsSuccessful = false,
                PsrBreakdown = new PsrBreakdown { BasePilotingSkill = 4, Modifiers = [] }
            },
            FallingDamageData = new FallingDamageData(
                HexDirection.Top, new HitLocationsData([], 5), new DiceResult(3), HitDirection.Front)
        };

        MockFallProcessor.ProcessMovementAttempt(unit,
            Arg.Is<SkidCheckRollContext>(c => c.SkidDistance == 3 && c.HexesMoved == 7), Game, MovementType.Run, Arg.Any<HexPosition?>())
            .Returns(fallContextData);

        CommandPublisher.ClearReceivedCalls();
        _sut.HandleCommand(moveCommand);

        CommandPublisher.Received().PublishCommand(
            Arg.Is<MoveUnitCommand>(cmd => cmd.UnitId == _unit1Id && cmd.IsCompleted));
        CommandPublisher.Received().PublishCommand(
            Arg.Is<MechFallCommand>(cmd => cmd.UnitId == _unit1Id));

        unit.Position!.Coordinates.ShouldBe(new HexCoordinates(8, 2));
    }

    [Fact]
    public void WhenSkidEntersHeavyWoods_ShouldStopEarly()
    {
        SetMap();
        _sut.Enter();
        var unit = Game.PhaseStepState!.Value.ActivePlayer.Units.Single(u => u.Id == _unit1Id) as Mech;
        unit!.Deploy(new HexPosition(1, 5, HexDirection.Top), null);

        var turnHex = Game.BattleMap!.GetHex(new HexCoordinates(6, 5));
        turnHex!.AddTerrain(new PavementTerrain());

        var heavyWoodsHex = Game.BattleMap!.GetHex(new HexCoordinates(6, 4));
        heavyWoodsHex!.AddTerrain(new HeavyWoodsTerrain());

        var moveCommand = new MoveUnitCommand
        {
            MovementType = MovementType.Run,
            GameOriginId = Game.Id,
            PlayerId = Game.PhaseStepState!.Value.ActivePlayer.Id,
            UnitId = _unit1Id,
            MovementPath =
            [
                new PathSegment(new HexPosition(1, 5, HexDirection.Top), new HexPosition(2, 5, HexDirection.Top), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }]).ToData(),
                new PathSegment(new HexPosition(2, 5, HexDirection.Top), new HexPosition(3, 5, HexDirection.Top), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }]).ToData(),
                new PathSegment(new HexPosition(3, 5, HexDirection.Top), new HexPosition(4, 5, HexDirection.Top), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }]).ToData(),
                new PathSegment(new HexPosition(4, 5, HexDirection.Top), new HexPosition(5, 5, HexDirection.Top), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }]).ToData(),
                new PathSegment(new HexPosition(5, 5, HexDirection.Top), new HexPosition(6, 5, HexDirection.Top), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }]).ToData(),
                new PathSegment(new HexPosition(6, 5, HexDirection.Top), new HexPosition(6, 5, HexDirection.Bottom), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 0 }]).ToData(),
                new PathSegment(new HexPosition(6, 5, HexDirection.Bottom), new HexPosition(7, 5, HexDirection.Bottom), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }]).ToData()
            ]
        };

        var fallContextData = new FallContextData
        {
            UnitId = unit.Id,
            GameId = Game.Id,
            IsFalling = true,
            PilotingSkillRoll = new PilotingSkillRollData
            {
                RollContext = new SkidCheckRollContext(5, 3),
                DiceResults = [2, 2],
                IsSuccessful = false,
                PsrBreakdown = new PsrBreakdown { BasePilotingSkill = 4, Modifiers = [] }
            },
            FallingDamageData = new FallingDamageData(
                HexDirection.Top, new HitLocationsData([], 5), new DiceResult(3), HitDirection.Front)
        };

        MockFallProcessor.ProcessMovementAttempt(unit,
            Arg.Is<SkidCheckRollContext>(c => c.SkidDistance == 1 && c.HexesMoved == 5), Game, MovementType.Run, Arg.Any<HexPosition?>())
            .Returns(fallContextData);

        CommandPublisher.ClearReceivedCalls();
        _sut.HandleCommand(moveCommand);

        CommandPublisher.Received().PublishCommand(
            Arg.Is<MoveUnitCommand>(cmd => cmd.UnitId == _unit1Id && cmd.IsCompleted));
        CommandPublisher.Received().PublishCommand(
            Arg.Is<MechFallCommand>(cmd => cmd.UnitId == _unit1Id));

        unit.Position!.Coordinates.ShouldBe(new HexCoordinates(6, 4));
    }

    [Fact]
    public void WhenSkidEntersRoughTerrain_ShouldReduceSkidDistance()
    {
        SetMap();
        _sut.Enter();
        var unit = Game.PhaseStepState!.Value.ActivePlayer.Units.Single(u => u.Id == _unit1Id) as Mech;
        unit!.Deploy(new HexPosition(1, 5, HexDirection.Top), null);

        var turnHex = Game.BattleMap!.GetHex(new HexCoordinates(6, 5));
        turnHex!.AddTerrain(new PavementTerrain());

        var roughHex = Game.BattleMap!.GetHex(new HexCoordinates(6, 4));
        roughHex!.AddTerrain(new RoughTerrain());

        var moveCommand = new MoveUnitCommand
        {
            MovementType = MovementType.Run,
            GameOriginId = Game.Id,
            PlayerId = Game.PhaseStepState!.Value.ActivePlayer.Id,
            UnitId = _unit1Id,
            MovementPath =
            [
                new PathSegment(new HexPosition(1, 5, HexDirection.Top), new HexPosition(2, 5, HexDirection.Top), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }]).ToData(),
                new PathSegment(new HexPosition(2, 5, HexDirection.Top), new HexPosition(3, 5, HexDirection.Top), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }]).ToData(),
                new PathSegment(new HexPosition(3, 5, HexDirection.Top), new HexPosition(4, 5, HexDirection.Top), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }]).ToData(),
                new PathSegment(new HexPosition(4, 5, HexDirection.Top), new HexPosition(5, 5, HexDirection.Top), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }]).ToData(),
                new PathSegment(new HexPosition(5, 5, HexDirection.Top), new HexPosition(6, 5, HexDirection.Top), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }]).ToData(),
                new PathSegment(new HexPosition(6, 5, HexDirection.Top), new HexPosition(6, 5, HexDirection.Bottom), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 0 }]).ToData(),
                new PathSegment(new HexPosition(6, 5, HexDirection.Bottom), new HexPosition(7, 5, HexDirection.Bottom), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }]).ToData()
            ]
        };

        var fallContextData = new FallContextData
        {
            UnitId = unit.Id,
            GameId = Game.Id,
            IsFalling = true,
            PilotingSkillRoll = new PilotingSkillRollData
            {
                RollContext = new SkidCheckRollContext(5, 3),
                DiceResults = [2, 2],
                IsSuccessful = false,
                PsrBreakdown = new PsrBreakdown { BasePilotingSkill = 4, Modifiers = [] }
            },
            FallingDamageData = new FallingDamageData(
                HexDirection.Top, new HitLocationsData([], 5), new DiceResult(3), HitDirection.Front)
        };

        MockFallProcessor.ProcessMovementAttempt(unit,
            Arg.Is<SkidCheckRollContext>(c => c.SkidDistance == 2 && c.HexesMoved == 5), Game, MovementType.Run, Arg.Any<HexPosition?>())
            .Returns(fallContextData);

        CommandPublisher.ClearReceivedCalls();
        _sut.HandleCommand(moveCommand);

        CommandPublisher.Received().PublishCommand(
            Arg.Is<MoveUnitCommand>(cmd => cmd.UnitId == _unit1Id && cmd.IsCompleted));
        CommandPublisher.Received().PublishCommand(
            Arg.Is<MechFallCommand>(cmd => cmd.UnitId == _unit1Id));

        unit.Position!.Coordinates.ShouldBe(new HexCoordinates(6, 3));
    }

    [Fact]
    public void WhenSkidContinuesOnPavement_ShouldNotReduceExtraDistance()
    {
        SetMap();
        _sut.Enter();
        var unit = Game.PhaseStepState!.Value.ActivePlayer.Units.Single(u => u.Id == _unit1Id) as Mech;
        unit!.Deploy(new HexPosition(1, 5, HexDirection.Top), null);

        var turnHex = Game.BattleMap!.GetHex(new HexCoordinates(6, 5));
        turnHex!.AddTerrain(new PavementTerrain());

        var pavementHex1 = Game.BattleMap!.GetHex(new HexCoordinates(6, 4));
        pavementHex1!.AddTerrain(new PavementTerrain());
        var pavementHex2 = Game.BattleMap!.GetHex(new HexCoordinates(6, 3));
        pavementHex2!.AddTerrain(new PavementTerrain());
        var pavementHex3 = Game.BattleMap!.GetHex(new HexCoordinates(6, 2));
        pavementHex3!.AddTerrain(new PavementTerrain());

        var moveCommand = new MoveUnitCommand
        {
            MovementType = MovementType.Run,
            GameOriginId = Game.Id,
            PlayerId = Game.PhaseStepState!.Value.ActivePlayer.Id,
            UnitId = _unit1Id,
            MovementPath =
            [
                new PathSegment(new HexPosition(1, 5, HexDirection.Top), new HexPosition(2, 5, HexDirection.Top), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }]).ToData(),
                new PathSegment(new HexPosition(2, 5, HexDirection.Top), new HexPosition(3, 5, HexDirection.Top), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }]).ToData(),
                new PathSegment(new HexPosition(3, 5, HexDirection.Top), new HexPosition(4, 5, HexDirection.Top), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }]).ToData(),
                new PathSegment(new HexPosition(4, 5, HexDirection.Top), new HexPosition(5, 5, HexDirection.Top), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }]).ToData(),
                new PathSegment(new HexPosition(5, 5, HexDirection.Top), new HexPosition(6, 5, HexDirection.Top), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }]).ToData(),
                new PathSegment(new HexPosition(6, 5, HexDirection.Top), new HexPosition(6, 5, HexDirection.Bottom), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 0 }]).ToData(),
                new PathSegment(new HexPosition(6, 5, HexDirection.Bottom), new HexPosition(7, 5, HexDirection.Bottom), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }]).ToData()
            ]
        };

        var fallContextData = new FallContextData
        {
            UnitId = unit.Id,
            GameId = Game.Id,
            IsFalling = true,
            PilotingSkillRoll = new PilotingSkillRollData
            {
                RollContext = new SkidCheckRollContext(5, 3),
                DiceResults = [2, 2],
                IsSuccessful = false,
                PsrBreakdown = new PsrBreakdown { BasePilotingSkill = 4, Modifiers = [] }
            },
            FallingDamageData = new FallingDamageData(
                HexDirection.Top, new HitLocationsData([], 5), new DiceResult(3), HitDirection.Front)
        };

        MockFallProcessor.ProcessMovementAttempt(unit,
            Arg.Is<SkidCheckRollContext>(c => c.SkidDistance == 3 && c.HexesMoved == 5), Game, MovementType.Run, Arg.Any<HexPosition?>())
            .Returns(fallContextData);

        CommandPublisher.ClearReceivedCalls();
        _sut.HandleCommand(moveCommand);

        CommandPublisher.Received().PublishCommand(
            Arg.Is<MoveUnitCommand>(cmd => cmd.UnitId == _unit1Id && cmd.IsCompleted));
        CommandPublisher.Received().PublishCommand(
            Arg.Is<MechFallCommand>(cmd => cmd.UnitId == _unit1Id));

        unit.Position!.Coordinates.ShouldBe(new HexCoordinates(6, 2));
    }

    [Fact]
    public void WhenSkidWouldExitMap_ShouldStopAtBoundary()
    {
        SetMap();
        _sut.Enter();
        var unit = Game.PhaseStepState!.Value.ActivePlayer.Units.Single(u => u.Id == _unit1Id) as Mech;
        unit!.Deploy(new HexPosition(1, 1, HexDirection.Top), null);

        var turnHex = Game.BattleMap!.GetHex(new HexCoordinates(6, 1));
        turnHex!.AddTerrain(new PavementTerrain());

        var moveCommand = new MoveUnitCommand
        {
            MovementType = MovementType.Run,
            GameOriginId = Game.Id,
            PlayerId = Game.PhaseStepState!.Value.ActivePlayer.Id,
            UnitId = _unit1Id,
            MovementPath =
            [
                new PathSegment(new HexPosition(1, 1, HexDirection.Top), new HexPosition(2, 1, HexDirection.Top), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }]).ToData(),
                new PathSegment(new HexPosition(2, 1, HexDirection.Top), new HexPosition(3, 1, HexDirection.Top), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }]).ToData(),
                new PathSegment(new HexPosition(3, 1, HexDirection.Top), new HexPosition(4, 1, HexDirection.Top), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }]).ToData(),
                new PathSegment(new HexPosition(4, 1, HexDirection.Top), new HexPosition(5, 1, HexDirection.Top), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }]).ToData(),
                new PathSegment(new HexPosition(5, 1, HexDirection.Top), new HexPosition(6, 1, HexDirection.Top), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }]).ToData(),
                new PathSegment(new HexPosition(6, 1, HexDirection.Top), new HexPosition(6, 1, HexDirection.Bottom), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 0 }]).ToData(),
                new PathSegment(new HexPosition(6, 1, HexDirection.Bottom), new HexPosition(7, 1, HexDirection.Bottom), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }]).ToData()
            ]
        };

        var fallContextData = new FallContextData
        {
            UnitId = unit.Id,
            GameId = Game.Id,
            IsFalling = true,
            PilotingSkillRoll = new PilotingSkillRollData
            {
                RollContext = new SkidCheckRollContext(5, 3),
                DiceResults = [2, 2],
                IsSuccessful = false,
                PsrBreakdown = new PsrBreakdown { BasePilotingSkill = 4, Modifiers = [] }
            },
            FallingDamageData = new FallingDamageData(
                HexDirection.Top, new HitLocationsData([], 5), new DiceResult(3), HitDirection.Front)
        };

        MockFallProcessor.ProcessMovementAttempt(unit,
            Arg.Is<SkidCheckRollContext>(c => c.SkidDistance == 0 && c.HexesMoved == 5), Game, MovementType.Run, Arg.Any<HexPosition?>())
            .Returns(fallContextData);

        CommandPublisher.ClearReceivedCalls();
        _sut.HandleCommand(moveCommand);

        CommandPublisher.Received().PublishCommand(
            Arg.Is<MoveUnitCommand>(cmd => cmd.UnitId == _unit1Id && cmd.IsCompleted));
        CommandPublisher.Received().PublishCommand(
            Arg.Is<MechFallCommand>(cmd => cmd.UnitId == _unit1Id));

        unit.Position!.Coordinates.ShouldBe(new HexCoordinates(6, 1));
    }

    [Fact]
    public void WhenSkidEntersWater_ShouldConsumeBasedOnDepth()
    {
        SetMap();
        _sut.Enter();
        var activePlayer = Game.PhaseStepState!.Value.ActivePlayer;
        var unit1 = activePlayer.Units.Single(u => u.Id == _unit1Id) as Mech;
        var unit2 = activePlayer.Units.First(u => u.Id != _unit1Id) as Mech;
        unit1!.Deploy(new HexPosition(1, 5, HexDirection.Top), null);
        unit2!.Deploy(new HexPosition(1, 3, HexDirection.Top), null);

        var turnHex1 = Game.BattleMap!.GetHex(new HexCoordinates(8, 5))!;
        turnHex1.AddTerrain(new PavementTerrain());
        var turnHex2 = Game.BattleMap!.GetHex(new HexCoordinates(6, 3))!;
        turnHex2.AddTerrain(new PavementTerrain());

        var standardWaterHex = Game.BattleMap!.GetHex(new HexCoordinates(8, 2))!;
        standardWaterHex.AddTerrain(new WaterTerrain(-1));
        var deepWaterHex = Game.BattleMap!.GetHex(new HexCoordinates(6, 2))!;
        deepWaterHex.AddTerrain(new WaterTerrain(-2));

        var unit1Command = new MoveUnitCommand
        {
            MovementType = MovementType.Run,
            GameOriginId = Game.Id,
            PlayerId = Game.PhaseStepState!.Value.ActivePlayer.Id,
            UnitId = _unit1Id,
            MovementPath =
            [
                new PathSegment(new HexPosition(1, 5, HexDirection.Top), new HexPosition(2, 5, HexDirection.Top), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }]).ToData(),
                new PathSegment(new HexPosition(2, 5, HexDirection.Top), new HexPosition(3, 5, HexDirection.Top), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }]).ToData(),
                new PathSegment(new HexPosition(3, 5, HexDirection.Top), new HexPosition(4, 5, HexDirection.Top), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }]).ToData(),
                new PathSegment(new HexPosition(4, 5, HexDirection.Top), new HexPosition(5, 5, HexDirection.Top), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }]).ToData(),
                new PathSegment(new HexPosition(5, 5, HexDirection.Top), new HexPosition(6, 5, HexDirection.Top), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }]).ToData(),
                new PathSegment(new HexPosition(6, 5, HexDirection.Top), new HexPosition(7, 5, HexDirection.Top), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }]).ToData(),
                new PathSegment(new HexPosition(7, 5, HexDirection.Top), new HexPosition(8, 5, HexDirection.Top), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }]).ToData(),
                new PathSegment(new HexPosition(8, 5, HexDirection.Top), new HexPosition(8, 5, HexDirection.Bottom), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 0 }]).ToData(),
                new PathSegment(new HexPosition(8, 5, HexDirection.Bottom), new HexPosition(9, 5, HexDirection.Bottom), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }]).ToData()
            ]
        };

        var unit2Command = new MoveUnitCommand
        {
            MovementType = MovementType.Run,
            GameOriginId = Game.Id,
            PlayerId = activePlayer.Id,
            UnitId = unit2.Id,
            MovementPath =
            [
                new PathSegment(new HexPosition(1, 3, HexDirection.Top), new HexPosition(2, 3, HexDirection.Top), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }]).ToData(),
                new PathSegment(new HexPosition(2, 3, HexDirection.Top), new HexPosition(3, 3, HexDirection.Top), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }]).ToData(),
                new PathSegment(new HexPosition(3, 3, HexDirection.Top), new HexPosition(4, 3, HexDirection.Top), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }]).ToData(),
                new PathSegment(new HexPosition(4, 3, HexDirection.Top), new HexPosition(5, 3, HexDirection.Top), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }]).ToData(),
                new PathSegment(new HexPosition(5, 3, HexDirection.Top), new HexPosition(6, 3, HexDirection.Top), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }]).ToData(),
                new PathSegment(new HexPosition(6, 3, HexDirection.Top), new HexPosition(6, 3, HexDirection.Bottom), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 0 }]).ToData(),
                new PathSegment(new HexPosition(6, 3, HexDirection.Bottom), new HexPosition(7, 3, HexDirection.Bottom), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }]).ToData()
            ]
        };

        var waterFallContextData = new FallContextData
        {
            UnitId = unit1.Id,
            GameId = Game.Id,
            IsFalling = true,
            PilotingSkillRoll = new PilotingSkillRollData
            {
                RollContext = new SkidCheckRollContext(7, 3),
                DiceResults = [2, 2],
                IsSuccessful = false,
                PsrBreakdown = new PsrBreakdown { BasePilotingSkill = 4, Modifiers = [] }
            },
            FallingDamageData = new FallingDamageData(
                HexDirection.Top, new HitLocationsData([], 5), new DiceResult(3), HitDirection.Front)
        };

        var deepWaterFallContextData = new FallContextData
        {
            UnitId = unit2.Id,
            GameId = Game.Id,
            IsFalling = true,
            PilotingSkillRoll = new PilotingSkillRollData
            {
                RollContext = new SkidCheckRollContext(5, 3),
                DiceResults = [2, 2],
                IsSuccessful = false,
                PsrBreakdown = new PsrBreakdown { BasePilotingSkill = 4, Modifiers = [] }
            },
            FallingDamageData = new FallingDamageData(
                HexDirection.Top, new HitLocationsData([], 5), new DiceResult(3), HitDirection.Front)
        };

        MockFallProcessor.ProcessMovementAttempt(unit1,
            Arg.Is<SkidCheckRollContext>(c => c.SkidDistance == 3 && c.HexesMoved == 7), Game, MovementType.Run, Arg.Any<HexPosition?>())
            .Returns(waterFallContextData);
        MockFallProcessor.ProcessMovementAttempt(unit2,
            Arg.Is<SkidCheckRollContext>(c => c.SkidDistance == 1 && c.HexesMoved == 5), Game, MovementType.Run, Arg.Any<HexPosition?>())
            .Returns(deepWaterFallContextData);

        CommandPublisher.ClearReceivedCalls();

        _sut.HandleCommand(unit1Command);

        CommandPublisher.Received().PublishCommand(
            Arg.Is<MoveUnitCommand>(cmd => cmd.UnitId == _unit1Id && cmd.IsCompleted));
        CommandPublisher.Received().PublishCommand(
            Arg.Is<MechFallCommand>(cmd => cmd.UnitId == _unit1Id));
        unit1.Position!.Coordinates.ShouldBe(new HexCoordinates(8, 2));

        CommandPublisher.ClearReceivedCalls();

        _sut.HandleCommand(unit2Command);

        CommandPublisher.Received().PublishCommand(
            Arg.Is<MoveUnitCommand>(cmd => cmd.UnitId == unit2.Id && cmd.IsCompleted));
        CommandPublisher.Received().PublishCommand(
            Arg.Is<MechFallCommand>(cmd => cmd.UnitId == unit2.Id));
        unit2.Position!.Coordinates.ShouldBe(new HexCoordinates(6, 2));
    }

    [Fact]
    public void ProcessMoveCommand_WhenWaterEntryFall_ShouldAddFallEventToPath()
    {
        SetMap();
        _sut.Enter();
        var unit = Game.PhaseStepState!.Value.ActivePlayer.Units.Single(u => u.Id == _unit1Id) as Mech;
        unit!.Deploy(new HexPosition(1, 2, HexDirection.Top), null);

        var hex = Game.BattleMap!.GetHex(new HexCoordinates(2, 2));
        hex!.AddTerrain(new WaterTerrain(-1));

        var moveCommand = new MoveUnitCommand
        {
            MovementType = MovementType.Walk,
            GameOriginId = Game.Id,
            PlayerId = Game.PhaseStepState!.Value.ActivePlayer.Id,
            UnitId = _unit1Id,
            MovementPath = [
                new PathSegment(new HexPosition(1, 2, HexDirection.Top), new HexPosition(2, 2, HexDirection.Top), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 2 }]).ToData(),
                new PathSegment(new HexPosition(2, 2, HexDirection.Top), new HexPosition(3, 2, HexDirection.Top), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }]).ToData()
            ]
        };

        var fallContextData = new FallContextData
        {
            UnitId = unit.Id,
            GameId = Game.Id,
            IsFalling = true,
            PilotingSkillRoll = new PilotingSkillRollData
            {
                RollContext = new EnteringDeepWaterRollContext(1),
                DiceResults = [2, 2],
                IsSuccessful = false,
                PsrBreakdown = new PsrBreakdown { BasePilotingSkill = 4, Modifiers = [] }
            },
            FallingDamageData = new FallingDamageData(
                HexDirection.Top,
                new HitLocationsData([], 5),
                new DiceResult(3), HitDirection.Front)
        };

        MockFallProcessor.ProcessMovementAttempt(unit, Arg.Is<EnteringDeepWaterRollContext>(c => c.WaterDepth == 1), Game, MovementType.Walk, Arg.Any<HexPosition?>()).Returns(fallContextData);

        CommandPublisher.ClearReceivedCalls();

        _sut.HandleCommand(moveCommand);

        unit.MovementTaken.ShouldNotBeNull();
        unit.MovementTaken.Segments[^1].Events.ShouldContain(e => e.Type == SegmentEventType.Fall);
    }

    [Fact]
    public void ProcessMoveCommand_WhenRunSkidFall_ShouldHaveBothSkidAndFallEventsOnPath()
    {
        SetMap();
        _sut.Enter();
        var unit = Game.PhaseStepState!.Value.ActivePlayer.Units.Single(u => u.Id == _unit1Id) as Mech;
        unit!.Deploy(new HexPosition(1, 2, HexDirection.Top), null);

        var roadHex = Game.BattleMap!.GetHex(new HexCoordinates(2, 2));
        roadHex!.AddTerrain(new RoadTerrain());

        var moveCommand = new MoveUnitCommand
        {
            MovementType = MovementType.Run,
            GameOriginId = Game.Id,
            PlayerId = Game.PhaseStepState!.Value.ActivePlayer.Id,
            UnitId = _unit1Id,
            MovementPath =
            [
                new PathSegment(new HexPosition(1, 2, HexDirection.Top), new HexPosition(2, 2, HexDirection.Top), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }]).ToData(),
                new PathSegment(new HexPosition(2, 2, HexDirection.Top), new HexPosition(2, 2, HexDirection.Bottom), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 0 }]).ToData(),
                new PathSegment(new HexPosition(2, 2, HexDirection.Bottom), new HexPosition(3, 2, HexDirection.Bottom), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }]).ToData()
            ]
        };

        var fallContextData = new FallContextData
        {
            UnitId = unit.Id,
            GameId = Game.Id,
            IsFalling = true,
            PilotingSkillRoll = new PilotingSkillRollData
            {
                RollContext = new SkidCheckRollContext(1, 3),
                DiceResults = [2, 2],
                IsSuccessful = false,
                PsrBreakdown = new PsrBreakdown { BasePilotingSkill = 4, Modifiers = [] }
            },
            FallingDamageData = new FallingDamageData(
                HexDirection.Top,
                new HitLocationsData([], 5),
                new DiceResult(3), HitDirection.Front)
        };

        MockFallProcessor.ProcessMovementAttempt(unit, Arg.Is<SkidCheckRollContext>(c => c.SkidDistance == 1 && c.HexesMoved == 1), Game, MovementType.Run, Arg.Any<HexPosition?>()).Returns(fallContextData);

        CommandPublisher.ClearReceivedCalls();

        _sut.HandleCommand(moveCommand);

        unit.MovementTaken.ShouldNotBeNull();
        unit.MovementTaken.Segments.Any(s => s.Events.Any(e => e.Type == SegmentEventType.Skid)).ShouldBeTrue();
        unit.MovementTaken.Segments[^1].Events.ShouldContain(e => e.Type == SegmentEventType.Fall);
    }

    [Fact]
    public void ProcessMoveCommand_WhenJumpFall_ShouldAddFallEventToPath()
    {
        _sut.Enter();
        var unit = Game.PhaseStepState!.Value.ActivePlayer.Units.Single(u => u.Id == _unit1Id) as Mech;
        unit!.Deploy(new HexPosition(1, 2, HexDirection.Top), null);

        var gyro = unit.GetAllComponents<Gyro>().First();
        gyro.Hit();

        var failedFallContext = new FallContextData
        {
            UnitId = _unit1Id,
            GameId = Game.Id,
            IsFalling = true,
            PilotingSkillRoll = new PilotingSkillRollData
            {
                RollContext = new PilotingSkillRollContext(PilotingSkillRollType.JumpWithDamage),
                DiceResults = [2, 2],
                IsSuccessful = false,
                PsrBreakdown = new PsrBreakdown { BasePilotingSkill = 4, Modifiers = [] }
            },
            FallingDamageData = new FallingDamageData(
                HexDirection.Top,
                new HitLocationsData([], 5),
                new DiceResult(3), HitDirection.Front)
        };

        MockFallProcessor.ProcessMovementAttempt(unit, new PilotingSkillRollContext(PilotingSkillRollType.JumpWithDamage), Game, MovementType.Jump).Returns(failedFallContext);

        var moveCommand = new MoveUnitCommand
        {
            MovementType = MovementType.Jump,
            GameOriginId = Game.Id,
            PlayerId = Game.PhaseStepState!.Value.ActivePlayer.Id,
            UnitId = _unit1Id,
            MovementPath = [
                new PathSegment(new HexPosition(1, 2, HexDirection.Top), new HexPosition(3, 1, HexDirection.Bottom), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }])
                    .ToData()
            ]
        };

        CommandPublisher.ClearReceivedCalls();

        _sut.HandleCommand(moveCommand);

        unit.MovementTaken.ShouldNotBeNull();
        unit.MovementTaken.Segments[^1].Events.ShouldContain(e => e.Type == SegmentEventType.Fall);
    }

    [Fact]
    public void ProcessStandupCommand_WhenSuccessful_ShouldHaveStandupAttemptEventWithCorrectCost()
    {
        _sut.Enter();
        var unit = Game.PhaseStepState!.Value.ActivePlayer.Units.Single(u => u.Id == _unit1Id) as Mech;
        unit!.Deploy(new HexPosition(1, 2, HexDirection.Top), null);
        unit.SetProne();

        var psrBreakdown = new PsrBreakdown
        {
            BasePilotingSkill = 4,
            Modifiers = [],
        };

        var successfulPsrData = new PilotingSkillRollData
        {
            RollContext = new PilotingSkillRollContext(PilotingSkillRollType.StandupAttempt),
            DiceResults = [3, 3],
            IsSuccessful = true,
            PsrBreakdown = psrBreakdown
        };

        var successfulStandupData = new FallContextData
        {
            UnitId = unit.Id,
            GameId = Game.Id,
            IsFalling = false,
            PilotingSkillRoll = successfulPsrData,
            LevelsFallen = 0,
            WasJumping = false
        };

        Game.FallProcessor.ProcessMovementAttempt(unit, new PilotingSkillRollContext(PilotingSkillRollType.StandupAttempt), Game, MovementType.StandingStill).Returns(successfulStandupData);

        CommandPublisher.ClearReceivedCalls();

        _sut.HandleCommand(new TryStandupCommand
        {
            GameOriginId = Game.Id,
            PlayerId = _player1Id,
            UnitId = _unit1Id,
            Timestamp = DateTime.UtcNow,
            NewFacing = HexDirection.Bottom,
            MovementTypeAfterStandup = MovementType.Run
        });

        unit.MovementTaken.ShouldNotBeNull();
        unit.MovementTaken.Segments[^1].Events.ShouldContain(e => e.Type == SegmentEventType.StandupAttempt);
        unit.MovementTaken.Segments[^1].Costs.OfType<StandUpAttemptMovementCost>().Any(c => c.Value == 2).ShouldBeTrue();
    }

    [Fact]
    public void ProcessStandupCommand_WhenMovementTakenNull_ShouldCreatePathWithStandupAttemptEvent()
    {
        _sut.Enter();
        var unit = Game.PhaseStepState!.Value.ActivePlayer.Units.Single(u => u.Id == _unit1Id) as Mech;
        unit!.Deploy(new HexPosition(1, 2, HexDirection.Top), null);
        unit.SetProne();
        unit.MovementTaken.ShouldBeNull();

        var psrBreakdown = new PsrBreakdown
        {
            BasePilotingSkill = 4,
            Modifiers = [],
        };

        var successfulPsrData = new PilotingSkillRollData
        {
            RollContext = new PilotingSkillRollContext(PilotingSkillRollType.StandupAttempt),
            DiceResults = [3, 3],
            IsSuccessful = true,
            PsrBreakdown = psrBreakdown
        };

        var successfulStandupData = new FallContextData
        {
            UnitId = unit.Id,
            GameId = Game.Id,
            IsFalling = false,
            PilotingSkillRoll = successfulPsrData,
            LevelsFallen = 0,
            WasJumping = false
        };

        Game.FallProcessor.ProcessMovementAttempt(unit, new PilotingSkillRollContext(PilotingSkillRollType.StandupAttempt), Game, MovementType.StandingStill).Returns(successfulStandupData);

        CommandPublisher.ClearReceivedCalls();

        _sut.HandleCommand(new TryStandupCommand
        {
            GameOriginId = Game.Id,
            PlayerId = _player1Id,
            UnitId = _unit1Id,
            Timestamp = DateTime.UtcNow,
            NewFacing = HexDirection.Bottom,
            MovementTypeAfterStandup = MovementType.Run
        });

        unit.MovementTaken.ShouldNotBeNull();
        unit.MovementTaken.Segments[^1].Events.ShouldContain(e => e.Type == SegmentEventType.StandupAttempt);
    }

    [Fact]
    public void ProcessMoveCommand_WhenOnBridge_ShouldNotCollapse_WhenTonnageWithinCF()
    {
        SetMap();
        _sut.Enter();
        MockConsciousnessCalculator.MakeConsciousnessRolls(Arg.Any<IPilot>()).Returns([]);

        var unit = Game.PhaseStepState!.Value.ActivePlayer.Units.Single(u => u.Id == _unit1Id) as Mech;
        unit!.Deploy(new HexPosition(1, 2, HexDirection.Top), null);

        var hex = Game.BattleMap!.GetHex(new HexCoordinates(2, 2));
        hex!.AddTerrain(new BridgeTerrain(1, 60));

        var moveCommand = new MoveUnitCommand
        {
            MovementType = MovementType.Walk,
            GameOriginId = Game.Id,
            PlayerId = Game.PhaseStepState!.Value.ActivePlayer.Id,
            UnitId = _unit1Id,
            MovementPath =
            [
                new PathSegment(new HexPosition(1, 2, HexDirection.Top), new HexPosition(2, 2, HexDirection.Top), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 2 }]).ToData(),
                new PathSegment(new HexPosition(2, 2, HexDirection.Top), new HexPosition(3, 2, HexDirection.Top), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }]).ToData()
            ]
        };

        CommandPublisher.ClearReceivedCalls();

        _sut.HandleCommand(moveCommand);

        CommandPublisher.DidNotReceive().PublishCommand(Arg.Any<BridgeCollapsedCommand>());
        hex.HasTerrain(MakaMekTerrains.Bridge).ShouldBeTrue();
        CommandPublisher.Received().PublishCommand(Arg.Is<MoveUnitCommand>(cmd =>
            cmd.UnitId == _unit1Id && cmd.IsCompleted && cmd.MovementPath.Count == 2));
    }

    [Fact]
    public void ProcessMoveCommand_WhenOnBridge_ShouldCollapse_WhenTonnageExceedsCF()
    {
        SetMap();
        _sut.Enter();
        MockConsciousnessCalculator.MakeConsciousnessRolls(Arg.Any<IPilot>()).Returns([]);

        var activePlayer = Game.Players.First(p => p.Id == _player1Id);
        var unit1 = activePlayer.Units.Single(u => u.Id == _unit1Id) as Mech;
        var unit2 = activePlayer.Units.First(u => u.Id != _unit1Id) as Mech;

        unit1!.Deploy(new HexPosition(2, 2, HexDirection.Top), null);
        unit2!.Deploy(new HexPosition(1, 2, HexDirection.Top), null);

        var hex = Game.BattleMap!.GetHex(new HexCoordinates(2, 2));
        hex!.AddTerrain(new BridgeTerrain(2, 30));

        var fallContextForUnit1 = new FallContextData
        {
            UnitId = unit1.Id,
            GameId = Game.Id,
            IsFalling = true,
            LevelsFallen = 2,
            PilotingSkillRoll = new PilotingSkillRollData
            {
                RollContext = new BridgeCollapseRollContext(2),
                DiceResults = [2, 2],
                IsSuccessful = false,
                PsrBreakdown = new PsrBreakdown { BasePilotingSkill = 4, Modifiers = [] }
            },
            FallingDamageData = new FallingDamageData(
                HexDirection.Top,
                new HitLocationsData([], 5),
                new DiceResult(3), HitDirection.Front)
        };

        var fallContextForUnit2 = fallContextForUnit1 with { UnitId = unit2.Id };

        MockFallProcessor.ProcessMovementAttempt(unit1, Arg.Is<BridgeCollapseRollContext>(c => c.BridgeHeight == 2), Game, MovementType.StandingStill, Arg.Any<HexPosition?>())
            .Returns(fallContextForUnit1);
        MockFallProcessor.ProcessMovementAttempt(unit2, Arg.Is<BridgeCollapseRollContext>(c => c.BridgeHeight == 2), Game, MovementType.Walk, Arg.Any<HexPosition?>())
            .Returns(fallContextForUnit2);

        var moveCommand = new MoveUnitCommand
        {
            MovementType = MovementType.Walk,
            GameOriginId = Game.Id,
            PlayerId = activePlayer.Id,
            UnitId = unit2.Id,
            MovementPath =
            [
                new PathSegment(new HexPosition(1, 2, HexDirection.Top), new HexPosition(2, 2, HexDirection.Top), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 2 }]).ToData()
            ]
        };

        CommandPublisher.ClearReceivedCalls();

        _sut.HandleCommand(moveCommand);

        CommandPublisher.Received().PublishCommand(Arg.Is<MechFallCommand>(cmd => cmd.UnitId == unit1.Id));
        CommandPublisher.Received().PublishCommand(Arg.Is<MechFallCommand>(cmd => cmd.UnitId == unit2.Id));
    }

    [Fact]
    public void ProcessMoveCommand_WhenBridgeCollapses_ShouldPublishBridgeCollapsedCommand()
    {
        SetMap();
        _sut.Enter();
        MockConsciousnessCalculator.MakeConsciousnessRolls(Arg.Any<IPilot>()).Returns([]);

        var activePlayer = Game.Players.First(p => p.Id == _player1Id);
        var unit1 = activePlayer.Units.Single(u => u.Id == _unit1Id) as Mech;
        var unit2 = activePlayer.Units.First(u => u.Id != _unit1Id) as Mech;

        unit1!.Deploy(new HexPosition(2, 2, HexDirection.Top), null);
        unit2!.Deploy(new HexPosition(1, 2, HexDirection.Top), null);

        var hex = Game.BattleMap!.GetHex(new HexCoordinates(2, 2));
        hex!.AddTerrain(new BridgeTerrain(2, 30));

        var fallContextForUnit1 = new FallContextData
        {
            UnitId = unit1.Id,
            GameId = Game.Id,
            IsFalling = true,
            LevelsFallen = 2,
            FallingDamageData = new FallingDamageData(
                HexDirection.Top,
                new HitLocationsData([], 5),
                new DiceResult(3), HitDirection.Front)
        };

        var fallContextForUnit2 = fallContextForUnit1 with { UnitId = unit2.Id };

        MockFallProcessor.ProcessMovementAttempt(unit1, Arg.Any<PilotingSkillRollContext>(), Game, MovementType.StandingStill, Arg.Any<HexPosition?>())
            .Returns(fallContextForUnit1);
        MockFallProcessor.ProcessMovementAttempt(unit2, Arg.Any<PilotingSkillRollContext>(), Game, MovementType.Walk, Arg.Any<HexPosition?>())
            .Returns(fallContextForUnit2);

        var moveCommand = new MoveUnitCommand
        {
            MovementType = MovementType.Walk,
            GameOriginId = Game.Id,
            PlayerId = activePlayer.Id,
            UnitId = unit2.Id,
            MovementPath =
            [
                new PathSegment(new HexPosition(1, 2, HexDirection.Top), new HexPosition(2, 2, HexDirection.Top), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 2 }]).ToData()
            ]
        };

        CommandPublisher.ClearReceivedCalls();

        _sut.HandleCommand(moveCommand);

        CommandPublisher.Received().PublishCommand(Arg.Is<BridgeCollapsedCommand>(cmd =>
            cmd.Coordinates == new HexCoordinateData(2, 2) &&
            cmd.ConstructionFactor == 30 &&
            cmd.TotalTonnage == 40 &&
            cmd.TriggeringUnitId == unit2.Id));
    }

    [Fact]
    public void ProcessMoveCommand_WhenBridgeCollapses_ShouldRemoveBridgeTerrain()
    {
        SetMap();
        _sut.Enter();
        MockConsciousnessCalculator.MakeConsciousnessRolls(Arg.Any<IPilot>()).Returns([]);

        var activePlayer = Game.Players.First(p => p.Id == _player1Id);
        var unit1 = activePlayer.Units.Single(u => u.Id == _unit1Id) as Mech;
        var unit2 = activePlayer.Units.First(u => u.Id != _unit1Id) as Mech;

        unit1!.Deploy(new HexPosition(2, 2, HexDirection.Top), null);
        unit2!.Deploy(new HexPosition(1, 2, HexDirection.Top), null);

        var hex = Game.BattleMap!.GetHex(new HexCoordinates(2, 2));
        hex!.AddTerrain(new BridgeTerrain(2, 30));

        var fallContextForUnit1 = new FallContextData
        {
            UnitId = unit1.Id,
            GameId = Game.Id,
            IsFalling = true,
            LevelsFallen = 2,
            FallingDamageData = new FallingDamageData(
                HexDirection.Top,
                new HitLocationsData([], 5),
                new DiceResult(3), HitDirection.Front)
        };

        var fallContextForUnit2 = fallContextForUnit1 with { UnitId = unit2.Id };

        MockFallProcessor.ProcessMovementAttempt(unit1, Arg.Any<PilotingSkillRollContext>(), Game, MovementType.StandingStill, Arg.Any<HexPosition?>())
            .Returns(fallContextForUnit1);
        MockFallProcessor.ProcessMovementAttempt(unit2, Arg.Any<PilotingSkillRollContext>(), Game, MovementType.Walk, Arg.Any<HexPosition?>())
            .Returns(fallContextForUnit2);

        var moveCommand = new MoveUnitCommand
        {
            MovementType = MovementType.Walk,
            GameOriginId = Game.Id,
            PlayerId = activePlayer.Id,
            UnitId = unit2.Id,
            MovementPath =
            [
                new PathSegment(new HexPosition(1, 2, HexDirection.Top), new HexPosition(2, 2, HexDirection.Top), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 2 }]).ToData()
            ]
        };

        CommandPublisher.ClearReceivedCalls();

        _sut.HandleCommand(moveCommand);

        hex.HasTerrain(MakaMekTerrains.Bridge).ShouldBeFalse();
    }

    [Fact]
    public void ProcessMoveCommand_WhenBridgeCollapses_ShouldStampBridgeCollapseSegmentEvent()
    {
        SetMap();
        _sut.Enter();
        MockConsciousnessCalculator.MakeConsciousnessRolls(Arg.Any<IPilot>()).Returns([]);

        var activePlayer = Game.Players.First(p => p.Id == _player1Id);
        var unit1 = activePlayer.Units.Single(u => u.Id == _unit1Id) as Mech;
        var unit2 = activePlayer.Units.First(u => u.Id != _unit1Id) as Mech;

        unit1!.Deploy(new HexPosition(2, 2, HexDirection.Top), null);
        unit2!.Deploy(new HexPosition(1, 2, HexDirection.Top), null);

        var hex = Game.BattleMap!.GetHex(new HexCoordinates(2, 2));
        hex!.AddTerrain(new BridgeTerrain(2, 30));

        var fallContextForUnit1 = new FallContextData
        {
            UnitId = unit1.Id,
            GameId = Game.Id,
            IsFalling = true,
            LevelsFallen = 2,
            FallingDamageData = new FallingDamageData(
                HexDirection.Top,
                new HitLocationsData([], 5),
                new DiceResult(3), HitDirection.Front)
        };

        var fallContextForUnit2 = fallContextForUnit1 with { UnitId = unit2.Id };

        MockFallProcessor.ProcessMovementAttempt(unit1, Arg.Any<PilotingSkillRollContext>(), Game, MovementType.StandingStill, Arg.Any<HexPosition?>())
            .Returns(fallContextForUnit1);
        MockFallProcessor.ProcessMovementAttempt(unit2, Arg.Any<PilotingSkillRollContext>(), Game, MovementType.Walk, Arg.Any<HexPosition?>())
            .Returns(fallContextForUnit2);

        var moveCommand = new MoveUnitCommand
        {
            MovementType = MovementType.Walk,
            GameOriginId = Game.Id,
            PlayerId = activePlayer.Id,
            UnitId = unit2.Id,
            MovementPath =
            [
                new PathSegment(new HexPosition(1, 2, HexDirection.Top), new HexPosition(2, 2, HexDirection.Top), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 2 }]).ToData()
            ]
        };

        CommandPublisher.ClearReceivedCalls();

        _sut.HandleCommand(moveCommand);

        unit2.MovementTaken.ShouldNotBeNull();
        unit2.MovementTaken.Segments[^1].Events.ShouldContain(e => e.Type == SegmentEventType.BridgeCollapse);
    }

    [Fact]
    public void ProcessMoveCommand_WhenJumpToBridgeHex_ShouldCollapseBridge_WhenTonnageExceedsCF()
    {
        SetMap();
        _sut.Enter();
        MockConsciousnessCalculator.MakeConsciousnessRolls(Arg.Any<IPilot>()).Returns([]);

        var activePlayer = Game.Players.First(p => p.Id == _player1Id);
        var unit1 = activePlayer.Units.Single(u => u.Id == _unit1Id) as Mech;
        var unit2 = activePlayer.Units.First(u => u.Id != _unit1Id) as Mech;

        unit1!.Deploy(new HexPosition(2, 2, HexDirection.Top), null);
        unit2!.Deploy(new HexPosition(1, 2, HexDirection.Top), null);

        var hex = Game.BattleMap!.GetHex(new HexCoordinates(2, 2));
        hex!.AddTerrain(new BridgeTerrain(2, 30));

        var fallContextForUnit1 = new FallContextData
        {
            UnitId = unit1.Id,
            GameId = Game.Id,
            IsFalling = true,
            LevelsFallen = 2,
            PilotingSkillRoll = new PilotingSkillRollData
            {
                RollContext = new BridgeCollapseRollContext(2),
                DiceResults = [2, 2],
                IsSuccessful = false,
                PsrBreakdown = new PsrBreakdown { BasePilotingSkill = 4, Modifiers = [] }
            },
            FallingDamageData = new FallingDamageData(
                HexDirection.Top,
                new HitLocationsData([], 5),
                new DiceResult(3), HitDirection.Front)
        };

        var fallContextForUnit2 = fallContextForUnit1 with { UnitId = unit2.Id };

        MockFallProcessor.ProcessMovementAttempt(unit1, Arg.Is<BridgeCollapseRollContext>(c => c.BridgeHeight == 2), Game, MovementType.StandingStill, Arg.Any<HexPosition?>())
            .Returns(fallContextForUnit1);
        MockFallProcessor.ProcessMovementAttempt(unit2, Arg.Is<BridgeCollapseRollContext>(c => c.BridgeHeight == 2), Game, MovementType.Jump, Arg.Any<HexPosition?>())
            .Returns(fallContextForUnit2);

        var moveCommand = new MoveUnitCommand
        {
            MovementType = MovementType.Jump,
            GameOriginId = Game.Id,
            PlayerId = activePlayer.Id,
            UnitId = unit2.Id,
            MovementPath =
            [
                new PathSegment(new HexPosition(1, 2, HexDirection.Top), new HexPosition(2, 2, HexDirection.Top), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 2 }]).ToData()
            ]
        };

        CommandPublisher.ClearReceivedCalls();

        _sut.HandleCommand(moveCommand);

        CommandPublisher.Received().PublishCommand(Arg.Is<MechFallCommand>(cmd => cmd.UnitId == unit1.Id));
        CommandPublisher.Received().PublishCommand(Arg.Is<MechFallCommand>(cmd => cmd.UnitId == unit2.Id));
    }

    [Fact]
    public void ProcessMoveCommand_WhenJumpToBridgeHex_ShouldPublishBridgeCollapsedCommand()
    {
        SetMap();
        _sut.Enter();
        MockConsciousnessCalculator.MakeConsciousnessRolls(Arg.Any<IPilot>()).Returns([]);

        var activePlayer = Game.Players.First(p => p.Id == _player1Id);
        var unit1 = activePlayer.Units.Single(u => u.Id == _unit1Id) as Mech;
        var unit2 = activePlayer.Units.First(u => u.Id != _unit1Id) as Mech;

        unit1!.Deploy(new HexPosition(2, 2, HexDirection.Top), null);
        unit2!.Deploy(new HexPosition(1, 2, HexDirection.Top), null);

        var hex = Game.BattleMap!.GetHex(new HexCoordinates(2, 2));
        hex!.AddTerrain(new BridgeTerrain(2, 30));

        var fallContextForUnit1 = new FallContextData
        {
            UnitId = unit1.Id,
            GameId = Game.Id,
            IsFalling = true,
            LevelsFallen = 2,
            FallingDamageData = new FallingDamageData(
                HexDirection.Top,
                new HitLocationsData([], 5),
                new DiceResult(3), HitDirection.Front)
        };

        var fallContextForUnit2 = fallContextForUnit1 with { UnitId = unit2.Id };

        MockFallProcessor.ProcessMovementAttempt(unit1, Arg.Any<PilotingSkillRollContext>(), Game, MovementType.StandingStill, Arg.Any<HexPosition?>())
            .Returns(fallContextForUnit1);
        MockFallProcessor.ProcessMovementAttempt(unit2, Arg.Any<PilotingSkillRollContext>(), Game, MovementType.Jump, Arg.Any<HexPosition?>())
            .Returns(fallContextForUnit2);

        var moveCommand = new MoveUnitCommand
        {
            MovementType = MovementType.Jump,
            GameOriginId = Game.Id,
            PlayerId = activePlayer.Id,
            UnitId = unit2.Id,
            MovementPath =
            [
                new PathSegment(new HexPosition(1, 2, HexDirection.Top), new HexPosition(2, 2, HexDirection.Top), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 2 }]).ToData()
            ]
        };

        CommandPublisher.ClearReceivedCalls();

        _sut.HandleCommand(moveCommand);

        CommandPublisher.Received().PublishCommand(Arg.Is<BridgeCollapsedCommand>(cmd =>
            cmd.Coordinates == new HexCoordinateData(2, 2) &&
            cmd.ConstructionFactor == 30 &&
            cmd.TotalTonnage == 40 &&
            cmd.TriggeringUnitId == unit2.Id));
    }

    [Fact]
    public void ProcessMoveCommand_WhenJumpToBridgeHex_ShouldRemoveBridgeTerrain_WhenBridgeCollapses()
    {
        SetMap();
        _sut.Enter();
        MockConsciousnessCalculator.MakeConsciousnessRolls(Arg.Any<IPilot>()).Returns([]);

        var activePlayer = Game.Players.First(p => p.Id == _player1Id);
        var unit1 = activePlayer.Units.Single(u => u.Id == _unit1Id) as Mech;
        var unit2 = activePlayer.Units.First(u => u.Id != _unit1Id) as Mech;

        unit1!.Deploy(new HexPosition(2, 2, HexDirection.Top), null);
        unit2!.Deploy(new HexPosition(1, 2, HexDirection.Top), null);

        var hex = Game.BattleMap!.GetHex(new HexCoordinates(2, 2));
        hex!.AddTerrain(new BridgeTerrain(2, 30));

        var fallContextForUnit1 = new FallContextData
        {
            UnitId = unit1.Id,
            GameId = Game.Id,
            IsFalling = true,
            LevelsFallen = 2,
            FallingDamageData = new FallingDamageData(
                HexDirection.Top,
                new HitLocationsData([], 5),
                new DiceResult(3), HitDirection.Front)
        };

        var fallContextForUnit2 = fallContextForUnit1 with { UnitId = unit2.Id };

        MockFallProcessor.ProcessMovementAttempt(unit1, Arg.Any<PilotingSkillRollContext>(), Game, MovementType.StandingStill, Arg.Any<HexPosition?>())
            .Returns(fallContextForUnit1);
        MockFallProcessor.ProcessMovementAttempt(unit2, Arg.Any<PilotingSkillRollContext>(), Game, MovementType.Jump, Arg.Any<HexPosition?>())
            .Returns(fallContextForUnit2);

        var moveCommand = new MoveUnitCommand
        {
            MovementType = MovementType.Jump,
            GameOriginId = Game.Id,
            PlayerId = activePlayer.Id,
            UnitId = unit2.Id,
            MovementPath =
            [
                new PathSegment(new HexPosition(1, 2, HexDirection.Top), new HexPosition(2, 2, HexDirection.Top), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 2 }]).ToData()
            ]
        };

        CommandPublisher.ClearReceivedCalls();

        _sut.HandleCommand(moveCommand);

        hex.HasTerrain(MakaMekTerrains.Bridge).ShouldBeFalse();
    }

    [Fact]
    public void ProcessMoveCommand_WhenJumpToBridgeHex_ShouldNotCollapse_WhenTonnageWithinCF()
    {
        SetMap();
        _sut.Enter();
        MockConsciousnessCalculator.MakeConsciousnessRolls(Arg.Any<IPilot>()).Returns([]);

        var unit = Game.PhaseStepState!.Value.ActivePlayer.Units.Single(u => u.Id == _unit1Id) as Mech;
        unit!.Deploy(new HexPosition(1, 2, HexDirection.Top), null);

        var hex = Game.BattleMap!.GetHex(new HexCoordinates(2, 2));
        hex!.AddTerrain(new BridgeTerrain(1, 60));

        var moveCommand = new MoveUnitCommand
        {
            MovementType = MovementType.Jump,
            GameOriginId = Game.Id,
            PlayerId = Game.PhaseStepState!.Value.ActivePlayer.Id,
            UnitId = _unit1Id,
            MovementPath =
            [
                new PathSegment(new HexPosition(1, 2, HexDirection.Top), new HexPosition(2, 2, HexDirection.Top), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 2 }]).ToData()
            ]
        };

        CommandPublisher.ClearReceivedCalls();

        _sut.HandleCommand(moveCommand);

        CommandPublisher.DidNotReceive().PublishCommand(Arg.Any<BridgeCollapsedCommand>());
        hex.HasTerrain(MakaMekTerrains.Bridge).ShouldBeTrue();
        CommandPublisher.Received().PublishCommand(Arg.Is<MoveUnitCommand>(cmd =>
            cmd.UnitId == _unit1Id && cmd.IsCompleted && cmd.MovementPath.Count == 1));
    }

    [Fact]
    public void ProcessMoveCommand_ShouldProcessRubbleEntry_AndTruncateOnFall()
    {
        // Arrange
        SetMap();
        _sut.Enter();
        var unit = Game.PhaseStepState!.Value.ActivePlayer.Units.Single(u => u.Id == _unit1Id) as Mech;
        unit!.Deploy(new HexPosition(1, 2, HexDirection.Top), null);

        var hex = Game.BattleMap!.GetHex(new HexCoordinates(2, 2));
        hex!.AddTerrain(new RubbleTerrain());

        var moveCommand = new MoveUnitCommand
        {
            MovementType = MovementType.Walk,
            GameOriginId = Game.Id,
            PlayerId = Game.PhaseStepState!.Value.ActivePlayer.Id,
            UnitId = _unit1Id,
            MovementPath = [
                new PathSegment(new HexPosition(1, 2, HexDirection.Top), new HexPosition(2, 2, HexDirection.Top), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 2 }]).ToData(),
                new PathSegment(new HexPosition(2, 2, HexDirection.Top), new HexPosition(3, 2, HexDirection.Top), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }]).ToData()
            ]
        };

        var fallContextData = new FallContextData
        {
            UnitId = unit.Id,
            GameId = Game.Id,
            IsFalling = true,
            PilotingSkillRoll = new PilotingSkillRollData
            {
                RollContext = new RubbleEntryRollContext(),
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

        MockFallProcessor.ProcessMovementAttempt(unit, Arg.Is<RubbleEntryRollContext>(_ => true), Game, MovementType.Walk, Arg.Any<HexPosition?>()).Returns(fallContextData);

        CommandPublisher.ClearReceivedCalls();

        // Act
        _sut.HandleCommand(moveCommand);

        // Assert
        CommandPublisher.Received().PublishCommand(Arg.Is<MoveUnitCommand>(cmd =>
            cmd.UnitId == _unit1Id && cmd.MovementPath.Count == 1 && cmd.MovementPath[0].To.Coordinates.Q == 2 && !cmd.IsCompleted));
        CommandPublisher.Received().PublishCommand(Arg.Is<MechFallCommand>(cmd => cmd.UnitId == _unit1Id));
    }

    [Fact]
    public void ProcessMoveCommand_ShouldPublishPsrCommand_WhenRubbleEntrySucceeds_DuringWalk()
    {
        // Arrange
        SetMap();
        _sut.Enter();
        var unit = Game.PhaseStepState!.Value.ActivePlayer.Units.Single(u => u.Id == _unit1Id) as Mech;
        unit!.Deploy(new HexPosition(1, 2, HexDirection.Top), null);

        var hex = Game.BattleMap!.GetHex(new HexCoordinates(2, 2));
        hex!.AddTerrain(new RubbleTerrain());

        var moveCommand = new MoveUnitCommand
        {
            MovementType = MovementType.Walk,
            GameOriginId = Game.Id,
            PlayerId = Game.PhaseStepState!.Value.ActivePlayer.Id,
            UnitId = _unit1Id,
            MovementPath = [
                new PathSegment(new HexPosition(1, 2, HexDirection.Top), new HexPosition(2, 2, HexDirection.Top), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 2 }]).ToData()
            ]
        };

        var fallContextData = new FallContextData
        {
            UnitId = unit.Id,
            GameId = Game.Id,
            IsFalling = false,
            PilotingSkillRoll = new PilotingSkillRollData
            {
                RollContext = new RubbleEntryRollContext(),
                DiceResults = [6, 6],
                IsSuccessful = true,
                PsrBreakdown = new PsrBreakdown { BasePilotingSkill = 4, Modifiers = [] }
            }
        };

        MockFallProcessor.ProcessMovementAttempt(unit, Arg.Is<RubbleEntryRollContext>(_ => true), Game, MovementType.Walk, Arg.Any<HexPosition?>()).Returns(fallContextData);

        CommandPublisher.ClearReceivedCalls();

        // Act
        _sut.HandleCommand(moveCommand);

        // Assert
        CommandPublisher.Received().PublishCommand(Arg.Is<MoveUnitCommand>(cmd => cmd.UnitId == _unit1Id && cmd.MovementPath.Count == 1 && cmd.IsCompleted));
        CommandPublisher.Received().PublishCommand(Arg.Is<MechFallCommand>(cmd =>
            cmd.UnitId == _unit1Id && cmd.DamageData == null && cmd.FallPilotingSkillRoll!.IsSuccessful));
    }

    [Fact]
    public void ProcessMoveCommand_ShouldProcessJumpRubbleEntry()
    {
        // Arrange
        SetMap();
        _sut.Enter();
        var unit = Game.PhaseStepState!.Value.ActivePlayer.Units.Single(u => u.Id == _unit1Id) as Mech;
        unit!.Deploy(new HexPosition(1, 2, HexDirection.Top), null);

        var hex = Game.BattleMap!.GetHex(new HexCoordinates(3, 2));
        hex!.AddTerrain(new RubbleTerrain());

        var moveCommand = new MoveUnitCommand
        {
            MovementType = MovementType.Jump,
            GameOriginId = Game.Id,
            PlayerId = Game.PhaseStepState!.Value.ActivePlayer.Id,
            UnitId = _unit1Id,
            MovementPath = [
                new PathSegment(new HexPosition(1, 2, HexDirection.Top), new HexPosition(3, 2, HexDirection.Top), [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 2 }]).ToData()
            ]
        };

        var fallContextData = new FallContextData
        {
            UnitId = unit.Id,
            GameId = Game.Id,
            IsFalling = true,
            PilotingSkillRoll = new PilotingSkillRollData
            {
                RollContext = new RubbleEntryRollContext(),
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

        MockFallProcessor.ProcessMovementAttempt(unit, Arg.Is<RubbleEntryRollContext>(_ => true), Game, MovementType.Jump, Arg.Any<HexPosition?>()).Returns(fallContextData);

        CommandPublisher.ClearReceivedCalls();

        // Act
        _sut.HandleCommand(moveCommand);

        // Assert
        CommandPublisher.Received().PublishCommand(Arg.Is<MoveUnitCommand>(cmd => cmd.UnitId == _unit1Id && cmd.MovementPath.Count == 1 && cmd.IsCompleted));
        CommandPublisher.Received().PublishCommand(Arg.Is<MechFallCommand>(cmd => cmd.UnitId == _unit1Id));
    }

    // --- Charge and DFA declarations carried by a move ---

    private static PathSegment ClearStep(HexPosition from, HexPosition to) =>
        new(from, to, [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }]);

    private static HexPosition Position(HexCoordinates coordinates) =>
        new(coordinates, HexDirection.Top);

    /// <summary>
    /// A target that has already finished its move, as the movement phase requires before it can
    /// be charged or jumped on.
    /// </summary>
    private IUnit DeployMovedTarget(HexCoordinates coordinates)
    {
        var target = Game.Players[1].Units[0];
        var position = Position(coordinates);
        target.Deploy(position, null);
        target.Move(MovementPath.CreateSingleSegmentPath(position), null, true);
        return target;
    }

    private static PhysicalAttackDeclarationData Charge(Guid targetId) => new()
    {
        AttackType = PhysicalAttackType.Charge,
        AttackerLimbs = [],
        TargetId = targetId
    };

    private static PhysicalAttackDeclarationData Dfa(Guid targetId) => new()
    {
        AttackType = PhysicalAttackType.DFA,
        AttackerLimbs = [],
        TargetId = targetId
    };

    private MoveUnitCommand MoveCommand(Guid playerId, IReadOnlyList<PathSegment> segments,
        MovementType movementType, PhysicalAttackDeclarationData declaration, Guid? idempotencyKey = null) => new()
    {
        GameOriginId = Game.Id,
        PlayerId = playerId,
        UnitId = _unit1Id,
        MovementType = movementType,
        IsCompleted = true,
        IdempotencyKey = idempotencyKey,
        MovementPath = segments.Select(s => s.ToData()).ToList(),
        Declaration = declaration
    };

    private (Mech Attacker, HexCoordinates Start, HexCoordinates Middle, HexCoordinates End)
        SetUpMapAndAttacker()
    {
        SetMap();
        _sut.Enter();
        var player = Game.PhaseStepState!.Value.ActivePlayer;
        var attacker = (Mech)player.Units.Single(u => u.Id == _unit1Id);
        var start = new HexCoordinates(3, 3);
        var middle = start.GetNeighbour(HexDirection.Bottom);
        var end = middle.GetNeighbour(HexDirection.Bottom);
        attacker.Deploy(Position(start), null);
        return (attacker, start, middle, end);
    }

    private (Mech Attacker, IUnit Target, HexCoordinates Start, HexCoordinates Middle, HexCoordinates End)
        SetUpChargeOnTheMap()
    {
        var (attacker, start, middle, end) = SetUpMapAndAttacker();
        var target = DeployMovedTarget(end);
        return (attacker, target, start, middle, end);
    }

    private void AssertDeclarationRejected(MoveUnitCommand command, IUnit attacker, Guid idempotencyKey)
    {
        CommandPublisher.ClearReceivedCalls();

        _sut.HandleCommand(command);

        CommandPublisher.Received(1).PublishCommand(Arg.Is<ErrorCommand>(cmd =>
            cmd.IdempotencyKey == idempotencyKey && cmd.ErrorCode == ErrorCode.ValidationFailed));
        CommandPublisher.DidNotReceive().PublishCommand(Arg.Any<MoveUnitCommand>());
        attacker.HasDeclaredPhysicalAttack.ShouldBeFalse();
        attacker.HasMoved.ShouldBeFalse();
    }

    [Fact]
    public void HandleCommand_WhenChargeIsValid_ShouldStoreTheDeclarationWithTheComputedDistance()
    {
        // Arrange
        var (attacker, target, start, middle, end) = SetUpChargeOnTheMap();
        var playerId = Game.PhaseStepState!.Value.ActivePlayer.Id;
        var idempotencyKey = Guid.NewGuid();
        var command = MoveCommand(playerId, [ClearStep(Position(start), Position(middle)),
            ClearStep(Position(middle), Position(end))], MovementType.Walk, Charge(target.Id), idempotencyKey);
        CommandPublisher.ClearReceivedCalls();

        // Act
        _sut.HandleCommand(command);

        // Assert - the declaration is stored, the distance is the hex traversed before the target,
        // and the attacker waits in the approach hex until the attack resolves
        attacker.Position!.Coordinates.ShouldBe(middle);
        attacker.MovementTaken!.Destination.Coordinates.ShouldBe(end);
        attacker.MovementPointsSpent.ShouldBe(2, "the MP to enter the target hex is spent at declaration");
        var declaration = attacker.DeclaredPhysicalAttack.ShouldNotBeNull();
        declaration.AttackType.ShouldBe(PhysicalAttackType.Charge);
        declaration.TargetId.ShouldBe(target.Id);
        declaration.HexesMoved.ShouldBe(1);
        CommandPublisher.DidNotReceive().PublishCommand(Arg.Any<ErrorCommand>());
        CommandPublisher.Received(1).PublishCommand(Arg.Is<MoveUnitCommand>(cmd =>
            cmd.IsCompleted && cmd.Declaration != null && cmd.Declaration.HexesMoved == 1));
    }

    [Fact]
    public void HandleCommand_WhenDfaIsValid_ShouldStoreTheDeclarationAndKeepTheAttackerAirborne()
    {
        // Arrange
        var (attacker, target, start, middle, end) = SetUpChargeOnTheMap();
        var centerTorso = attacker.Parts[PartLocation.CenterTorso];
        centerTorso.TryAddComponent(new JumpJets()).ShouldBeTrue();
        centerTorso.TryAddComponent(new JumpJets()).ShouldBeTrue();
        var jumpStep = new PathSegment(Position(start), Position(middle), [new JumpMovementCost { Value = 1 }]);
        var lastJumpStep = new PathSegment(Position(middle), Position(end), [new JumpMovementCost { Value = 1 }]);
        var command = MoveCommand(Game.PhaseStepState!.Value.ActivePlayer.Id,
            [jumpStep, lastJumpStep], MovementType.Jump, Dfa(target.Id));

        // Act
        _sut.HandleCommand(command);

        // Assert - the DFA lands when it resolves, not while the weapons are declared
        attacker.Position!.Coordinates.ShouldBe(middle);
        attacker.DeclaredPhysicalAttack.ShouldNotBeNull().AttackType.ShouldBe(PhysicalAttackType.DFA);
        attacker.DeclaredPhysicalAttack!.HexesMoved.ShouldBe(1);
    }

    [Fact]
    public void HandleCommand_WhenTheTargetHasNotMoved_ShouldRejectTheDeclaration()
    {
        // Arrange
        var (attacker, target, start, middle, end) = SetUpChargeOnTheMap();
        // Undo the target's move: a unit that can still move might move away
        target.ResetTurnState();
        var idempotencyKey = Guid.NewGuid();
        var command = MoveCommand(Game.PhaseStepState!.Value.ActivePlayer.Id,
            [ClearStep(Position(start), Position(middle)), ClearStep(Position(middle), Position(end))],
            MovementType.Walk, Charge(target.Id), idempotencyKey);

        // Act & Assert
        AssertDeclarationRejected(command, attacker, idempotencyKey);
    }

    [Fact]
    public void HandleCommand_WhenTheTargetStartedTheTurnShutdown_ShouldAllowTheCharge()
    {
        // Arrange - a unit that began the turn shutdown is treated as having moved first even
        // though it never received a move command
        var (attacker, target, start, middle, end) = SetUpChargeOnTheMap();
        target.ResetTurnState();
        target.Shutdown(new ShutdownData { Reason = ShutdownReason.Heat, Turn = Game.Turn - 1 });
        var command = MoveCommand(Game.PhaseStepState!.Value.ActivePlayer.Id,
            [ClearStep(Position(start), Position(middle)), ClearStep(Position(middle), Position(end))],
            MovementType.Walk, Charge(target.Id));

        // Act
        _sut.HandleCommand(command);

        // Assert
        attacker.Position!.Coordinates.ShouldBe(middle);
        attacker.DeclaredPhysicalAttack.ShouldNotBeNull().TargetId.ShouldBe(target.Id);
    }

    [Fact]
    public void HandleCommand_WhenTheTargetStartedTheTurnUnconscious_ShouldAllowTheCharge()
    {
        // Arrange - a unit whose pilot began the turn unconscious counts as having moved first
        var (attacker, target, start, middle, end) = SetUpChargeOnTheMap();
        target.ResetTurnState();
        target.Pilot!.KnockUnconscious(Game.Turn - 1);
        var command = MoveCommand(Game.PhaseStepState!.Value.ActivePlayer.Id,
            [ClearStep(Position(start), Position(middle)), ClearStep(Position(middle), Position(end))],
            MovementType.Walk, Charge(target.Id));

        // Act
        _sut.HandleCommand(command);

        // Assert
        attacker.Position!.Coordinates.ShouldBe(middle);
        attacker.DeclaredPhysicalAttack.ShouldNotBeNull().TargetId.ShouldBe(target.Id);
    }

    [Fact]
    public void HandleCommand_WhenTheTargetIsFriendly_ShouldRejectTheDeclaration()
    {
        // Arrange
        var (attacker, _, start, middle, end) = SetUpChargeOnTheMap();
        var friendly = Game.PhaseStepState!.Value.ActivePlayer.Units[1];
        friendly.Deploy(Position(end), null);
        friendly.Move(MovementPath.CreateSingleSegmentPath(Position(end)), null, true);
        var idempotencyKey = Guid.NewGuid();
        var command = MoveCommand(Game.PhaseStepState!.Value.ActivePlayer.Id,
            [ClearStep(Position(start), Position(middle)), ClearStep(Position(middle), Position(end))],
            MovementType.Walk, Charge(friendly.Id), idempotencyKey);

        // Act & Assert
        AssertDeclarationRejected(command, attacker, idempotencyKey);
    }

    [Fact]
    public void HandleCommand_WhenTheDeclarationHasLimbs_ShouldRejectIt()
    {
        // Arrange
        var (attacker, target, start, middle, end) = SetUpChargeOnTheMap();
        var idempotencyKey = Guid.NewGuid();
        var declaration = Charge(target.Id) with { AttackerLimbs = [PartLocation.LeftArm] };
        var command = MoveCommand(Game.PhaseStepState!.Value.ActivePlayer.Id,
            [ClearStep(Position(start), Position(middle)), ClearStep(Position(middle), Position(end))],
            MovementType.Walk, declaration, idempotencyKey);

        // Act & Assert
        AssertDeclarationRejected(command, attacker, idempotencyKey);
    }

    [Fact]
    public void HandleCommand_WhenTheDeclarationHasNullLimbs_ShouldRejectIt()
    {
        // Arrange - a crafted payload can carry a null limb collection
        var (attacker, target, start, middle, end) = SetUpChargeOnTheMap();
        var idempotencyKey = Guid.NewGuid();
        var declaration = Charge(target.Id) with { AttackerLimbs = null! };
        var command = MoveCommand(Game.PhaseStepState!.Value.ActivePlayer.Id,
            [ClearStep(Position(start), Position(middle)), ClearStep(Position(middle), Position(end))],
            MovementType.Walk, declaration, idempotencyKey);

        // Act & Assert
        AssertDeclarationRejected(command, attacker, idempotencyKey);
    }

    [Fact]
    public void HandleCommand_WhenTheMovementModeDoesNotMatchTheAttack_ShouldRejectIt()
    {
        // Arrange
        var (attacker, target, start, middle, end) = SetUpChargeOnTheMap();
        var idempotencyKey = Guid.NewGuid();
        var command = MoveCommand(Game.PhaseStepState!.Value.ActivePlayer.Id,
            [ClearStep(Position(start), Position(middle)), ClearStep(Position(middle), Position(end))],
            MovementType.Walk, Dfa(target.Id), idempotencyKey);

        // Act & Assert
        AssertDeclarationRejected(command, attacker, idempotencyKey);
    }

    [Fact]
    public void HandleCommand_WhenThePathEndsElsewhere_ShouldRejectTheDeclaration()
    {
        // Arrange
        var (attacker, target, start, middle, _) = SetUpChargeOnTheMap();
        var idempotencyKey = Guid.NewGuid();
        var command = MoveCommand(Game.PhaseStepState!.Value.ActivePlayer.Id,
            [ClearStep(Position(start), Position(middle))],
            MovementType.Walk, Charge(target.Id), idempotencyKey);

        // Act & Assert
        AssertDeclarationRejected(command, attacker, idempotencyKey);
    }

    [Fact]
    public void HandleCommand_WhenTheEntryCostIsNotPaid_ShouldRejectTheDeclaration()
    {
        // Arrange
        var (attacker, target, start, middle, end) = SetUpChargeOnTheMap();
        var idempotencyKey = Guid.NewGuid();
        var freeStep = new PathSegment(Position(middle), Position(end),
            [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 0 }]);
        var command = MoveCommand(Game.PhaseStepState!.Value.ActivePlayer.Id,
            [ClearStep(Position(start), Position(middle)), freeStep],
            MovementType.Walk, Charge(target.Id), idempotencyKey);

        // Act & Assert
        AssertDeclarationRejected(command, attacker, idempotencyKey);
    }

    [Fact]
    public void HandleCommand_WhenTheMoveExceedsTheRemainingPoints_ShouldRejectTheDeclaration()
    {
        // Arrange
        var (attacker, target, start, middle, end) = SetUpChargeOnTheMap();
        var centerTorso = attacker.Parts[PartLocation.CenterTorso];
        centerTorso.TryAddComponent(new JumpJets()).ShouldBeTrue(); // a single jump MP
        var idempotencyKey = Guid.NewGuid();
        var command = MoveCommand(Game.PhaseStepState!.Value.ActivePlayer.Id,
            [
                new PathSegment(Position(start), Position(middle), [new JumpMovementCost { Value = 1 }]),
                new PathSegment(Position(middle), Position(end), [new JumpMovementCost { Value = 1 }])
            ], MovementType.Jump, Dfa(target.Id), idempotencyKey);

        // Act & Assert
        AssertDeclarationRejected(command, attacker, idempotencyKey);
    }

    [Fact]
    public void HandleCommand_WhenChargeLevelDifferenceExceedsOne_ShouldRejectTheDeclaration()
    {
        // Arrange - the target is two levels above the attacker, which only a DFA can bridge
        var (attacker, target, start, middle, end) = SetUpChargeOnTheMap();
        Game.BattleMap!.AddHex(new Hex(end, 2));
        var climbStep = new PathSegment(Position(middle), Position(end),
            [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 3 }]);
        var idempotencyKey = Guid.NewGuid();
        var command = MoveCommand(Game.PhaseStepState!.Value.ActivePlayer.Id,
            [ClearStep(Position(start), Position(middle)), climbStep],
            MovementType.Walk, Charge(target.Id), idempotencyKey);

        // Act & Assert
        AssertDeclarationRejected(command, attacker, idempotencyKey);
    }

    [Fact]
    public void HandleCommand_WhenChargeLevelDifferenceIsOne_ShouldAcceptTheDeclaration()
    {
        // Arrange - a target one level higher is a legal charge
        var (attacker, target, start, middle, end) = SetUpChargeOnTheMap();
        Game.BattleMap!.AddHex(new Hex(end, 1));
        var climbStep = new PathSegment(Position(middle), Position(end),
            [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 2 }]);
        var command = MoveCommand(Game.PhaseStepState!.Value.ActivePlayer.Id,
            [ClearStep(Position(start), Position(middle)), climbStep],
            MovementType.Walk, Charge(target.Id));

        // Act
        _sut.HandleCommand(command);

        // Assert
        attacker.Position!.Coordinates.ShouldBe(middle);
        attacker.DeclaredPhysicalAttack.ShouldNotBeNull().AttackType.ShouldBe(PhysicalAttackType.Charge);
    }

    [Fact]
    public void HandleCommand_WhenTheStartHexLevelDiffersFromTheApproachHex_ShouldAcceptTheCharge()
    {
        // Arrange - the attacker starts a hill two levels above the target, but reaches the target's
        // level before entering its hex, so only the approach hex matters for the level difference
        var (attacker, target, start, middle, end) = SetUpChargeOnTheMap();
        Game.BattleMap!.AddHex(new Hex(start, 2));
        var command = MoveCommand(Game.PhaseStepState!.Value.ActivePlayer.Id,
            [ClearStep(Position(start), Position(middle)), ClearStep(Position(middle), Position(end))],
            MovementType.Walk, Charge(target.Id));

        // Act
        _sut.HandleCommand(command);

        // Assert
        attacker.Position!.Coordinates.ShouldBe(middle);
        attacker.DeclaredPhysicalAttack.ShouldNotBeNull().AttackType.ShouldBe(PhysicalAttackType.Charge);
    }

    [Fact]
    public void HandleCommand_WhenTheApproachHexLevelDiffersFromTheTarget_ShouldRejectTheCharge()
    {
        // Arrange - the attacker starts one level above the target, but charges from a hex two levels
        // above it, which no Charge can bridge regardless of the starting hex
        var (attacker, target, start, middle, end) = SetUpChargeOnTheMap();
        Game.BattleMap!.AddHex(new Hex(start, 1));
        Game.BattleMap!.AddHex(new Hex(middle, 2));
        var climbStep = new PathSegment(Position(start), Position(middle),
            [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 2 }]);
        var dropStep = new PathSegment(Position(middle), Position(end),
            [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 3 }]);
        var idempotencyKey = Guid.NewGuid();
        var command = MoveCommand(Game.PhaseStepState!.Value.ActivePlayer.Id,
            [climbStep, dropStep], MovementType.Walk, Charge(target.Id), idempotencyKey);

        // Act & Assert
        AssertDeclarationRejected(command, attacker, idempotencyKey);
    }

    [Fact]
    public void HandleCommand_WhenTheTargetStandsInDepthOneWaterTwoEffectiveLevelsDown_ShouldRejectTheCharge()
    {
        // Arrange - the approach hex is one level above the target hex, but the Depth 1 water
        // drops the target's effective standing level to two levels below the attacker; raw hex
        // levels would wrongly allow this charge
        var (attacker, target, start, middle, end) = SetUpChargeOnTheMap();
        Game.BattleMap!.AddHex(new Hex(middle, 1));
        Game.BattleMap!.GetHex(end)!.AddTerrain(new WaterTerrain(-1));
        var waterStep = new PathSegment(Position(middle), Position(end),
            [new TerrainMovementCost { TerrainId = MakaMekTerrains.Water, Value = 4 }]);
        var idempotencyKey = Guid.NewGuid();
        var command = MoveCommand(Game.PhaseStepState!.Value.ActivePlayer.Id,
            [ClearStep(Position(start), Position(middle)), waterStep],
            MovementType.Walk, Charge(target.Id), idempotencyKey);

        // Act & Assert
        AssertDeclarationRejected(command, attacker, idempotencyKey);
    }

    [Fact]
    public void HandleCommand_WhenTheTargetStandsInDepthOneWaterOneEffectiveLevelDown_ShouldAcceptTheCharge()
    {
        // Arrange - Depth 1 water drops the target one level, which a charge can still reach
        var (attacker, target, start, middle, end) = SetUpChargeOnTheMap();
        Game.BattleMap!.GetHex(end)!.AddTerrain(new WaterTerrain(-1));
        var waterStep = new PathSegment(Position(middle), Position(end),
            [new TerrainMovementCost { TerrainId = MakaMekTerrains.Water, Value = 3 }]);
        MockFallProcessor.ProcessMovementAttempt(attacker,
                Arg.Is<EnteringDeepWaterRollContext>(c => c.WaterDepth == 1),
                Game, MovementType.Walk, Arg.Any<HexPosition?>())
            .Returns(new FallContextData
            {
                UnitId = attacker.Id,
                GameId = Game.Id,
                IsFalling = false,
                PilotingSkillRoll = new PilotingSkillRollData
                {
                    RollContext = new EnteringDeepWaterRollContext(1),
                    DiceResults = [6, 6],
                    IsSuccessful = true,
                    PsrBreakdown = new PsrBreakdown { BasePilotingSkill = 4, Modifiers = [] }
                }
            });
        var command = MoveCommand(Game.PhaseStepState!.Value.ActivePlayer.Id,
            [ClearStep(Position(start), Position(middle)), waterStep],
            MovementType.Walk, Charge(target.Id));

        // Act
        _sut.HandleCommand(command);

        // Assert
        attacker.Position!.Coordinates.ShouldBe(middle);
        attacker.DeclaredPhysicalAttack.ShouldNotBeNull().AttackType.ShouldBe(PhysicalAttackType.Charge);
    }

    [Fact]
    public void HandleCommand_WhenTheChargeTargetIsSubmerged_ShouldRejectTheCharge()
    {
        // Arrange - a Depth 2 target is fully submerged, so a surface attacker cannot charge it
        // even though the raw hex levels and the effective levels are equal
        var (attacker, target, start, middle, end) = SetUpChargeOnTheMap();
        Game.BattleMap!.AddHex(new Hex(middle, 1));
        var waterHex = new Hex(end, 1);
        waterHex.AddTerrain(new WaterTerrain(-2));
        Game.BattleMap!.AddHex(waterHex);
        var waterStep = new PathSegment(Position(middle), Position(end),
            [new TerrainMovementCost { TerrainId = MakaMekTerrains.Water, Value = 6 }]);
        var idempotencyKey = Guid.NewGuid();
        var command = MoveCommand(Game.PhaseStepState!.Value.ActivePlayer.Id,
            [ClearStep(Position(start), Position(middle)), waterStep],
            MovementType.Run, Charge(target.Id), idempotencyKey);

        // Act & Assert
        AssertDeclarationRejected(command, attacker, idempotencyKey);
    }

    [Fact]
    public void HandleCommand_WhenChargingFromSubmergedWater_ShouldRejectTheCharge()
    {
        // Arrange - a submerged attacker can only attack another submerged unit, so it cannot
        // charge a surface target even when the effective level difference is one
        var (attacker, target, start, middle, end) = SetUpChargeOnTheMap();
        var waterHex = new Hex(middle, 1);
        waterHex.AddTerrain(new WaterTerrain(-2));
        Game.BattleMap!.AddHex(waterHex);
        var climbStep = new PathSegment(Position(start), Position(middle),
            [new TerrainMovementCost { TerrainId = MakaMekTerrains.Water, Value = 5 }]);
        var chargeStep = new PathSegment(Position(middle), Position(end),
            [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 2 }]);
        var idempotencyKey = Guid.NewGuid();
        var command = MoveCommand(Game.PhaseStepState!.Value.ActivePlayer.Id,
            [climbStep, chargeStep], MovementType.Run, Charge(target.Id), idempotencyKey);

        // Act & Assert
        AssertDeclarationRejected(command, attacker, idempotencyKey);
    }

    [Fact]
    public void HandleCommand_WhenTheDfaTargetIsSubmerged_ShouldRejectTheDeclaration()
    {
        // Arrange - a DFA bridges level differences, but it cannot reach a fully submerged target
        var (attacker, target, start, middle, end) = SetUpChargeOnTheMap();
        Game.BattleMap!.AddHex(new Hex(middle, 1));
        var waterHex = new Hex(end, 1);
        waterHex.AddTerrain(new WaterTerrain(-2));
        Game.BattleMap!.AddHex(waterHex);
        var centerTorso = attacker.Parts[PartLocation.CenterTorso];
        centerTorso.TryAddComponent(new JumpJets()).ShouldBeTrue();
        centerTorso.TryAddComponent(new JumpJets()).ShouldBeTrue();
        var idempotencyKey = Guid.NewGuid();
        var command = MoveCommand(Game.PhaseStepState!.Value.ActivePlayer.Id,
            [
                new PathSegment(Position(start), Position(middle), [new JumpMovementCost { Value = 1 }]),
                new PathSegment(Position(middle), Position(end), [new JumpMovementCost { Value = 1 }])
            ], MovementType.Jump, Dfa(target.Id), idempotencyKey);

        // Act & Assert
        AssertDeclarationRejected(command, attacker, idempotencyKey);
    }

    [Fact]
    public void HandleCommand_WhenTheChargePathReverses_ShouldCountOnlyHexesAfterTheLastReversal()
    {
        // Arrange - two backward hexes followed by two forward hexes ending on the target
        var (attacker, start, middle, end) = SetUpMapAndAttacker();
        var forward1 = end.GetNeighbour(HexDirection.Bottom);
        var targetHex = forward1.GetNeighbour(HexDirection.Bottom);
        var target = DeployMovedTarget(targetHex);
        var command = MoveCommand(Game.PhaseStepState!.Value.ActivePlayer.Id,
            [
                new PathSegment(Position(start), Position(middle),
                    [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }], true),
                new PathSegment(Position(middle), Position(end),
                    [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }], true),
                ClearStep(Position(end), Position(forward1)),
                ClearStep(Position(forward1), Position(targetHex))
            ], MovementType.Walk, Charge(target.Id));

        // Act
        _sut.HandleCommand(command);

        // Assert - the forward leg entered forward1 and the target; only forward1 counts
        attacker.DeclaredPhysicalAttack.ShouldNotBeNull().HexesMoved.ShouldBe(1);
    }

    [Fact]
    public void HandleCommand_WhenTheChargePathRevisitsAHex_ShouldCountEveryEntry()
    {
        // Arrange - the path leaves and re-enters the approach hex without a direction reversal
        var (attacker, start, middle, end) = SetUpMapAndAttacker();
        var target = DeployMovedTarget(end);
        var command = MoveCommand(Game.PhaseStepState!.Value.ActivePlayer.Id,
            [
                ClearStep(Position(start), Position(middle)),
                ClearStep(Position(middle), Position(end)),
                ClearStep(Position(end), Position(middle)),
                ClearStep(Position(middle), Position(end))
            ], MovementType.Walk, Charge(target.Id));

        // Act
        _sut.HandleCommand(command);

        // Assert - middle and end were each entered twice; the target (end) is excluded, so three
        // entries remain
        attacker.DeclaredPhysicalAttack.ShouldNotBeNull().HexesMoved.ShouldBe(3);
    }

    [Fact]
    public void HandleCommand_WhenTheTargetIsAlreadyReserved_ShouldRejectTheDeclaration()
    {
        // Arrange
        var (attacker, target, start, middle, end) = SetUpChargeOnTheMap();
        var reservingUnit = Game.PhaseStepState!.Value.ActivePlayer.Units[1];
        reservingUnit.Deploy(Position(start.GetNeighbour(HexDirection.Top)), null);
        reservingUnit.DeclarePhysicalAttack(new PhysicalAttackDeclarationData
        {
            AttackType = PhysicalAttackType.Push,
            AttackerLimbs = [PartLocation.LeftArm, PartLocation.RightArm],
            TargetId = target.Id
        });
        var idempotencyKey = Guid.NewGuid();
        var command = MoveCommand(Game.PhaseStepState!.Value.ActivePlayer.Id,
            [ClearStep(Position(start), Position(middle)), ClearStep(Position(middle), Position(end))],
            MovementType.Walk, Charge(target.Id), idempotencyKey);

        // Act & Assert
        AssertDeclarationRejected(command, attacker, idempotencyKey);
    }

    [Fact]
    public void HandleCommand_WhenTheTargetIsPerformingAMovementAttack_ShouldRejectTheDeclaration()
    {
        // Arrange - a unit that is itself charging or jumping does not finish its movement until
        // the Physical Attack Phase, so it cannot be the target of another Charge or DFA
        var (attacker, target, start, middle, end) = SetUpChargeOnTheMap();
        target.DeclarePhysicalAttack(Charge(attacker.Id));
        var idempotencyKey = Guid.NewGuid();
        var command = MoveCommand(Game.PhaseStepState!.Value.ActivePlayer.Id,
            [ClearStep(Position(start), Position(middle)), ClearStep(Position(middle), Position(end))],
            MovementType.Walk, Charge(target.Id), idempotencyKey);

        // Act & Assert
        AssertDeclarationRejected(command, attacker, idempotencyKey);
    }

    [Fact]
    public void HandleCommand_WhenChargingAProneMech_ShouldRejectTheDeclaration()
    {
        // Arrange - a Mech cannot charge a prone target
        var (attacker, target, start, middle, end) = SetUpChargeOnTheMap();
        ((Mech)target).SetProne();
        var idempotencyKey = Guid.NewGuid();
        var command = MoveCommand(Game.PhaseStepState!.Value.ActivePlayer.Id,
            [ClearStep(Position(start), Position(middle)), ClearStep(Position(middle), Position(end))],
            MovementType.Walk, Charge(target.Id), idempotencyKey);

        // Act & Assert
        AssertDeclarationRejected(command, attacker, idempotencyKey);
    }

    [Fact]
    public void HandleCommand_WhenDfaTargetsAProneMech_ShouldAcceptTheDeclaration()
    {
        // Arrange - a DFA against a prone target remains legal
        var (attacker, target, start, middle, end) = SetUpChargeOnTheMap();
        ((Mech)target).SetProne();
        var centerTorso = attacker.Parts[PartLocation.CenterTorso];
        centerTorso.TryAddComponent(new JumpJets()).ShouldBeTrue();
        centerTorso.TryAddComponent(new JumpJets()).ShouldBeTrue();
        var command = MoveCommand(Game.PhaseStepState!.Value.ActivePlayer.Id,
            [
                new PathSegment(Position(start), Position(middle), [new JumpMovementCost { Value = 1 }]),
                new PathSegment(Position(middle), Position(end), [new JumpMovementCost { Value = 1 }])
            ], MovementType.Jump, Dfa(target.Id));

        // Act
        _sut.HandleCommand(command);

        // Assert
        attacker.Position!.Coordinates.ShouldBe(middle);
        attacker.DeclaredPhysicalAttack.ShouldNotBeNull().AttackType.ShouldBe(PhysicalAttackType.DFA);
    }

    [Fact]
    public void HandleCommand_WhenDfaCannotClearTheTargetHeight_ShouldRejectTheDeclaration()
    {
        // Arrange - the target stands on a Level 3 hill and is two levels high, so the DFA needs
        // 5 Jumping MP even though the adjacent jump only costs 1 MP
        var (attacker, target, start, middle, end) = SetUpChargeOnTheMap();
        Game.BattleMap!.AddHex(new Hex(end, 3));
        var centerTorso = attacker.Parts[PartLocation.CenterTorso];
        centerTorso.TryAddComponent(new JumpJets()).ShouldBeTrue();
        centerTorso.TryAddComponent(new JumpJets()).ShouldBeTrue();
        var idempotencyKey = Guid.NewGuid();
        var command = MoveCommand(Game.PhaseStepState!.Value.ActivePlayer.Id,
            [
                new PathSegment(Position(start), Position(middle), [new JumpMovementCost { Value = 1 }]),
                new PathSegment(Position(middle), Position(end), [new JumpMovementCost { Value = 1 }])
            ], MovementType.Jump, Dfa(target.Id), idempotencyKey);

        // Act & Assert
        AssertDeclarationRejected(command, attacker, idempotencyKey);
    }

    [Fact]
    public void HandleCommand_WhenDfaCanClearTheTargetHeight_ShouldAcceptTheDeclaration()
    {
        // Arrange - 5 Jumping MP clear the Level 3 hill plus the two-level height of a standing Mech
        var (attacker, target, start, middle, end) = SetUpChargeOnTheMap();
        Game.BattleMap!.AddHex(new Hex(end, 3));
        attacker.Parts[PartLocation.CenterTorso].TryAddComponent(new JumpJets()).ShouldBeTrue();
        attacker.Parts[PartLocation.CenterTorso].TryAddComponent(new JumpJets()).ShouldBeTrue();
        attacker.Parts[PartLocation.LeftTorso].TryAddComponent(new JumpJets()).ShouldBeTrue();
        attacker.Parts[PartLocation.LeftTorso].TryAddComponent(new JumpJets()).ShouldBeTrue();
        attacker.Parts[PartLocation.RightTorso].TryAddComponent(new JumpJets()).ShouldBeTrue();
        var command = MoveCommand(Game.PhaseStepState!.Value.ActivePlayer.Id,
            [
                new PathSegment(Position(start), Position(middle), [new JumpMovementCost { Value = 1 }]),
                new PathSegment(Position(middle), Position(end), [new JumpMovementCost { Value = 1 }])
            ], MovementType.Jump, Dfa(target.Id));

        // Act
        _sut.HandleCommand(command);

        // Assert
        attacker.Position!.Coordinates.ShouldBe(middle);
        attacker.DeclaredPhysicalAttack.ShouldNotBeNull().AttackType.ShouldBe(PhysicalAttackType.DFA);
    }

    [Fact]
    public void HandleCommand_WhenTheAttackerAlreadyDeclared_ShouldRejectTheDeclaration()
    {
        // Arrange
        var (attacker, target, start, middle, end) = SetUpChargeOnTheMap();
        attacker.DeclarePhysicalAttack(new PhysicalAttackDeclarationData
        {
            AttackType = PhysicalAttackType.Punch,
            AttackerLimbs = [PartLocation.LeftArm],
            TargetId = target.Id
        });
        var idempotencyKey = Guid.NewGuid();
        var command = MoveCommand(Game.PhaseStepState!.Value.ActivePlayer.Id,
            [ClearStep(Position(start), Position(middle)), ClearStep(Position(middle), Position(end))],
            MovementType.Walk, Charge(target.Id), idempotencyKey);

        // Act & Assert
        _sut.HandleCommand(command);
        CommandPublisher.Received(1).PublishCommand(Arg.Is<ErrorCommand>(cmd => cmd.IdempotencyKey == idempotencyKey));
        attacker.DeclaredPhysicalAttack!.AttackType.ShouldBe(PhysicalAttackType.Punch);
    }

    [Fact]
    public void HandleCommand_WhenThePathEndsWithAFacingChange_ShouldAcceptTheCharge()
    {
        // Arrange - the path finder may append rotation segments after the unit enters the target
        var (attacker, target, start, middle, end) = SetUpChargeOnTheMap();
        var turnAtTarget = new PathSegment(
            new HexPosition(end, HexDirection.Top),
            new HexPosition(end, HexDirection.Bottom),
            [new RotationMovementCost { FromFacing = HexDirection.Top, ToFacing = HexDirection.Bottom, Value = 1 }]);
        var command = MoveCommand(Game.PhaseStepState!.Value.ActivePlayer.Id,
            [ClearStep(Position(start), Position(middle)), ClearStep(Position(middle), Position(end)), turnAtTarget],
            MovementType.Walk, Charge(target.Id));

        // Act
        _sut.HandleCommand(command);

        // Assert - the trailing turn keeps the unit in the approach hex, facing the target
        attacker.Position!.Coordinates.ShouldBe(middle);
        attacker.DeclaredPhysicalAttack.ShouldNotBeNull().HexesMoved.ShouldBe(1);
    }

    [Fact]
    public void HandleCommand_WhenTheMoveIsDeferred_ShouldCountTheCombinedPath()
    {
        // Arrange - the unit already executed part of its move and now continues it with a charge
        var (attacker, start, middle, end) = SetUpMapAndAttacker();
        var afterMid = end.GetNeighbour(HexDirection.Bottom);
        var targetHex = afterMid.GetNeighbour(HexDirection.Bottom);
        var declaredTarget = DeployMovedTarget(targetHex);
        attacker.Move(new MovementPath([ClearStep(Position(start), Position(middle))], MovementType.Walk),
            null, false);
        var command = MoveCommand(Game.PhaseStepState!.Value.ActivePlayer.Id,
            [ClearStep(Position(middle), Position(end)), ClearStep(Position(end), Position(afterMid)),
                ClearStep(Position(afterMid), Position(targetHex))],
            MovementType.Walk, Charge(declaredTarget.Id));

        // Act
        _sut.HandleCommand(command);

        // Assert - the executed and the continuation hexes count, the origin and the target do not
        attacker.DeclaredPhysicalAttack.ShouldNotBeNull().HexesMoved.ShouldBe(3);
    }

    [Fact]
    public void HandleCommand_WhenAnInterruptTruncatesTheMove_ShouldNotStoreTheDeclaration()
    {
        // Arrange - a run that skids on a road never completes the charge
        SetMap();
        _sut.Enter();
        var player = Game.PhaseStepState!.Value.ActivePlayer;
        var unit = (Mech)player.Units.Single(u => u.Id == _unit1Id);
        var start = new HexPosition(1, 2, HexDirection.Top);
        unit.Deploy(start, null);
        var roadHex = Game.BattleMap!.GetHex(new HexCoordinates(2, 2));
        roadHex!.AddTerrain(new RoadTerrain());
        var targetHex = new HexCoordinates(3, 2);
        var target = DeployMovedTarget(targetHex);
        var command = MoveCommand(player.Id,
            [
                new PathSegment(start, new HexPosition(2, 2, HexDirection.Top),
                    [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }]),
                new PathSegment(new HexPosition(2, 2, HexDirection.Top), new HexPosition(2, 2, HexDirection.Bottom),
                    [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 0 }]),
                new PathSegment(new HexPosition(2, 2, HexDirection.Bottom), new HexPosition(3, 2, HexDirection.Bottom),
                    [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }])
            ], MovementType.Run, Charge(target.Id));
        MockFallProcessor.ProcessMovementAttempt(unit,
                Arg.Is<SkidCheckRollContext>(c => c.SkidDistance == 1 && c.HexesMoved == 1),
                Game, MovementType.Run, Arg.Any<HexPosition?>())
            .Returns(new FallContextData
            {
                UnitId = unit.Id,
                GameId = Game.Id,
                IsFalling = true,
                PilotingSkillRoll = new PilotingSkillRollData
                {
                    RollContext = new SkidCheckRollContext(1, 3),
                    DiceResults = [2, 2],
                    IsSuccessful = false,
                    PsrBreakdown = new PsrBreakdown { BasePilotingSkill = 4, Modifiers = [] }
                },
                FallingDamageData = new FallingDamageData(
                    HexDirection.Top,
                    new HitLocationsData([], 5),
                    new DiceResult(3), HitDirection.Front
                )
            });

        // Act
        _sut.HandleCommand(command);

        // Assert
        CommandPublisher.Received(1).PublishCommand(Arg.Is<MoveUnitCommand>(cmd =>
            cmd.UnitId == _unit1Id && cmd.Declaration == null));
        unit.HasDeclaredPhysicalAttack.ShouldBeFalse();
    }

    [Fact]
    public void HandleCommand_WhenAValidDfaLandsInAHazard_ShouldKeepTheDeclaration()
    {
        // Arrange - the landing PSR fails after the declaration was applied
        SetMap();
        _sut.Enter();
        var player = Game.PhaseStepState!.Value.ActivePlayer;
        var unit = (Mech)player.Units.Single(u => u.Id == _unit1Id);
        var start = new HexCoordinates(1, 3);
        var middle = start.GetNeighbour(HexDirection.Top);
        var end = middle.GetNeighbour(HexDirection.Top);
        unit.Deploy(Position(start), null);
        var centerTorso = unit.Parts[PartLocation.CenterTorso];
        centerTorso.TryAddComponent(new JumpJets()).ShouldBeTrue();
        centerTorso.TryAddComponent(new JumpJets()).ShouldBeTrue();
        unit.GetAllComponents<Gyro>().First().Hit();
        var target = DeployMovedTarget(end);
        var fallContextData = new FallContextData
        {
            UnitId = unit.Id,
            GameId = Game.Id,
            IsFalling = true,
            PilotingSkillRoll = new PilotingSkillRollData
            {
                RollContext = new PilotingSkillRollContext(PilotingSkillRollType.JumpWithDamage),
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
        MockFallProcessor.ProcessMovementAttempt(unit,
                Arg.Is<PilotingSkillRollContext>(c => c.RollType == PilotingSkillRollType.JumpWithDamage),
                Game, MovementType.Jump)
            .Returns(fallContextData);
        var command = MoveCommand(player.Id,
            [
                new PathSegment(Position(start), Position(middle), [new JumpMovementCost { Value = 1 }]),
                new PathSegment(Position(middle), Position(end), [new JumpMovementCost { Value = 1 }])
            ], MovementType.Jump, Dfa(target.Id));

        // Act
        _sut.HandleCommand(command);

        // Assert - a landing hazard does not void the declaration
        CommandPublisher.Received().PublishCommand(Arg.Is<MechFallCommand>(cmd => cmd.UnitId == _unit1Id));
        unit.HasPendingMovementAttack.ShouldBeTrue();
        unit.Position!.Coordinates.ShouldBe(middle, "the attacker fell before entering the target hex");
    }
}
