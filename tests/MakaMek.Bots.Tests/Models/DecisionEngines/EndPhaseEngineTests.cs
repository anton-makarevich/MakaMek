using NSubstitute;
using Sanet.MakaMek.Bots.Models.DecisionEngines;
using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Data.Units.Components;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components.Engines;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Core.Models.Units.Pilots;

namespace Sanet.MakaMek.Bots.Tests.Models.DecisionEngines;

public class EndPhaseEngineTests
{
    private readonly IClientGame _clientGame;
    private readonly IPlayer _player;
    private readonly EndPhaseEngine _sut;

    public EndPhaseEngineTests()
    {
        _clientGame = Substitute.For<IClientGame>();
        _player = Substitute.For<IPlayer>();
        
        _clientGame.Id.Returns(Guid.NewGuid());
        _clientGame.Turn.Returns(5); // Set a default turn number
        _player.Id.Returns(Guid.NewGuid());
        _player.Name.Returns("Test Player");
        
        _sut = new EndPhaseEngine(_clientGame);
    }

    [Fact]
    public async Task MakeDecision_ShouldAlwaysEndTurn()
    {
        // Arrange
        _player.AliveUnits.Returns([]);
        
        // Act
        await _sut.MakeDecision(_player);
        
        // Assert
        await _clientGame.Received(1).EndTurn(Arg.Is<TurnEndedCommand>(cmd =>
            cmd.PlayerId == _player.Id &&
            cmd.GameOriginId == _clientGame.Id));
    }

    [Fact]
    public async Task MakeDecision_WhenShutdownUnits_ShouldAttemptStartup()
    {
        // Arrange
        var shutdownUnit = CreateTestMech(isShutdown: true, currentHeat: 15);
        _player.AliveUnits.Returns([shutdownUnit]);
        
        // Act
        await _sut.MakeDecision(_player);
        
        // Assert
        await _clientGame.Received(1).StartupUnit(Arg.Is<StartupUnitCommand>(cmd =>
            cmd.PlayerId == _player.Id &&
            cmd.UnitId == shutdownUnit.Id &&
            cmd.GameOriginId == _clientGame.Id));
        
        await _clientGame.Received(1).EndTurn(Arg.Any<TurnEndedCommand>());
    }

    [Fact]
    public async Task MakeDecision_WhenOverheatedUnits_ShouldShutdownUnit()
    {
        // Arrange
        var overheatedUnit = CreateTestMech(isShutdown: false, currentHeat: 30);
        _player.AliveUnits.Returns([overheatedUnit]);
        
        // Act
        await _sut.MakeDecision(_player);
        
        // Assert
        await _clientGame.Received(1).ShutdownUnit(Arg.Is<ShutdownUnitCommand>(cmd =>
            cmd.PlayerId == _player.Id &&
            cmd.UnitId == overheatedUnit.Id &&
            cmd.GameOriginId == _clientGame.Id));
        
        await _clientGame.Received(1).EndTurn(Arg.Any<TurnEndedCommand>());
    }

    [Fact]
    public async Task MakeDecision_WhenNormalHeatUnits_ShouldNotShutdown()
    {
        // Arrange
        var normalUnit = CreateTestMech(isShutdown: false, currentHeat: 20);
        _player.AliveUnits.Returns([normalUnit]);
        
        // Act
        await _sut.MakeDecision(_player);
        
        // Assert
        await _clientGame.DidNotReceive().ShutdownUnit(Arg.Any<ShutdownUnitCommand>());
        await _clientGame.Received(1).EndTurn(Arg.Any<TurnEndedCommand>());
    }

    [Fact]
    public async Task MakeDecision_WhenExceptionInHandlers_ShouldStillEndTurn()
    {
        // Arrange
        _player.AliveUnits.Returns((IReadOnlyList<IUnit>?)null!); // This will cause an exception
        
        // Act
        await _sut.MakeDecision(_player);
        
        // Assert - Should still attempt to end turn even if other operations fail
        await _clientGame.Received(1).EndTurn(Arg.Any<TurnEndedCommand>());
    }

    [Fact]
    public async Task MakeDecision_WhenMultipleUnitTypes_ShouldHandleAll()
    {
        // Arrange
        var shutdownUnit = CreateTestMech(isShutdown: true, currentHeat: 15);
        var overheatedUnit = CreateTestMech(isShutdown: false, currentHeat: 30);
        var normalUnit = CreateTestMech(isShutdown: false, currentHeat: 10);
        
        _player.AliveUnits.Returns([shutdownUnit, overheatedUnit, normalUnit]);
        
        // Act
        await _sut.MakeDecision(_player);
        
        // Assert
        await _clientGame.Received(1).StartupUnit(Arg.Is<StartupUnitCommand>(cmd =>
            cmd.UnitId == shutdownUnit.Id));
        
        await _clientGame.Received(1).ShutdownUnit(Arg.Is<ShutdownUnitCommand>(cmd =>
            cmd.UnitId == overheatedUnit.Id));
        
        await _clientGame.Received(1).EndTurn(Arg.Any<TurnEndedCommand>());
    }

    private static List<UnitPart> CreateBasicPartsData(int engineRating = 100)
    {
        var engineData = new ComponentData
        {
            Type = MakaMekComponent.Engine,
            Assignments =
            [
                new LocationSlotAssignment(PartLocation.CenterTorso, 0, 3),
                new LocationSlotAssignment(PartLocation.CenterTorso, 7, 3)
            ],
            SpecificData = new EngineStateData(EngineType.Fusion, engineRating)
        };
        var centerTorso = new CenterTorso("CenterTorso", 31, 10, 6);
        centerTorso.TryAddComponent(new Engine(engineData), [0, 1, 2, 7, 8, 9]);
        return
        [
            new Head("Head", 9, 3),
            centerTorso,
            new SideTorso("LeftTorso", PartLocation.LeftTorso, 25, 8, 6),
            new SideTorso("RightTorso", PartLocation.RightTorso, 25, 8, 6),
            new Arm("RightArm", PartLocation.RightArm, 17, 6),
            new Arm("LeftArm", PartLocation.LeftArm, 17, 6),
            new Leg("RightLeg", PartLocation.RightLeg, 25, 8),
            new Leg("LeftLeg", PartLocation.LeftLeg, 25, 8)
        ];
    }

    private Mech CreateTestMech(bool isShutdown, int currentHeat)
    {
        var mech = new Mech("Test", "TST-1A", 50, CreateBasicPartsData());
        
        // Assign a conscious pilot so the mech can attempt startup
        var pilot = new MechWarrior("Test", "Pilot");
        mech.AssignPilot(pilot);
        
        // Set heat
        if (currentHeat > 0)
        {
            var heatData = new HeatData
            {
                MovementHeatSources = [new MovementHeatData { MovementType = MovementType.Walk, MovementPointsSpent = 0, HeatPoints = currentHeat }],
                WeaponHeatSources = [],
                ExternalHeatSources = [],
                DissipationData = new HeatDissipationData { HeatSinks = 0, EngineHeatSinks = 0, DissipationPoints = 0 }
            };
            mech.ApplyHeat(heatData);
        }
        
        // Set shutdown state
        if (!isShutdown) return mech;
        var shutdownData = new ShutdownData 
        { 
            Reason = ShutdownReason.Voluntary, 
            Turn = _clientGame.Turn - 1 // Set to previous turn so startup is possible
        };
        mech.Shutdown(shutdownData);

        return mech;
    }
}
