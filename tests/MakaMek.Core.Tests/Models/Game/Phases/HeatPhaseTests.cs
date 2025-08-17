using NSubstitute;
using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Models.Game.Phases;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components;
using Sanet.MakaMek.Core.Models.Units.Components.Engines;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Phases;

public class HeatPhaseTests : GamePhaseTestsBase
{
    private readonly HeatPhase _sut;
    private readonly Guid _player1Id = Guid.NewGuid();
    private readonly Guid _player2Id = Guid.NewGuid();
    private readonly Guid _unit1Id;
    private readonly Guid _unit2Id;
    private readonly Unit _unit1;
    private readonly Unit _unit2;
    private readonly IGamePhase _mockNextPhase;

    public HeatPhaseTests()
    {
        // Create mock next phase and configure the phase manager
        _mockNextPhase = Substitute.For<IGamePhase>();
        MockPhaseManager.GetNextPhase(PhaseNames.Heat, Game).Returns(_mockNextPhase);
        
        _sut = new HeatPhase(Game);

        // Add two players with units
        Game.HandleCommand(CreateJoinCommand(_player1Id, "Player 1"));
        Game.HandleCommand(CreateJoinCommand(_player2Id, "Player 2"));
        Game.HandleCommand(CreateStatusCommand(_player1Id, PlayerStatus.Ready));
        Game.HandleCommand(CreateStatusCommand(_player2Id, PlayerStatus.Ready));

        // Get unit IDs and references
        var player1 = Game.Players[0];
        _unit1 = player1.Units[0];
        _unit1Id = _unit1.Id;

        var player2 = Game.Players[1];
        _unit2 = player2.Units[0];
        _unit2Id = _unit2.Id;

        // Set initiative order
        Game.SetInitiativeOrder(new List<IPlayer> { player2, player1 });

        // Deploy units to the map
        Game.HandleCommand(CreateDeployCommand(_player1Id, _unit1Id, 1, 1, 0)); // 0 = Forward direction
        Game.HandleCommand(CreateDeployCommand(_player2Id, _unit2Id, 3, 3, 0)); // 0 = Forward direction

        // Clear any commands published during setup
        CommandPublisher.ClearReceivedCalls();
    }

    [Fact]
    public void Enter_ShouldPublishAutomaticRestart_ForPreviouslyHeatShutdownMech()
    {
        // Arrange: rebuild game to use MockHeatEffectsCalculator
        SetGameWithRulesProvider(new ClassicBattletechRulesProvider());

        // Set up players and a single unit
        var playerId = Guid.NewGuid();
        Game.HandleCommand(CreateJoinCommand(playerId, "P1"));
        Game.HandleCommand(CreateStatusCommand(playerId, PlayerStatus.Ready));

        var mech = Game.Players[0].Units[0] as Mech;
        mech.ShouldNotBeNull();

        // Make the mech shutdown due to heat in the previous turn
        mech.Shutdown(new ShutdownData { Reason = ShutdownReason.Heat, Turn = Game.Turn-1 });

        // Mock automatic restart attempt
        var expectedRestart = new UnitStartupCommand
        {
            UnitId = mech.Id,
            IsAutomaticRestart = true,
            IsRestartPossible = true,
            AvoidShutdownRoll = null,
            GameOriginId = Guid.Empty
        };
        MockHeatEffectsCalculator
            .AttemptRestart(mech, Game.Turn)
            .Returns(expectedRestart);

        // Initiative and deploy so heat processing runs
        Game.SetInitiativeOrder(new List<IPlayer> { Game.Players[0] });
        Game.HandleCommand(CreateDeployCommand(playerId, mech.Id, 1, 1, 0));

        // New phase instance bound to rebuilt Game
        var sut = new HeatPhase(Game);

        // Clear noise
        CommandPublisher.ClearReceivedCalls();

        // Act
        sut.Enter();

        // Assert: restart command published for the mech
        CommandPublisher.Received().PublishCommand(
            Arg.Is<UnitStartupCommand>(cmd => 
                cmd.UnitId == mech.Id
                && cmd.GameOriginId == Game.Id));
    }
    
