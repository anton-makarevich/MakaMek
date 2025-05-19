using NSubstitute;
using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Models.Game.Commands.Server;
using Sanet.MakaMek.Core.Models.Game.Phases;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components;
using Sanet.MakaMek.Core.Models.Units.Components.Engines;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;
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
    public void Enter_ShouldProcessHeatForAllUnits_AndTransitionToNextPhase()
    {
        // Arrange
        // Setup units with heat sources
        SetupUnitWithMovement(_unit1, MovementType.Run);
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
    public void Enter_WithMovementHeat_ShouldCalculateAndApplyCorrectHeat()
    {
        // Arrange
        SetupUnitWithMovement(_unit1, MovementType.Run);

        // Act
        _sut.Enter();

        // Assert
        // Verify heat updated command was published with correct movement heat source
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
        SetupUnitWithWeaponFired(_unit2);

        // Act
        _sut.Enter();

        // Assert
        // Verify heat updated command was published with correct weapon heat source
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
        // Verify heat updated command was published with engine heat source
        CommandPublisher.Received(1).PublishCommand(
            Arg.Is<HeatUpdatedCommand>(cmd => 
                cmd.UnitId == _unit1Id && 
                cmd.HeatData.EngineHeatSource != null &&
                cmd.HeatData.EngineHeatSource.Value.Hits == 1 &&
                cmd.HeatData.EngineHeatSource.Value.HeatPoints == 5));
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

    #region Helper Methods

    private void SetupUnitWithMovement(Unit unit, MovementType movementType)
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
        
        // If a weapon exists, just set its target
        weapon.Target = _unit2;
    }
    
    private void SetupUnitWithEngineDamage(Unit unit, int hits)
    {
        // Find the engine component on the unit
        var engine = unit.GetAllComponents<Engine>().FirstOrDefault();
        
        // If engine exists, apply hits
        if (engine != null)
        {
            for (int i = 0; i < hits; i++)
            {
                engine.Hit();
            }
        }
    }
    #endregion
}
