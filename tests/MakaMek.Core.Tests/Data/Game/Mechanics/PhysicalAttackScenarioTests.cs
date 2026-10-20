using NSubstitute;
using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Pilots;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Models.MovementCosts;
using Sanet.MakaMek.Map.Models.Terrains;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Data.Game.Mechanics;

public class PhysicalAttackScenarioTests
{
    private static readonly MovementPath WalkingPath = new([
            new PathSegment(
                new HexPosition(1, 1, HexDirection.Top),
                new HexPosition(1, 2, HexDirection.Top),
                [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }]),
            new PathSegment(
                new HexPosition(1, 2, HexDirection.Top),
                new HexPosition(1, 3, HexDirection.Top),
                [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }])
        ], MovementType.Walk);

    private static IUnit CreateUnit(
        IPilot? pilot = null,
        HexPosition? position = null,
        MovementPath? movement = null)
    {
        var unit = Substitute.For<IUnit>();
        unit.Pilot.Returns(pilot);
        unit.Position.Returns(position);
        unit.MovementTaken.Returns(movement);
        return unit;
    }

    [Fact]
    public void FromUnits_WithValidUnits_CreatesScenarioWithCorrectValues()
    {
        // Arrange
        var pilot = Substitute.For<IPilot>();
        pilot.Gunnery.Returns(4);
        pilot.Piloting.Returns(5);

        var attackerPosition = new HexPosition(1, 1, HexDirection.Top);
        var targetPosition = new HexPosition(new HexCoordinates(5, 5), HexDirection.Bottom);
        var attacker = CreateUnit(pilot, attackerPosition, WalkingPath);
        var target = CreateUnit(Substitute.For<IPilot>(), targetPosition, WalkingPath);

        // Act
        var scenario = PhysicalAttackScenario.FromUnits(attacker, target, PhysicalAttackType.Punch);

        // Assert
        scenario.AttackerPiloting.ShouldBe(5);
        scenario.AttackType.ShouldBe(PhysicalAttackType.Punch);
        scenario.AttackerPosition.ShouldBe(attackerPosition);
        scenario.TargetPosition.ShouldBe(targetPosition);
        scenario.AttackerMovementType.ShouldBe(MovementType.Walk);
        scenario.TargetHexesMoved.ShouldBe(2);
    }

    [Fact]
    public void FromUnits_UsesPilotingNotGunnery()
    {
        // Arrange
        var pilot = Substitute.For<IPilot>();
        pilot.Gunnery.Returns(3);
        pilot.Piloting.Returns(6);

        var singlePath = MovementPath.CreateSingleSegmentPath(new HexPosition(1, 1, HexDirection.Top));
        var attacker = CreateUnit(pilot, new HexPosition(1, 1, HexDirection.Top), singlePath);
        var target = CreateUnit(Substitute.For<IPilot>(), new HexPosition(1, 2, HexDirection.Top), singlePath);

        // Act
        var scenario = PhysicalAttackScenario.FromUnits(attacker, target, PhysicalAttackType.Kick);

        // Assert
        scenario.AttackerPiloting.ShouldBe(6);
    }

    [Fact]
    public void FromUnits_ReturnsHeights()
    {
        // Arrange
        var singlePath = MovementPath.CreateSingleSegmentPath(new HexPosition(1, 1, HexDirection.Top));
        var attacker = CreateUnit(Substitute.For<IPilot>(), new HexPosition(1, 1, HexDirection.Top), singlePath);
        attacker.Height.Returns(2);
        var target = CreateUnit(Substitute.For<IPilot>(), new HexPosition(1, 2, HexDirection.Top), singlePath);
        target.Height.Returns(3);

        // Act
        var scenario = PhysicalAttackScenario.FromUnits(attacker, target, PhysicalAttackType.Punch);

        // Assert
        scenario.AttackerHeight.ShouldBe(2);
        scenario.TargetHeight.ShouldBe(3);
    }

    [Fact]
    public void FromUnits_CarriesTargetUnit()
    {
        // Arrange
        var singlePath = MovementPath.CreateSingleSegmentPath(new HexPosition(1, 1, HexDirection.Top));
        var attacker = CreateUnit(Substitute.For<IPilot>(), new HexPosition(1, 1, HexDirection.Top), singlePath);
        var target = CreateUnit(Substitute.For<IPilot>(), new HexPosition(1, 2, HexDirection.Top), singlePath);

        // Act
        var scenario = PhysicalAttackScenario.FromUnits(attacker, target, PhysicalAttackType.Punch);

        // Assert
        scenario.TargetUnit.ShouldBe(target);
    }

    [Fact]
    public void FromUnits_CarriesAttackerUnit()
    {
        // Arrange
        var singlePath = MovementPath.CreateSingleSegmentPath(new HexPosition(1, 1, HexDirection.Top));
        var attacker = CreateUnit(Substitute.For<IPilot>(), new HexPosition(1, 1, HexDirection.Top), singlePath);
        var target = CreateUnit(Substitute.For<IPilot>(), new HexPosition(1, 2, HexDirection.Top), singlePath);

        // Act
        var scenario = PhysicalAttackScenario.FromUnits(attacker, target, PhysicalAttackType.Punch);

        // Assert
        scenario.AttackerUnit.ShouldBe(attacker);
    }

    [Fact]
    public void FromUnits_WithStationaryUnit_ProducesStandingStillAndZeroHexes()
    {
        // Arrange
        var stationaryPath = MovementPath.CreateSingleSegmentPath(new HexPosition(1, 1, HexDirection.Top));
        var attacker = CreateUnit(Substitute.For<IPilot>(), new HexPosition(1, 1, HexDirection.Top), stationaryPath);
        var target = CreateUnit(Substitute.For<IPilot>(), new HexPosition(1, 2, HexDirection.Top), stationaryPath);

        // Act
        var scenario = PhysicalAttackScenario.FromUnits(attacker, target, PhysicalAttackType.Punch);

        // Assert
        scenario.AttackerMovementType.ShouldBe(MovementType.StandingStill);
        scenario.TargetHexesMoved.ShouldBe(0);
    }

    [Fact]
    public void FromUnits_WithNullPilot_ThrowsException()
    {
        // Arrange
        var attacker = CreateUnit(null, new HexPosition(1, 1, HexDirection.Top));
        var target = CreateUnit(Substitute.For<IPilot>(), new HexPosition(1, 2, HexDirection.Top));

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => PhysicalAttackScenario.FromUnits(attacker, target, PhysicalAttackType.Punch))
            .Message.ShouldBe("Attacker pilot is not assigned");
    }

    [Fact]
    public void FromUnits_WithNullAttackerPosition_ThrowsException()
    {
        // Arrange
        var attacker = CreateUnit(Substitute.For<IPilot>());
        var target = CreateUnit(Substitute.For<IPilot>(), new HexPosition(1, 2, HexDirection.Top));

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => PhysicalAttackScenario.FromUnits(attacker, target, PhysicalAttackType.Punch))
            .Message.ShouldBe("Attacker position is not set");
    }

    [Fact]
    public void FromUnits_WithNullTargetPosition_ThrowsException()
    {
        // Arrange
        var attacker = CreateUnit(Substitute.For<IPilot>(), new HexPosition(1, 1, HexDirection.Top));
        var target = CreateUnit(Substitute.For<IPilot>());

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => PhysicalAttackScenario.FromUnits(attacker, target, PhysicalAttackType.Punch))
            .Message.ShouldBe("Target position is not set");
    }

    [Fact]
    public void FromUnits_WithNullAttackerMovementPath_ThrowsException()
    {
        // Arrange
        var attacker = CreateUnit(Substitute.For<IPilot>(), new HexPosition(1, 1, HexDirection.Top));
        var target = CreateUnit(Substitute.For<IPilot>(), new HexPosition(1, 2, HexDirection.Top), WalkingPath);

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => PhysicalAttackScenario.FromUnits(attacker, target, PhysicalAttackType.Punch))
            .Message.ShouldBe("Attacker's Movement Type is undefined");
    }

    [Fact]
    public void FromUnits_WithNullTargetMovementPath_ThrowsException()
    {
        // Arrange
        var attacker = CreateUnit(Substitute.For<IPilot>(), new HexPosition(1, 1, HexDirection.Top), WalkingPath);
        var target = CreateUnit(Substitute.For<IPilot>(), new HexPosition(1, 2, HexDirection.Top));

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => PhysicalAttackScenario.FromUnits(attacker, target, PhysicalAttackType.Punch))
            .Message.ShouldBe("Target's Movement Type is undefined");
    }
}