    [Fact]
    public void Enter_ShouldNotPublishAutomaticRestart_WhenMechIsNotShutdown()
    {
        // Arrange: rebuild game to use MockHeatEffectsCalculator
        SetGameWithRulesProvider(new ClassicBattletechRulesProvider());

        // Set up players and a single unit
        var playerId = Guid.NewGuid();
        Game.HandleCommand(CreateJoinCommand(playerId, "P1"));
        Game.HandleCommand(CreateStatusCommand(playerId, PlayerStatus.Ready));

        var mech = Game.Players[0].Units[0] as Mech;
        mech.ShouldNotBeNull();

        // Mock automatic restart attempt
        var expectedRestart = new UnitStartupCommand
        {
            UnitId = mech.Id,
            IsAutomaticRestart = true,
            IsRestartPossible = true,
            AvoidShutdownRoll = null,
            GameOriginId = Guid.Empty
        };
        MockHeatEffectsCalculator
            .AttemptRestart(mech, Game.Turn)
            .Returns(expectedRestart);

        // Initiative and deploy so heat processing runs
        Game.SetInitiativeOrder(new List<IPlayer> { Game.Players[0] });
        Game.HandleCommand(CreateDeployCommand(playerId, mech.Id, 1, 1, 0));

        // New phase instance bound to rebuilt Game
        var sut = new HeatPhase(Game);

        // Clear noise
        CommandPublisher.ClearReceivedCalls();

        // Act
        sut.Enter();

        // Assert
        CommandPublisher.DidNotReceive().PublishCommand(Arg.Any<UnitStartupCommand>());
    }
    
    [Fact]
    public void Enter_ShouldNotPublishAutomaticRestart_WhenMechIsShutdownInSameTurn()
    {
        // Arrange: rebuild game to use MockHeatEffectsCalculator
        SetGameWithRulesProvider(new ClassicBattletechRulesProvider());

        // Set up players and a single unit
        var playerId = Guid.NewGuid();
        Game.HandleCommand(CreateJoinCommand(playerId, "P1"));
        Game.HandleCommand(CreateStatusCommand(playerId, PlayerStatus.Ready));

        var mech = Game.Players[0].Units[0] as Mech;
        mech.ShouldNotBeNull();
        mech.Shutdown(new ShutdownData { Reason = ShutdownReason.Heat, Turn = Game.Turn });

        // Mock automatic restart attempt
        var expectedRestart = new UnitStartupCommand
        {
            UnitId = mech.Id,
            IsAutomaticRestart = true,
            IsRestartPossible = true,
            AvoidShutdownRoll = null,
            GameOriginId = Guid.Empty
        };
        MockHeatEffectsCalculator
            .AttemptRestart(mech, Game.Turn)
            .Returns(expectedRestart);

        // Initiative and deploy so heat processing runs
        Game.SetInitiativeOrder(new List<IPlayer> { Game.Players[0] });
        Game.HandleCommand(CreateDeployCommand(playerId, mech.Id, 1, 1, 0));

        // New phase instance bound to rebuilt Game
        var sut = new HeatPhase(Game);

        // Clear noise
        CommandPublisher.ClearReceivedCalls();

        // Act
        sut.Enter();

        // Assert
        CommandPublisher.DidNotReceive().PublishCommand(Arg.Any<UnitStartupCommand>());
    }
    
    [Fact]
    public void Enter_ShouldNotPublishAutomaticRestart_WhenMechIsShutdownVoluntarily()
    {
        // Arrange: rebuild game to use MockHeatEffectsCalculator
        SetGameWithRulesProvider(new ClassicBattletechRulesProvider());

        // Set up players and a single unit
        var playerId = Guid.NewGuid();
        Game.HandleCommand(CreateJoinCommand(playerId, "P1"));
        Game.HandleCommand(CreateStatusCommand(playerId, PlayerStatus.Ready));

        var mech = Game.Players[0].Units[0] as Mech;
        mech.ShouldNotBeNull();
        mech.Shutdown(new ShutdownData { Reason = ShutdownReason.Voluntary, Turn = Game.Turn-1 });

        // Mock automatic restart attempt
        var expectedRestart = new UnitStartupCommand
        {
            UnitId = mech.Id,
            IsAutomaticRestart = true,
            IsRestartPossible = true,
            AvoidShutdownRoll = null,
            GameOriginId = Guid.Empty
        };
        MockHeatEffectsCalculator
            .AttemptRestart(mech, Game.Turn)
            .Returns(expectedRestart);

        // Initiative and deploy so heat processing runs
        Game.SetInitiativeOrder(new List<IPlayer> { Game.Players[0] });
        Game.HandleCommand(CreateDeployCommand(playerId, mech.Id, 1, 1, 0));

        // New phase instance bound to rebuilt Game
        var sut = new HeatPhase(Game);

        // Clear noise
        CommandPublisher.ClearReceivedCalls();

        // Act
        sut.Enter();

        // Assert
        CommandPublisher.DidNotReceive().PublishCommand(Arg.Any<UnitStartupCommand>());
    }
    
