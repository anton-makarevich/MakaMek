using NSubstitute;
using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Attack;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Pilots;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Data.Game.Mechanics;

public class AttackScenarioTests
{
    [Fact]
    public void FromUnits_WithValidUnits_CreatesScenarioWithCorrectValues()
    {
        // Arrange
        var attacker = Substitute.For<IUnit>();
        var target = Substitute.For<IUnit>();
        var pilot = Substitute.For<IPilot>();
        pilot.Gunnery.Returns(4);
        
        var movementPath = new MovementPath([
                new PathSegment(
                    new HexPosition(1, 1, HexDirection.Top),
                    new HexPosition(1, 2, HexDirection.Top),
                    1),
                new PathSegment(
                    new HexPosition(1, 2, HexDirection.Top),
                    new HexPosition(1, 3, HexDirection.Top),
                    1),
                new PathSegment(
                    new HexPosition(1, 3, HexDirection.Top),
                    new HexPosition(1, 4, HexDirection.Top),
                    1)
            ], MovementType.Walk);
        
        var attackerPosition = new HexPosition(new HexCoordinates(1, 1), HexDirection.Top);
        var targetPosition = new HexPosition(new HexCoordinates(5, 5), HexDirection.Bottom);
        
        attacker.Pilot.Returns(pilot);
        attacker.Position.Returns(attackerPosition);
        attacker.Facing.Returns(attackerPosition.Facing);
        attacker.MovementTaken.Returns(movementPath);
        attacker.GetAttackModifiers(PartLocation.RightArm).Returns(new List<RollModifier>
        {
            new HeatRollModifier { Value = 2, HeatLevel = 15 }
        });
        
        target.Position.Returns(targetPosition);
        target.MovementTaken.Returns(movementPath);
        
        // Act
        var scenario = AttackScenario.FromUnits(attacker, target, PartLocation.RightArm, isPrimaryTarget: true, aimedShotTarget: PartLocation.Head);
        
        // Assert
        scenario.AttackerGunnery.ShouldBe(4);
        scenario.AttackerPosition.ShouldBe(attackerPosition);
        scenario.TargetPosition.ShouldBe(targetPosition);
        scenario.AttackerMovementType.ShouldBe(MovementType.Run);
        scenario.TargetHexesMoved.ShouldBe(3);
        scenario.AttackerModifiers.Count.ShouldBe(1);
        scenario.AttackerModifiers[0].ShouldBeOfType<HeatRollModifier>();
        scenario.AttackerFacing.ShouldBe(HexDirection.Top);
        scenario.IsPrimaryTarget.ShouldBeTrue();
        scenario.AimedShotTarget.ShouldBe(PartLocation.Head);
    }
    
    [Fact]
    public void FromUnits_WithNullPilot_ThrowsException()
    {
        // Arrange
        var attacker = Substitute.For<IUnit>();
        var target = Substitute.For<IUnit>();
        attacker.Pilot.Returns((IPilot?)null);
        
        // Act & Assert
        Should.Throw<InvalidOperationException>(() => AttackScenario.FromUnits(attacker, target, PartLocation.CenterTorso))
            .Message.ShouldBe("Attacker pilot is not assigned");
    }
    
    [Fact]
    public void FromUnits_WithNullMovementType_ThrowsException()
    {
        // Arrange
        var attacker = Substitute.For<IUnit>();
        var target = Substitute.For<IUnit>();
        var pilot = Substitute.For<IPilot>();
        
        attacker.Pilot.Returns(pilot);
        attacker.Position.Returns(new HexPosition(new HexCoordinates(1, 1), HexDirection.Top));
        attacker.MovementTaken.Returns((MovementPath?)null);
        target.Position.Returns(new HexPosition(new HexCoordinates(5, 5), HexDirection.Bottom));
        
        // Act & Assert
        Should.Throw<InvalidOperationException>(() => AttackScenario.FromUnits(attacker, target, PartLocation.CenterTorso))
            .Message.ShouldBe("Attacker's Movement Type is undefined");
    }
    
    [Fact]
    public void FromHypothetical_CreatesScenarioWithProvidedValues()
    {
        // Arrange
        var attackerPosition = new HexPosition(1, 1, HexDirection.Top);
        var targetPosition = new HexPosition(new HexCoordinates(5, 5), HexDirection.Bottom);
        List<RollModifier> modifiers =
        [
            new HeatRollModifier { Value = 2, HeatLevel = 15 },
            new ProneAttackerModifier { Value = 2 }
        ];
        
        // Act
        var scenario = AttackScenario.FromHypothetical(
            attackerGunnery: 5,
            attackerPosition: attackerPosition,
            attackerMovementType: MovementType.Jump,
            targetPosition: targetPosition,
            targetHexesMoved: 4,
            attackerModifiers: modifiers,
            attackerFacing: HexDirection.TopLeft,
            isPrimaryTarget: false,
            aimedShotTarget: PartLocation.LeftLeg);
        
        // Assert
        scenario.AttackerGunnery.ShouldBe(5);
        scenario.AttackerPosition.ShouldBe(attackerPosition);
        scenario.TargetPosition.ShouldBe(targetPosition);
        scenario.AttackerMovementType.ShouldBe(MovementType.Jump);
        scenario.TargetHexesMoved.ShouldBe(4);
        scenario.AttackerModifiers.Count.ShouldBe(2);
        scenario.AttackerFacing.ShouldBe(HexDirection.TopLeft);
        scenario.IsPrimaryTarget.ShouldBeFalse();
        scenario.AimedShotTarget.ShouldBe(PartLocation.LeftLeg);
    }
    
    [Fact]
    public void FromUnits_WithNullAttackerPosition_ThrowsException()
    {
        // Arrange
        var attacker = Substitute.For<IUnit>();
        var target = Substitute.For<IUnit>();
        var pilot = Substitute.For<IPilot>();
    
        attacker.Pilot.Returns(pilot);
        attacker.Position.Returns((HexPosition?)null);
        target.Position.Returns(new HexPosition(new HexCoordinates(5, 5), HexDirection.Bottom));
    
        // Act & Assert
        Should.Throw<InvalidOperationException>(() => AttackScenario.FromUnits(attacker, target, PartLocation.CenterTorso))
            .Message.ShouldBe("Attacker position is not set");
    }

    [Fact]
    public void FromUnits_WithNullTargetPosition_ThrowsException()
    {
        // Arrange
        var attacker = Substitute.For<IUnit>();
        var target = Substitute.For<IUnit>();
        var pilot = Substitute.For<IPilot>();
    
        attacker.Pilot.Returns(pilot);
        attacker.Position.Returns(new HexPosition(new HexCoordinates(1, 1), HexDirection.Top));
        target.Position.Returns((HexPosition?)null);
    
        // Act & Assert
        Should.Throw<InvalidOperationException>(() => AttackScenario.FromUnits(attacker, target, PartLocation.CenterTorso))
            .Message.ShouldBe("Target position is not set");
    }
}