    [Fact]
    public void Enter_ShouldNotPublishAutomaticRestart_WhenNotReturnedByCalculator()
    {
        // Arrange: rebuild game to use MockHeatEffectsCalculator
        SetGameWithRulesProvider(new ClassicBattletechRulesProvider());

        // Set up players and a single unit
        var playerId = Guid.NewGuid();
        Game.HandleCommand(CreateJoinCommand(playerId, "P1"));
        Game.HandleCommand(CreateStatusCommand(playerId, PlayerStatus.Ready));

        var mech = Game.Players[0].Units[0] as Mech;
        mech.ShouldNotBeNull();

        // Make the mech shutdown due to heat in the previous turn
        mech.Shutdown(new ShutdownData { Reason = ShutdownReason.Heat, Turn = Game.Turn-1 });

        // Mock automatic restart attempt
        UnitStartupCommand? expectedRestart = null;
        MockHeatEffectsCalculator
            .AttemptRestart(mech, Game.Turn)
            .Returns(expectedRestart);

        // Initiative and deploy so heat processing runs
        Game.SetInitiativeOrder(new List<IPlayer> { Game.Players[0] });
        Game.HandleCommand(CreateDeployCommand(playerId, mech.Id, 1, 1, 0));

        // New phase instance bound to rebuilt Game
        var sut = new HeatPhase(Game);

        // Clear noise
        CommandPublisher.ClearReceivedCalls();

        // Act
        sut.Enter();

        // Assert: restart command is not published
        CommandPublisher.DidNotReceive().PublishCommand(Arg.Any<UnitStartupCommand>());
    }

    [Fact]
    public void Enter_ShouldPublishShutdownMech_WhenHeatShutdownCommandReturned()
    {
        // Arrange: rebuild game to use MockHeatEffectsCalculator
        SetGameWithRulesProvider(new ClassicBattletechRulesProvider());

        var playerId = Guid.NewGuid();
        Game.HandleCommand(CreateJoinCommand(playerId, "P1"));
        Game.HandleCommand(CreateStatusCommand(playerId, PlayerStatus.Ready));

        var mech = Game.Players[0].Units[0] as Mech;
        mech.ShouldNotBeNull();

        // Mock heat shutdown command to be returned by calculator
        var shutdownData = new ShutdownData { Reason = ShutdownReason.Heat, Turn = Game.Turn };
        var expectedShutdown = new UnitShutdownCommand
        {
            UnitId = mech.Id,
            ShutdownData = shutdownData,
            IsAutomaticShutdown = true,
            AvoidShutdownRoll = null,
            GameOriginId = Guid.Empty // will be set by phase
        };
        MockHeatEffectsCalculator
            .CheckForHeatShutdown(mech, Game.Turn)
            .Returns(expectedShutdown);

        Game.SetInitiativeOrder(new List<IPlayer> { Game.Players[0] });
        Game.HandleCommand(CreateDeployCommand(playerId, mech.Id, 1, 1, 0));

        var sut = new HeatPhase(Game);

        CommandPublisher.ClearReceivedCalls();

        // Act
        sut.Enter();

        // Assert: command published with GameOriginId set and unit is shutdown
        CommandPublisher.Received().PublishCommand(
            Arg.Is<UnitShutdownCommand>(cmd => cmd.UnitId == mech.Id && cmd.GameOriginId == Game.Id));

        mech.IsShutdown.ShouldBeTrue();
        mech.CurrentShutdownData.ShouldNotBeNull();
        mech.CurrentShutdownData!.Value.Reason.ShouldBe(ShutdownReason.Heat);
    }
    
    [Fact]
    public void Enter_ShouldNotPublishShutdownMech_WhenHeatShutdownCommandIsNotReturned()
    {
        // Arrange: rebuild game to use MockHeatEffectsCalculator
        SetGameWithRulesProvider(new ClassicBattletechRulesProvider());

        var playerId = Guid.NewGuid();
        Game.HandleCommand(CreateJoinCommand(playerId, "P1"));
        Game.HandleCommand(CreateStatusCommand(playerId, PlayerStatus.Ready));

        var mech = Game.Players[0].Units[0] as Mech;
        mech.ShouldNotBeNull();

        // Mock heat shutdown command to be returned by calculator
        UnitShutdownCommand? expectedShutdown = null;
        MockHeatEffectsCalculator
            .CheckForHeatShutdown(mech, Game.Turn)
            .Returns(expectedShutdown);

        Game.SetInitiativeOrder(new List<IPlayer> { Game.Players[0] });
        Game.HandleCommand(CreateDeployCommand(playerId, mech.Id, 1, 1, 0));

        var sut = new HeatPhase(Game);

        CommandPublisher.ClearReceivedCalls();

        // Act
        sut.Enter();

        // Assert: command published with GameOriginId set and unit is shutdown
        CommandPublisher.DidNotReceive().PublishCommand(Arg.Any<UnitShutdownCommand>());
    }

    [Fact]
    public void Enter_ShouldProcessHeatForAllUnits_AndTransitionToNextPhase()
    {
        // Arrange
        // Setup units with heat sources
        SetupUnitWithMovement(_unit1, MovementType.Run);
        _unit2.Deploy(new HexPosition(1, 1, HexDirection.Bottom));
        SetupUnitWithWeaponFired(_unit2);

        // Act
        _sut.Enter();

        // Assert
        // Verify heat updated commands were published for both units
        CommandPublisher.Received().PublishCommand(
            Arg.Is<HeatUpdatedCommand>(cmd => 
                cmd.UnitId == _unit1Id || cmd.UnitId == _unit2Id));

        // Verify transition to next phase
        MockPhaseManager.Received(1).GetNextPhase(PhaseNames.Heat, Game);
        _mockNextPhase.Received(1).Enter();
    }

    [Fact]
    public void Enter_WithHeatAmmoExplosionThreshold_ShouldCheckForAmmoExplosion()
    {
        // Arrange
        var mech = _unit1 as Mech;
        SetupUnitWithHighHeat(mech!, 25); // Heat level that triggers ammo explosion check

        var explosionCommand = new AmmoExplosionCommand
        {
            UnitId = _unit1Id,
            AvoidExplosionRoll = new AvoidAmmoExplosionRollData
            {
                HeatLevel = 25,
                DiceResults = [3, 4],
                AvoidNumber = 6,
                IsSuccessful = true
            },
            ExplosionDamage = [],
            GameOriginId = Guid.Empty
        };

        MockHeatEffectsCalculator.CheckForHeatAmmoExplosion(mech!)
            .Returns(explosionCommand);

        // Act
        _sut.Enter();

        // Assert
        MockHeatEffectsCalculator.Received(1).CheckForHeatAmmoExplosion(mech!);
        CommandPublisher.Received(1).PublishCommand(
            Arg.Is<AmmoExplosionCommand>(cmd => cmd.UnitId == _unit1Id));
    }

    [Fact]
    public void Enter_WithNoAmmoExplosionThreshold_ShouldNotCheckForAmmoExplosion()
    {
        // Arrange
        var mech = _unit1 as Mech;
        SetupUnitWithLowHeat(mech!, 10); // Heat level below ammo explosion threshold

        MockHeatEffectsCalculator.CheckForHeatAmmoExplosion(mech!)
            .Returns((AmmoExplosionCommand?)null);

        // Act
        _sut.Enter();

        // Assert
        MockHeatEffectsCalculator.Received(1).CheckForHeatAmmoExplosion(mech!);
        CommandPublisher.DidNotReceive().PublishCommand(Arg.Any<AmmoExplosionCommand>());
    }

    [Fact]
    public void Enter_WithMovementHeat_ShouldCalculateAndApplyCorrectHeat()
    {
        // Arrange
        SetupUnitWithMovement(_unit1, MovementType.Run);

        // Act
        _sut.Enter();

        // Assert
        // Verify heat updated command was published with a correct movement heat source
        CommandPublisher.Received(1).PublishCommand(
            Arg.Is<HeatUpdatedCommand>(cmd => 
                cmd.UnitId == _unit1Id && 
                cmd.HeatData.MovementHeatSources.Count == 1 &&
                cmd.HeatData.MovementHeatSources[0].MovementType == MovementType.Run &&
                cmd.HeatData.MovementHeatSources[0].HeatPoints == 2)); // Run generates 2 heat points
    }

    [Fact]
    public void Enter_WithWeaponHeat_ShouldCalculateAndApplyCorrectHeat()
    {
        // Arrange
        _unit2.Deploy(new HexPosition(1, 1, HexDirection.Bottom));
        SetupUnitWithWeaponFired(_unit2);

        // Act
        _sut.Enter();

        // Assert
        // Verify heat updated command was published with a correct weapon heat source
        CommandPublisher.Received(1).PublishCommand(
            Arg.Is<HeatUpdatedCommand>(cmd => 
                cmd.UnitId == _unit2Id && 
                cmd.HeatData.WeaponHeatSources.Count == 1 &&
                cmd.HeatData.WeaponHeatSources[0].WeaponName == "Medium Laser" &&
                cmd.HeatData.WeaponHeatSources[0].HeatPoints == 3));
    }

    [Fact]
    public void Enter_WithHeatDissipation_ShouldApplyCorrectDissipation()
    {
        // Arrange
        // Add initial heat to unit
        _unit1.ApplyHeat(new HeatData
        {
            MovementHeatSources = [],
            WeaponHeatSources = [
            new WeaponHeatData
            {
                WeaponName = "test",
                HeatPoints = 15
            }
            ],
            DissipationData = new HeatDissipationData
            {
                HeatSinks = 0,
                EngineHeatSinks = 0,
                DissipationPoints = 0
            }
        });
        var initialHeat = _unit1.CurrentHeat;
        _unit1.ResetTurnState();

        // Act
        _sut.Enter();

        // Assert
        // Verify heat was dissipated
        _unit1.CurrentHeat.ShouldBeLessThan(initialHeat);

        // Verify heat updated command was published with correct dissipation data
        CommandPublisher.Received().PublishCommand(
            Arg.Is<HeatUpdatedCommand>(cmd => 
                cmd.UnitId == _unit1Id && 
                cmd.HeatData.DissipationData.HeatSinks == _unit1.GetAllComponents<HeatSink>().Count() &&
                cmd.HeatData.DissipationData.EngineHeatSinks == 10 &&
                cmd.HeatData.DissipationData.DissipationPoints > 0));
    }

    [Fact]
    public void Enter_WithNoHeatSources_ShouldStillPublishHeatUpdatedCommand()
    {
        // Arrange
        // No heat sources or movement for units

        // Act
        _sut.Enter();

        // Assert
        // Verify heat updated commands were still published for both units
        CommandPublisher.Received(2).PublishCommand(
            Arg.Is<HeatUpdatedCommand>(cmd => 
                (cmd.UnitId == _unit1Id || cmd.UnitId == _unit2Id) &&
                cmd.HeatData.MovementHeatSources.Count == 0 &&
                cmd.HeatData.WeaponHeatSources.Count == 0));
    }

    [Fact]
    public void Enter_WithCombinedHeatSources_ShouldCalculateCorrectTotalHeat()
    {
        // Arrange
        // Setup unit with both movement and weapon heat
        SetupUnitWithMovement(_unit1, MovementType.Run);
        SetupUnitWithWeaponFired(_unit1);
        
        // Act
        _sut.Enter();

        // Assert
        // Verify heat updated command was published with correct heat sources
        CommandPublisher.Received(1).PublishCommand(
            Arg.Is<HeatUpdatedCommand>(cmd => 
                cmd.UnitId == _unit1Id && 
                cmd.HeatData.MovementHeatSources.Count == 1 &&
                cmd.HeatData.MovementHeatSources[0].MovementType == MovementType.Run &&
                cmd.HeatData.WeaponHeatSources.Count == 1));
    }

    [Fact]
    public void Enter_WithEngineDamage_ShouldIncludeEngineHeatSource()
    {
        // Arrange
        // Setup unit with engine damage
        SetupUnitWithEngineDamage(_unit1, 1); // 1 hit = 5 heat points
        
        // Act
        _sut.Enter();
        
        // Assert
        // Verify heat updated command was published with an engine heat source
        CommandPublisher.Received(1).PublishCommand(
            Arg.Is<HeatUpdatedCommand>(cmd => 
                cmd.UnitId == _unit1Id && 
                cmd.HeatData.EngineHeatSource != null &&
                cmd.HeatData.EngineHeatSource.EngineHits == 1 &&
                cmd.HeatData.EngineHeatSource.Value == 5));
    }
    
    [Fact]
    public void Enter_WithEngineDamageAndOtherHeatSources_ShouldCalculateCorrectTotalHeat()
    {
        // Arrange
        // Setup unit with engine damage, movement and weapon heat
        SetupUnitWithEngineDamage(_unit1, 1); // 1 hit = 5 heat points
        SetupUnitWithMovement(_unit1, MovementType.Run); // 2 heat points
        SetupUnitWithWeaponFired(_unit1); // 3 heat points from medium laser
        
        // Act
        _sut.Enter();
        
        // Assert
        // Verify heat updated command was published with correct total heat
        CommandPublisher.Received(1).PublishCommand(
            Arg.Is<HeatUpdatedCommand>(cmd => 
                cmd.UnitId == _unit1Id && 
                cmd.HeatData.TotalHeatPoints == 10)); // 5 + 2 + 3 = 10
    }
    
    [Fact]
    public void Enter_ShouldSetConsciousness_AndPublishCommand_WhenCalculatorReturnsIt()
    {
        // Arrange
        var unit = Game.Players.First(p => p.Id == _player1Id).Units[0];
        var pilot = unit.Pilot;
        pilot!.IsConscious.ShouldBeTrue();
        var consciousnessCommand = new PilotConsciousnessRollCommand
        {
            GameOriginId = Guid.NewGuid(),
            PilotId = pilot.Id,
            UnitId = unit.Id,
            IsRecoveryAttempt = false,
            ConsciousnessNumber = 4,
            DiceResults = [7, 2],
            IsSuccessful = false 
        };
        MockConsciousnessCalculator.MakeConsciousnessRolls(pilot).Returns([consciousnessCommand]);
        // Act
        _sut.Enter();
        
        // Assert
        pilot.IsConscious.ShouldBeFalse();
        CommandPublisher.Received(1).PublishCommand(
            Arg.Is<PilotConsciousnessRollCommand>(cmd => 
                cmd.GameOriginId == Game.Id &&
                cmd.IsRecoveryAttempt == false &&
                cmd.IsSuccessful == false));
    }
    
    [Fact]
    public void Enter_ShouldNotSetConsciousness_AndNotPublishCommand_WhenCalculatorDoesntReturnAny()
    {
        // Arrange
        var unit = Game.Players.First(p => p.Id == _player1Id).Units[0];
        var pilot = unit.Pilot;
        pilot!.IsConscious.ShouldBeTrue();
        MockConsciousnessCalculator.MakeConsciousnessRolls(pilot).Returns([]);
        // Act
        _sut.Enter();
        
        // Assert
        pilot.IsConscious.ShouldBeTrue();
        CommandPublisher.DidNotReceive().PublishCommand(Arg.Any<PilotConsciousnessRollCommand>());
    }
    
    private static void SetupUnitWithMovement(Unit unit, MovementType movementType)
    {
        var deployPosition = new HexPosition(new HexCoordinates(1,1), HexDirection.Bottom);
        unit.Deploy(deployPosition);
        unit.Move(movementType, [new PathSegmentData
            {
                From = deployPosition.ToData(),
                To = deployPosition.ToData(),
                Cost = 0
            }
        ]);
    }

    private void SetupUnitWithWeaponFired(Unit unit)
    {
        // Find a weapon on the unit or add one if needed
        var weapon = unit.GetAllComponents<Weapon>().First(w=>w.Heat>0);
        
        // If a weapon exists, set its target
        unit.DeclareWeaponAttack([
            new WeaponTargetData
            {
                Weapon = weapon.ToData(),
                TargetId = _unit2Id,
                IsPrimaryTarget = true
            }
        ]);
    }
    
    private static void SetupUnitWithEngineDamage(Unit unit, int hits)
    {
        // Find the engine component on the unit
        var engine = unit.GetAllComponents<Engine>().FirstOrDefault();

        // If engine exists, apply hits
        if (engine != null)
        {
            for (var i = 0; i < hits; i++)
            {
                engine.Hit();
            }
        }
    }

    private static void SetupUnitWithHighHeat(Mech mech, int heatLevel)
    {
        // Use reflection to set the current heat since there's no public setter
        var heatProperty = typeof(Mech).GetProperty("CurrentHeat");
        heatProperty?.SetValue(mech, heatLevel);
    }

    private static void SetupUnitWithLowHeat(Mech mech, int heatLevel)
    {
        // Use reflection to set the current heat since there's no public setter
        var heatProperty = typeof(Mech).GetProperty("CurrentHeat");
        heatProperty?.SetValue(mech, heatLevel);
    }
}
