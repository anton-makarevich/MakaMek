using NSubstitute;
using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Data.Units.Components;
using Sanet.MakaMek.Core.Events;
using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Models.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Attack;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Penalties.MovementPenalties;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Shouldly;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components;
using Sanet.MakaMek.Core.Models.Units.Components.Engines;
using Sanet.MakaMek.Core.Models.Units.Components.Internal;
using Sanet.MakaMek.Core.Models.Units.Components.Internal.Actuators;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons.Energy;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Core.Models.Units.Pilots;
using Shouldly.ShouldlyExtensionMethods;

namespace Sanet.MakaMek.Core.Tests.Models.Units.Mechs;

public class MechTests
{
    private readonly IDiceRoller _diceRoller = Substitute.For<IDiceRoller>();
    private readonly IDamageTransferCalculator _damageTransferCalculator = Substitute.For<IDamageTransferCalculator>();

    public MechTests()
    {
        _damageTransferCalculator
            .CalculateExplosionDamage(Arg.Any<Unit>(), Arg.Any<PartLocation>(), Arg.Any<int>())
            .Returns([]);
    }

    private static List<UnitPart> CreateBasicPartsData()
    {
        var engineData = new ComponentData
        {
            Type = MakaMekComponent.Engine,
            Assignments =
            [
                new LocationSlotAssignment(PartLocation.CenterTorso, 0, 3),
                new LocationSlotAssignment(PartLocation.CenterTorso, 7, 3)
            ],
            SpecificData = new EngineStateData(EngineType.Fusion, 100)
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
    
    private static LocationHitData CreateHitDataForLocation(PartLocation partLocation,
        int damage,
        int[]? aimedShotRoll = null,
        int[]? locationRoll = null)
    {
        return new LocationHitData(
        [
            new LocationDamageData(partLocation,
                damage-1,
                1,
                false)
        ], aimedShotRoll??[], locationRoll??[], partLocation);
    }

    [Fact]
    public void Mech_CanWalkBackwards_BitCannotRun()
    {
        // Arrange & Act
        var mech = new Mech("Test", "TST-1A", 50, 4, CreateBasicPartsData());

        // Assert
        mech.CanMoveBackward(MovementType.Walk).ShouldBeTrue();
        mech.CanMoveBackward(MovementType.Run).ShouldBeFalse();
    }

    [Fact]
    public void Constructor_InitializesAllParts()
    {
        // Arrange & Act
        var mech = new Mech("Test", "TST-1A", 50, 4, CreateBasicPartsData());

        // Assert
        mech.Parts.Count.ShouldBe(8, "all mech locations should be initialized");
        mech.Parts.ShouldContainKey(PartLocation.Head);
        mech.Parts.ShouldContainKey(PartLocation.CenterTorso);
        mech.Parts.ShouldContainKey(PartLocation.LeftTorso);
        mech.Parts.ShouldContainKey(PartLocation.RightTorso);
        mech.Parts.ShouldContainKey(PartLocation.LeftArm);
        mech.Parts.ShouldContainKey(PartLocation.RightArm);
        mech.Parts.ShouldContainKey(PartLocation.LeftLeg);
        mech.Parts.ShouldContainKey(PartLocation.RightLeg);
        
        mech.Class.ShouldBe(WeightClass.Medium);
    }

    [Theory]
    [InlineData(PartLocation.LeftArm, PartLocation.LeftTorso)]
    [InlineData(PartLocation.RightArm, PartLocation.RightTorso)]
    [InlineData(PartLocation.LeftLeg, PartLocation.LeftTorso)]
    [InlineData(PartLocation.RightLeg, PartLocation.RightTorso)]
    [InlineData(PartLocation.LeftTorso, PartLocation.CenterTorso)]
    [InlineData(PartLocation.RightTorso, PartLocation.CenterTorso)]
    public void GetTransferLocation_ReturnsCorrectLocation(PartLocation from, PartLocation expected)
    {
        // Arrange
        var mech = new Mech("Test", "TST-1A", 50, 4, CreateBasicPartsData());

        // Act
        var transferLocation = mech.GetTransferLocation(from);

        // Assert
        transferLocation.ShouldBe(expected);
    }

    [Fact]
    public void Move_ShouldUpdatePosition()
    {
        // Arrange
        var mech = new Mech("Test", "TST-1A", 50, 4, CreateBasicPartsData());
        var deployPosition = new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom);
        var newCoordinates = new HexPosition(new HexCoordinates(1, 2), HexDirection.BottomLeft);
        mech.Deploy(deployPosition);

        // Act
        mech.Move(MovementType.Walk, [new PathSegment(deployPosition, newCoordinates, 0).ToData()]);

        // Assert
        mech.Position.ShouldBe(newCoordinates);
        mech.HasMoved.ShouldBeTrue();
        mech.MovementTypeUsed.ShouldBe(MovementType.Walk);
        mech.DistanceCovered.ShouldBe(1);
        mech.MovementPointsSpent.ShouldBe(0);
    }
    
    [Fact]
    public void Move_ShouldNotUpdateDistanceCovered_WhenMovingToSamePosition()
    {
        // Arrange
        var mech = new Mech("Test", "TST-1A", 50, 4, CreateBasicPartsData());
        var position = new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom);
        mech.Deploy(position);

        // Act
        mech.Move(MovementType.Walk, [new PathSegment(position, position, 0).ToData()]);

        // Assert
        mech.Position.ShouldBe(position);
        mech.HasMoved.ShouldBeTrue();
        mech.MovementTypeUsed.ShouldBe(MovementType.Walk);
        mech.DistanceCovered.ShouldBe(0);
        mech.MovementPointsSpent.ShouldBe(0);
    }
    
    [Fact]
    public void Move_ShouldKeepPosition_WhenNoPathSegments()
    {
        // Arrange
        var mech = new Mech("Test", "TST-1A", 50, 4, CreateBasicPartsData());
        var deployPosition = new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom);
        mech.Deploy(deployPosition);

        // Act
        mech.Move(MovementType.Walk, []);

        // Assert
        mech.Position.ShouldBe(deployPosition);
        mech.HasMoved.ShouldBeTrue();
        mech.MovementTypeUsed.ShouldBe(MovementType.Walk);
        mech.DistanceCovered.ShouldBe(0);
        mech.MovementPointsSpent.ShouldBe(0);
    }

    [Fact]
    public void Move_ShouldThrowException_WhenNotDeployed()
    {
        // Arrange
        var mech = new Mech("Test", "TST-1A", 50, 4, CreateBasicPartsData());
        var newCoordinates = new HexPosition(new HexCoordinates(1, 2), HexDirection.BottomLeft);
    
        // Act
        var act = () => mech.Move(MovementType.Walk,
            [new PathSegment(new HexPosition(1, 1, HexDirection.Bottom), newCoordinates, 1).ToData()]);
    
        // Assert
        var ex = Should.Throw<InvalidOperationException>(act);
        ex.Message.ShouldBe("Unit is not deployed.");
    }

    [Fact]
    public void ResetTurnState_ShouldResetMovementTracking()
    {
        // Arrange
        var mech = new Mech("Test", "TST-1A", 50, 4, CreateBasicPartsData());
        var deployPosition = new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom);
        var newCoordinates = new HexPosition(new HexCoordinates(1, 2), HexDirection.BottomLeft);
        mech.Deploy(deployPosition);
        mech.Move(MovementType.Walk, [new PathSegment(deployPosition, newCoordinates, 1).ToData()]);

        // Act
        mech.ResetTurnState();

        // Assert
        mech.Position.ShouldBe(newCoordinates);
        mech.HasMoved.ShouldBeFalse();
        mech.MovementTypeUsed.ShouldBeNull();
        mech.DistanceCovered.ShouldBe(0);
        mech.MovementPointsSpent.ShouldBe(0);
    }

    [Fact]
    public void HeatDissipation_CalculatedBasedOnHeatSinks()
    {
        // Arrange
        var parts = CreateBasicPartsData();
        var centerTorso = parts.Single(p => p.Location == PartLocation.CenterTorso);
        centerTorso.TryAddComponent(new HeatSink());
        centerTorso.TryAddComponent(new HeatSink());
        var mech = new Mech("Test", "TST-1A", 50, 4, parts);

        // Act
        var dissipation = mech.HeatDissipation;

        // Assert
        dissipation.ShouldBe(6, "2 heat sinks + 4 engine HS");
    }

    [Fact]
    public void CalculateBattleValue_IncludesWeapons()
    {
        // Arrange
        var parts = CreateBasicPartsData();
        var centerTorso = parts.Single(p => p.Location == PartLocation.CenterTorso);
        centerTorso.TryAddComponent(new MediumLaser());
        var mech = new Mech("Test", "TST-1A", 50, 4, parts);

        // Act
        var bv = mech.CalculateBattleValue();

        // Assert
        bv.ShouldBe(5046, "5000 (base BV for 50 tons) + 46 (medium laser)");
    }

    [Fact]
    public void Status_StartsActive()
    {
        // Arrange & Act
        var sut = new Mech("Test", "TST-1A", 50, 4, CreateBasicPartsData());
        var pilot = Substitute.For<IPilot>();
        pilot.IsConscious.Returns(true);
        sut.AssignPilot(pilot);

        // Assert
        sut.Status.ShouldBe(UnitStatus.Active);
    }

    [Fact]
    public void Shutdown_ChangesStatusToShutdown()
    {
        // Arrange
        var mech = new Mech("Test", "TST-1A", 50, 4, CreateBasicPartsData());
        var shutdownData = new ShutdownData { Reason = ShutdownReason.Voluntary, Turn = 1 };

        // Act
        mech.Shutdown(shutdownData);

        // Assert
        mech.Status.ShouldHaveFlag(UnitStatus.Shutdown);
    }

    [Fact]
    public void Startup_ChangesStatusToActive()
    {
        // Arrange
        var sut = new Mech("Test", "TST-1A", 50, 4, CreateBasicPartsData());
        var pilot = Substitute.For<IPilot>();
        pilot.IsConscious.Returns(true);
        sut.AssignPilot(pilot);
        var shutdownData = new ShutdownData { Reason = ShutdownReason.Voluntary, Turn = 1 };
        sut.Shutdown(shutdownData);

        // Act
        sut.Startup();

        // Assert
        sut.Status.ShouldBe(UnitStatus.Active);
    }

    [Fact]
    public void SetProne_AddsProneStatus()
    {
        // Arrange
        var mech = new Mech("Test", "TST-1A", 50, 4, CreateBasicPartsData());

        // Act
        mech.SetProne();

        // Assert
        (mech.Status & UnitStatus.Prone).ShouldBe(UnitStatus.Prone);
        mech.IsProne.ShouldBeTrue();
    }

    [Fact]
    public void StandUp_RemovesProneStatus()
    {
        // Arrange
        var mech = new Mech("Test", "TST-1A", 50, 4, CreateBasicPartsData());
        mech.Deploy(new HexPosition(new HexCoordinates(0, 0), HexDirection.Top));
        mech.SetProne();

        // Act
        mech.StandUp(HexDirection.Bottom);

        // Assert
        (mech.Status & UnitStatus.Prone).ShouldNotBe(UnitStatus.Prone);
        mech.IsProne.ShouldBeFalse();
        mech.Position!.Facing.ShouldBe(HexDirection.Bottom);
    }

    [Theory]
    [InlineData(0, HexDirection.Top, HexDirection.TopRight, false)] // No rotation allowed
    [InlineData(1, HexDirection.Top, HexDirection.TopRight, true)] // 60 degrees allowed, within limit
    [InlineData(1, HexDirection.Top, HexDirection.Bottom, false)] // 60 degrees allowed, beyond the limit
    [InlineData(2, HexDirection.Top, HexDirection.BottomRight, true)] // 120 degrees allowed, within limit
    [InlineData(3, HexDirection.Top, HexDirection.Bottom, true)] // 180 degrees allowed, within limit
    public void RotateTorso_ShouldRespectPossibleTorsoRotation(
        int possibleRotation,
        HexDirection unitFacing,
        HexDirection targetFacing,
        bool shouldRotate)
    {
        // Arrange
        var parts = CreateBasicPartsData();
        var torsos = parts.OfType<Torso>().ToList();
        var mech = new Mech("Test", "TST-1A", 50, 4, parts, possibleRotation);
        mech.Deploy(new HexPosition(new HexCoordinates(0, 0), unitFacing));

        // Act
        mech.RotateTorso(targetFacing);

        // Assert
        foreach (var torso in torsos)
        {
            torso.Facing.ShouldBe(shouldRotate ? targetFacing : unitFacing);
        }
    }

    [Fact]
    public void HasUsedTorsoTwist_WhenTorsosAlignedWithUnit_ShouldBeFalse()
    {
        // Arrange
        var parts = CreateBasicPartsData();
        var mech = new Mech("Test", "TST-1A", 50, 4, parts);
        mech.Deploy(new HexPosition(new HexCoordinates(0, 0), HexDirection.Top));

        // Assert
        mech.HasUsedTorsoTwist.ShouldBeFalse();
    }

    [Fact]
    public void HasUsedTorsoTwist_WhenTorsosRotated_ShouldBeTrue()
    {
        // Arrange
        var parts = CreateBasicPartsData();
        var mech = new Mech("Test", "TST-1A", 50, 4, parts);
        mech.Deploy(new HexPosition(new HexCoordinates(0, 0), HexDirection.Top));

        // Act
        mech.RotateTorso(HexDirection.TopRight);

        // Assert
        mech.HasUsedTorsoTwist.ShouldBeTrue();
    }

    [Theory]
    [InlineData(0, false)] // No rotation possible
    [InlineData(1, true)] // Normal rotation
    [InlineData(2, true)] // Extended rotation
    public void CanRotateTorso_ShouldRespectPossibleTorsoRotation(int possibleRotation, bool expected)
    {
        // Arrange
        var parts = CreateBasicPartsData();
        var mech = new Mech("Test", "TST-1A", 50, 4, parts, possibleRotation);
        mech.Deploy(new HexPosition(new HexCoordinates(0, 0), HexDirection.Top));

        // Act & Assert
        mech.CanRotateTorso.ShouldBe(expected);
    }

    [Fact]
    public void CanRotateTorso_WhenTorsoAlreadyRotated_ShouldBeFalse()
    {
        // Arrange
        var parts = CreateBasicPartsData();
        var mech = new Mech("Test", "TST-1A", 50, 4, parts);
        mech.Deploy(new HexPosition(new HexCoordinates(0, 0), HexDirection.Top));

        // Act
        mech.RotateTorso(HexDirection.TopRight);

        // Assert
        mech.CanRotateTorso.ShouldBeFalse();
    }

    [Fact]
    public void Constructor_ShouldSetDefaultPossibleTorsoRotation()
    {
        // Arrange & Act
        var mech = new Mech("Test", "TST-1A", 50, 4, CreateBasicPartsData());

        // Assert
        mech.PossibleTorsoRotation.ShouldBe(1);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Constructor_ShouldSetSpecifiedPossibleTorsoRotation(int rotation)
    {
        // Arrange & Act
        var mech = new Mech("Test", "TST-1A", 50, 4, CreateBasicPartsData(), rotation);

        // Assert
        mech.PossibleTorsoRotation.ShouldBe(rotation);
    }

    [Fact]
    public void Constructor_DoesNotAssignPilot()
    {
        // Arrange & Act
        var mech = new Mech("Test", "TST-1A", 50, 4, CreateBasicPartsData());

        // Assert
        mech.Pilot.ShouldBeNull();
    }

    [Fact]
    public void AssignPilot_WithValidPilot_AssignsPilotSuccessfully()
    {
        // Arrange
        var mech = new Mech("Test", "TST-1A", 50, 4, CreateBasicPartsData());
        var pilot = new MechWarrior("John", "Doe");

        // Act
        mech.AssignPilot(pilot);

        // Assert
        mech.Pilot.ShouldBe(pilot);
    }

    [Fact]
    public void AssignPilot_WithValidPilot_SetsBidirectionalRelationship()
    {
        // Arrange
        var mech = new Mech("Test", "TST-1A", 50, 4, CreateBasicPartsData());
        var pilot = new MechWarrior("John", "Doe");

        // Act
        mech.AssignPilot(pilot);

        // Assert
        mech.Pilot.ShouldBe(pilot);
        pilot.AssignedTo.ShouldBe(mech);
    }

    [Fact]
    public void AssignPilot_WhenPilotAlreadyAssignedToAnotherUnit_ReassignsPilot()
    {
        // Arrange
        var mech1 = new Mech("Test1", "TST-1A", 50, 4, CreateBasicPartsData());
        var mech2 = new Mech("Test2", "TST-2A", 60, 4, CreateBasicPartsData());
        var pilot = new MechWarrior("John", "Doe");

        // Initially assign pilot to mech1
        mech1.AssignPilot(pilot);

        // Act - reassign pilot to mech2
        mech2.AssignPilot(pilot);

        // Assert
        mech1.Pilot.ShouldBeNull();
        mech2.Pilot.ShouldBe(pilot);
        pilot.AssignedTo.ShouldBe(mech2);
    }

    [Fact]
    public void AssignPilot_WhenUnitAlreadyHasPilot_UnassignsPreviousPilot()
    {
        // Arrange
        var mech = new Mech("Test", "TST-1A", 50, 4, CreateBasicPartsData());
        var pilot1 = new MechWarrior("John", "Doe");
        var pilot2 = new MechWarrior("Jane", "Smith");

        // Initially assign pilot1
        mech.AssignPilot(pilot1);

        // Act - assign pilot2
        mech.AssignPilot(pilot2);

        // Assert
        mech.Pilot.ShouldBe(pilot2);
        pilot1.AssignedTo.ShouldBeNull();
        pilot2.AssignedTo.ShouldBe(mech);
    }

    [Fact]
    public void UnassignPilot_WithAssignedPilot_RemovesBidirectionalRelationship()
    {
        // Arrange
        var mech = new Mech("Test", "TST-1A", 50, 4, CreateBasicPartsData());
        var pilot = new MechWarrior("John", "Doe");
        mech.AssignPilot(pilot);

        // Act
        mech.UnassignPilot();

        // Assert
        mech.Pilot.ShouldBeNull();
        pilot.AssignedTo.ShouldBeNull();
    }

    [Fact]
    public void UnassignPilot_WithNoPilot_DoesNotThrow()
    {
        // Arrange
        var mech = new Mech("Test", "TST-1A", 50, 4, CreateBasicPartsData());

        // Act & Assert - should not throw
        mech.UnassignPilot();
        mech.Pilot.ShouldBeNull();
    }

    [Fact]
    public void ResetTurnState_ShouldResetTorsoRotation()
    {
        // Arrange
        var parts = CreateBasicPartsData();
        var torsos = parts.OfType<Torso>().ToList();
        var mech = new Mech("Test", "TST-1A", 50, 4, parts);
        mech.Deploy(new HexPosition(new HexCoordinates(1, 1), HexDirection.BottomRight));

        // Rotate torsos in a different direction
        mech.RotateTorso(HexDirection.Bottom);

        // Verify torsos are rotated
        foreach (var torso in torsos)
        {
            torso.Facing.ShouldBe(HexDirection.Bottom, "Torso should be rotated before reset");
        }

        // Act
        mech.ResetTurnState();

        // Assert
        foreach (var torso in torsos)
        {
            torso.Facing.ShouldBe(HexDirection.BottomRight, "Torso should be reset to match unit facing");
        }
    }

    [Fact]
    public void ResetTurnState_ShouldResetWeaponTargets()
    {
        // Arrange
        var sut = new Mech("Test", "TST-1A", 50, 4, CreateBasicPartsData());
        var weapon = new MediumLaser();
        // Attach a weapon to a part (e.g., right arm)
        var rightArm = sut.Parts[PartLocation.RightArm];
        rightArm.TryAddComponent(weapon);
        sut.Deploy(new HexPosition(new HexCoordinates(1, 1), HexDirection.BottomRight));
        // Set a dummy target
        var dummyTarget = new Mech("Dummy", "DMY-1A", 50, 4, CreateBasicPartsData());
        sut.DeclareWeaponAttack([
            new WeaponTargetData
            {
                Weapon = weapon.ToData(),
                TargetId = dummyTarget.Id,
                IsPrimaryTarget = true
            }
        ]);
        sut.HasDeclaredWeaponAttack.ShouldBeTrue();
        sut.GetAllWeaponTargetsData().ShouldNotBeEmpty();

        // Act
        sut.ResetTurnState();

        // Assert
        sut.HasDeclaredWeaponAttack.ShouldBeFalse();
        sut.GetAllWeaponTargetsData().ShouldBeEmpty();
    }


    [Theory]
    [InlineData(5, 8, 2)]
    [InlineData(4, 6, 0)]
    [InlineData(3, 5, 2)]
    public void GetMovement_ReturnsCorrectMPs(int walkMp, int runMp, int jumpMp)
    {
        // Arrange
        var parts = CreateBasicPartsData();
        if (jumpMp > 0)
        {
            var centerTorso = parts.Single(p => p.Location == PartLocation.CenterTorso);
            centerTorso.TryAddComponent(new JumpJets());
            centerTorso.TryAddComponent(new JumpJets());
        }

        var mech = new Mech("Test", "TST-1A", 50, walkMp, parts);

        // Act
        var walkingMp = mech.GetMovementPoints(MovementType.Walk);
        var runningMp = mech.GetMovementPoints(MovementType.Run);
        var jumpingMp = mech.GetMovementPoints(MovementType.Jump);

        // Assert
        walkingMp.ShouldBe(walkMp, "walking MP should match the base movement");
        runningMp.ShouldBe(runMp, "running MP should be 1.5x walking");
        jumpingMp.ShouldBe(jumpMp, "jumping MP should match the number of jump jets");
    }

    [Theory]
    // 2–7: 0, 8–9: 1, 10–11: 2, 12: 3 (torso), 1 (head/limb blown off)
    [InlineData(2, 0)]
    [InlineData(7, 0)]
    [InlineData(8, 1)]
    [InlineData(9, 1)]
    [InlineData(10, 2)]
    [InlineData(11, 2)]
    [InlineData(12, 3)]
    [InlineData(13, 0)]
    public void GetNumCriticalHits_ReturnsExpected(int roll, int expected)
    {
        var sut = new Mech("Test", "TST-1A", 50, 4, CreateBasicPartsData());
        sut.GetNumCriticalHits(roll).ShouldBe(expected);
    }

    [Fact]
    public void CalculateCriticalHitsData_ShouldSetCriticalHits_WhenDamageExceedsArmorAndCritsRolled()
    {
        // Arrange
        var part = new Arm("TestArm", PartLocation.RightArm, 0, 10);
        var mech = new Mech("TestChassis", "TestModel", 50, 5, [part]);
        for (var i = 1; i < part.TotalSlots; i++)
            part.TryAddComponent(new TestComponent());
        
        _diceRoller.Roll2D6().Returns(
            [new DiceResult(5), new DiceResult(5)] // 10 total for crit roll
        );
        _diceRoller.RollD6().Returns(new DiceResult(2),
            new DiceResult(3),
            new DiceResult(4),
            new DiceResult(5));

        // Act
        var critsData = mech.CalculateCriticalHitsData(PartLocation.RightArm, _diceRoller, _damageTransferCalculator);

        // Assert
        critsData.ShouldNotBeNull();
        critsData.HitComponents.ShouldNotBeNull();
        critsData.HitComponents.Length.ShouldBe(2);
        critsData.HitComponents.FirstOrDefault(c => c.Slot == 2).ShouldNotBeNull();
        critsData.Roll.ShouldBe([5, 5]);
        critsData.NumCriticalHits.ShouldBe(2);
    }

    private class TestComponent() : Component(new EquipmentDefinition(
        "Test Component",
        MakaMekComponent.Masc));

    [Fact]
    public void CalculateCriticalHitsData_ShouldReturnOnlyAvailableInSecondGroup()
    {
        // Arrange
        var part = new Arm("TestArm", PartLocation.RightArm, 0, 10);
        var mech = new Mech("TestChassis", "TestModel", 50, 5, [part]);
        for (var i = 1; i < 7; i++)
            part.TryAddComponent(new TestComponent());

        _diceRoller.Roll2D6().Returns(
            [new DiceResult(5), new DiceResult(5)] // 10 total for crit roll
        );
        _diceRoller.RollD6().Returns(
            new DiceResult(4),
            new DiceResult(3),
            new DiceResult(4),
            new DiceResult(5));

        // Act
        var critsData = mech.CalculateCriticalHitsData(PartLocation.RightArm, _diceRoller, _damageTransferCalculator);

        // Assert
        critsData.ShouldNotBeNull();
        critsData.HitComponents.ShouldNotBeNull();
        critsData.HitComponents.Length.ShouldBe(2);
        critsData.HitComponents.FirstOrDefault(c => c.Slot == 6).ShouldNotBeNull();
    }

    [Fact]
    public void CalculateCriticalHitsData_ShouldSetCriticalHits_InSmallPart_WhenDamageExceedsArmorAndCritsRolled()
    {
        // Arrange
        var part = new Leg("TestLeg", PartLocation.RightLeg, 0, 10);
        var mech = new Mech("TestChassis", "TestModel", 50, 5, [part]);

        _diceRoller.Roll2D6().Returns(
            [new DiceResult(5), new DiceResult(5)] // 10 total for crit roll
        );
        _diceRoller.RollD6().Returns(new DiceResult(2), new DiceResult(3), new DiceResult(4), new DiceResult(5));

        // Act
        var critsData = mech.CalculateCriticalHitsData(PartLocation.RightLeg, _diceRoller, _damageTransferCalculator);

        // Assert
        critsData.ShouldNotBeNull();
        critsData.HitComponents.ShouldNotBeNull();
        critsData.HitComponents.Length.ShouldBe(2);
        critsData.HitComponents.FirstOrDefault(c => c.Slot == 1).ShouldNotBeNull();
        critsData.HitComponents.FirstOrDefault(c => c.Slot == 2).ShouldNotBeNull();
    }

    [Fact]
    public void CalculateCriticalHitsData_ShouldNotSetCriticalHits_WhenNoCritsRolled()
    {
        // Arrange
        var part = new Arm("TestArm", PartLocation.RightArm, 0, 10);
        var mech = new Mech("TestChassis", "TestModel", 50, 5, [part]);
        for (var i = 1; i < part.TotalSlots; i++)
            part.TryAddComponent(new TestComponent());

        _diceRoller.Roll2D6().Returns(
            [new(3), new(3)] // 6 total for crit roll (no crits)
        );

        // Act
        var critsData = mech.CalculateCriticalHitsData(PartLocation.RightArm, _diceRoller, _damageTransferCalculator);

        // Assert
        critsData.ShouldNotBeNull();
        critsData.Roll.ShouldBe([3, 3]);
        critsData.NumCriticalHits.ShouldBe(0);
        critsData.HitComponents.ShouldBeNull();
    }

    [Theory]
    [InlineData(PartLocation.LeftArm)]
    [InlineData(PartLocation.RightLeg)]
    public void CalculateCriticalHitsData_ShouldAutoPickOnlyAvailableSlot(PartLocation location)
    {
        // Arrange
        UnitPart part = (location == PartLocation.LeftArm)
            ? new Arm("TestArm", location, 0, 10)
            : new Leg("TestLeg", location, 0, 10);

        foreach (var component in part.Components)
        {
            if (component.MountedAtFirstLocationSlots[0] != 1)
                component.Hit(); // destroy all components but first
        }

        var mech = new Mech("TestChassis", "TestModel", 50, 5, [part]);

        _diceRoller.Roll2D6().Returns(
            [new(4), new(5)] // 9 total for crit roll
        );
        _diceRoller.RollD6().Returns(new DiceResult(1));

        // Act
        var critsData = mech.CalculateCriticalHitsData(location, _diceRoller, _damageTransferCalculator);

        // Assert
        critsData.ShouldNotBeNull();
        critsData.HitComponents.ShouldNotBeNull();
        critsData.HitComponents.Length.ShouldBe(1);
        critsData.HitComponents[0].Slot.ShouldBe(0);
    }

    [Fact]
    public void CalculateCriticalHitsData_ShouldReturnNull_WhenNoSlotsAvailable()
    {
        // Arrange
        var part = new Arm("TestArm", PartLocation.RightArm, 0, 10);
        part.CriticalHit(0); // destroy shoulder
        var mech = new Mech("TestChassis", "TestModel", 50, 5, [part]);

        _diceRoller.Roll2D6().Returns(
            [new(4), new(5)] // 9 total for crit roll
        );

        // Act
        var critsData = mech.CalculateCriticalHitsData(PartLocation.RightArm, _diceRoller, _damageTransferCalculator);

        // Assert
        critsData?.HitComponents.ShouldBeNull();
    }

    [Fact]
    public void CalculateCriticalHitsData_ShouldSetIsBlownOff_WhenCriticalRollIs12AndLocationCanBeBlownOff()
    {
        // Arrange
        var part = new Arm("TestArm", PartLocation.RightArm, 0, 10);
        var mech = new Mech("TestChassis", "TestModel", 50, 5, [part]);

        _diceRoller.Roll2D6().Returns(
            [new(6), new(6)] // 12 total for crit roll
        );

        // Act
        var critsData = mech.CalculateCriticalHitsData(PartLocation.RightArm, _diceRoller, _damageTransferCalculator);

        // Assert
        critsData.ShouldNotBeNull();
        critsData.IsBlownOff.ShouldBeTrue();
        critsData.NumCriticalHits.ShouldBe(0);
        critsData.HitComponents.ShouldBeNull();
    }

    [Fact]
    public void CalculateCriticalHitsData_ShouldNotSetIsBlownOff_WhenCriticalRollIs12AndLocationCannotBeBlownOff()
    {
        // Arrange
        var part = new CenterTorso("TestTorso", 0, 10, 6);
        var mech = new Mech("TestChassis", "TestModel", 50, 5, [part]);

        _diceRoller.Roll2D6().Returns(
            [new DiceResult(6), new DiceResult(6)] // 12 total for crit roll
        );
        _diceRoller.RollD6().Returns(
            new DiceResult(4),
            new DiceResult(5),
            new DiceResult(6));

        // Act
        var critsData = mech.CalculateCriticalHitsData(PartLocation.CenterTorso, _diceRoller, _damageTransferCalculator);

        // Assert
        critsData.ShouldNotBeNull();
        critsData.IsBlownOff.ShouldBeFalse();
        critsData.NumCriticalHits.ShouldBe(3);
        critsData.HitComponents.ShouldNotBeNull();
    }

    [Fact]
    public void CalculateCriticalHitsData_ShouldAllowHittingMultiSlotComponent_AfterOneSlotIsHit()
    {
        // Arrange
        var mech = new Mech("Test", "TST-1A", 50, 4, CreateBasicPartsData());
        var centerTorso = mech.Parts[PartLocation.CenterTorso];

        // Gyro is in slots 3-6 of center torso
        // First, hit slot 3
        centerTorso.CriticalHit(3);

        // Setup dice roller to return values that would hit slot 1
        _diceRoller.Roll2D6().Returns(
            [new DiceResult(5), new DiceResult(4)] // Total 9 for crit roll (1 crit)
        );
        _diceRoller.RollD6()
            .Returns(new DiceResult(4)); //that should go to the second group where only one slot is available (6) 

        // Act
        var critData = mech.CalculateCriticalHitsData(PartLocation.CenterTorso, _diceRoller, _damageTransferCalculator);

        // Assert
        critData.ShouldNotBeNull();
        critData.HitComponents.ShouldNotBeNull();

        // Verify that slot 3 is already marked as hit
        centerTorso.HitSlots.ShouldContain(3);
    }


    [Theory]
    [InlineData(0, 5, 5, 8)] // No heat, no penalty
    [InlineData(5, 5, 4, 6)] // 5 heat, -1 MP
    [InlineData(10, 5, 3, 5)] // 10 heat, -2 MP
    [InlineData(15, 5, 2, 3)] // 15 heat, -3 MP
    [InlineData(20, 5, 1, 2)] // 20 heat, -4 MP
    [InlineData(25, 5, 0, 0)] // 25 heat, -5 MP (reduced to 0)
    [InlineData(30, 5, 0, 0)] // 30+ heat, -5 MP and shutdown
    public void HeatShouldAffectMovementPoints(int heat, int baseMovement, int expectedWalkMp, int expectedRunMp)
    {
        // Arrange
        var sut = new Mech("Test", "TST-1A", 50, baseMovement, CreateBasicPartsData());

        // Act
        // Set heat and apply effects
        var heatData = new HeatData
        {
            MovementHeatSources = [],
            WeaponHeatSources =
            [
                new WeaponHeatData
                {
                    WeaponName = "TestWeapon",
                    HeatPoints = heat
                }
            ],
            DissipationData = default
        };

        // Apply heat effects
        sut.ApplyHeat(heatData);

        // Assert
        sut.GetMovementPoints(MovementType.Walk).ShouldBe(expectedWalkMp);
        sut.GetMovementPoints(MovementType.Run).ShouldBe(expectedRunMp);
        sut.MovementHeatPenalty?.Value.ShouldBe(baseMovement - expectedWalkMp);

        // Jumping MP should not be affected by heat
        var jumpJets = new JumpJets();
        var leftLeg = sut.Parts[PartLocation.LeftLeg];
        leftLeg.TryAddComponent(jumpJets);

        var originalJumpMp = jumpJets.JumpMp;
        sut.GetMovementPoints(MovementType.Jump).ShouldBe(originalJumpMp, "Jump MP should not be affected by heat");
    }

    [Fact]
    public void CanJump_WhenMechIsProne_ShouldReturnFalse()
    {
        // Arrange
        var parts = CreateBasicPartsData();
        var leftLeg = parts.Single(p => p.Location == PartLocation.LeftLeg);
        leftLeg.TryAddComponent(new JumpJets());

        var sut = new Mech("Test", "TST-1A", 50, 5, parts);
        sut.SetProne();

        // Act & Assert
        sut.CanJump.ShouldBeFalse("Prone mechs should not be able to jump");
    }

    [Fact]
    public void CanJump_WhenMechStoodUpThisPhase_ShouldReturnFalse()
    {
        // Arrange
        var parts = CreateBasicPartsData();
        var leftLeg = parts.Single(p => p.Location == PartLocation.LeftLeg);
        leftLeg.TryAddComponent(new JumpJets());

        var sut = new Mech("Test", "TST-1A", 50, 5, parts);
        sut.AttemptStandup(); // This increments StandupAttempts

        // Act & Assert
        sut.CanJump.ShouldBeFalse("Mechs that stood up this phase should not be able to jump");
    }

    [Fact]
    public void CanJump_WhenNoJumpJetsAvailable_ShouldReturnFalse()
    {
        // Arrange
        var parts = CreateBasicPartsData();
        var sut = new Mech("Test", "TST-1A", 50, 5, parts);

        // Act & Assert
        sut.CanJump.ShouldBeFalse("Mechs without jump jets should not be able to jump");
    }

    [Fact]
    public void CanJump_WhenJumpJetsDestroyed_ShouldReturnFalse()
    {
        // Arrange
        var parts = CreateBasicPartsData();
        var leftLeg = parts.Single(p => p.Location == PartLocation.LeftLeg);
        var jumpJets = new JumpJets();
        leftLeg.TryAddComponent(jumpJets);

        var sut = new Mech("Test", "TST-1A", 50, 5, parts);

        // Destroy the jump jets
        jumpJets.Hit();

        // Act & Assert
        sut.CanJump.ShouldBeFalse("Mechs with destroyed jump jets should not be able to jump");
    }

    [Fact]
    public void CanJump_WhenAllConditionsMet_ShouldReturnTrue()
    {
        // Arrange
        var parts = CreateBasicPartsData();
        var leftLeg = parts.Single(p => p.Location == PartLocation.LeftLeg);
        leftLeg.TryAddComponent(new JumpJets());

        var sut = new Mech("Test", "TST-1A", 50, 5, parts);

        // Act & Assert
        sut.CanJump.ShouldBeTrue(
            "Mechs with functional jump jets that are not prone and haven't stood up should be able to jump");
    }

    [Fact]
    public void CanJump_AfterResetTurnState_ShouldReturnTrue()
    {
        // Arrange
        var parts = CreateBasicPartsData();
        var leftLeg = parts.Single(p => p.Location == PartLocation.LeftLeg);
        leftLeg.TryAddComponent(new JumpJets());

        var sut = new Mech("Test", "TST-1A", 50, 5, parts);
        sut.AttemptStandup(); // This increments StandupAttempts
        sut.CanJump.ShouldBeFalse("Mech that attempted standup this phase should not be able to jump");

        // Reset turn state (this should reset StandupAttempts to 0)
        sut.ResetTurnState();

        // Act & Assert
        sut.CanJump.ShouldBeTrue("Mech should be able to jump after standing up and resetting turn state");
    }

    [Fact]
    public void CanRun_ShouldReturnTrue_ForNewMech()
    {
        // Arrange
        var sut = new Mech("Test", "TST-1A", 50, 5, CreateBasicPartsData());

        // Act & Assert
        sut.CanRun.ShouldBeTrue();
    }

    [Fact]
    public void CanRun_WhenMechIsProne_ShouldReturnTrue()
    {
        // Arrange
        var sut = new Mech("Test", "TST-1A", 50, 5, CreateBasicPartsData());
        sut.SetProne();

        // Act & Assert
        sut.CanRun.ShouldBeTrue();
    }

    [Fact]
    public void CanRun_ShouldReturnFalse_WhenLegIsBlownOff()
    {
        // Arrange
        var sut = new Mech("Test", "TST-1A", 50, 0, CreateBasicPartsData());
        var leg = sut.Parts[PartLocation.LeftLeg];
        leg.BlowOff();

        // Act & Assert
        sut.CanRun.ShouldBeFalse();
    }

    [Fact]
    public void CanRun_ShouldReturnFalse_WhenLegIsDestroyed()
    {
        // Arrange
        var sut = new Mech("Test", "TST-1A", 50, 0, CreateBasicPartsData());
        var leg = sut.Parts[PartLocation.LeftLeg];
        leg.ApplyDamage(100, HitDirection.Front);
        leg.IsDestroyed.ShouldBeTrue();

        // Act & Assert
        sut.CanRun.ShouldBeFalse();
    }

    [Fact]
    public void IsPsrForJumpRequired_WithUndamagedGyro_ReturnsFalse()
    {
        // Arrange
        var sut = new Mech("Test", "TST-1A", 50, 5, CreateBasicPartsData());

        var gyro = sut.GetAllComponents<Gyro>().First();

        // Act
        var result = sut.IsPsrForJumpRequired();

        // Assert
        result.ShouldBeFalse();
        gyro.Hits.ShouldBe(0);
    }

    [Fact]
    public void IsPsrForJumpRequired_WithDamagedGyro_ReturnsTrue()
    {
        // Arrange
        var sut = new Mech("Test", "TST-1A", 50, 5, CreateBasicPartsData());
        var gyro = sut.GetAllComponents<Gyro>().First();
        gyro.Hit(); // Damage to the gyro (1 hit)

        // Act
        var result = sut.IsPsrForJumpRequired();

        // Assert
        result.ShouldBeTrue();
        gyro.Hits.ShouldBe(1);
        gyro.IsDestroyed.ShouldBeFalse();
    }

    [Fact]
    public void IsPsrForJumpRequired_WithDestroyedGyro_ReturnsFalse()
    {
        // Arrange
        var sut = new Mech("Test", "TST-1A", 50, 5, CreateBasicPartsData());
        var gyro = sut.GetAllComponents<Gyro>().First();
        gyro.Hit(); // First hit
        gyro.Hit(); // Second hit - destroys the gyro

        // Act
        var result = sut.IsPsrForJumpRequired();

        // Assert
        result.ShouldBeFalse();
        gyro.Hits.ShouldBe(2);
        gyro.IsDestroyed.ShouldBeTrue();
    }

    [Fact]
    public void IsPsrForJumpRequired_WithOneDestroyedFootActuator_ReturnsTrue()
    {
        // Arrange
        var sut = new Mech("Test", "TST-1A", 50, 5, CreateBasicPartsData());
        var footActuator = sut.GetAllComponents<FootActuator>().First();
        footActuator.Hit(); // Destroy the foot actuator

        // Act
        var result = sut.IsPsrForJumpRequired();

        // Assert
        result.ShouldBeTrue();
        footActuator.IsDestroyed.ShouldBeTrue();
    }

    [Fact]
    public void IsPsrForJumpRequired_WithMultipleDestroyedFootActuators_ReturnsTrue()
    {
        // Arrange
        var sut = new Mech("Test", "TST-1A", 50, 5, CreateBasicPartsData());
        var footActuators = sut.GetAllComponents<FootActuator>().ToList();

        // Destroy all foot actuators
        foreach (var actuator in footActuators)
        {
            actuator.Hit();
        }

        // Act
        var result = sut.IsPsrForJumpRequired();

        // Assert
        result.ShouldBeTrue();
        footActuators.ShouldAllBe(actuator => actuator.IsDestroyed);
    }

    [Fact]
    public void IsPsrForJumpRequired_WithOneDestroyedHipActuator_ReturnsTrue()
    {
        // Arrange
        var sut = new Mech("Test", "TST-1A", 50, 5, CreateBasicPartsData());
        var hipActuator = sut.GetAllComponents<HipActuator>().First();
        hipActuator.Hit(); // Destroy the hip actuator

        // Act
        var result = sut.IsPsrForJumpRequired();

        // Assert
        result.ShouldBeTrue();
        hipActuator.IsDestroyed.ShouldBeTrue();
    }

    [Fact]
    public void IsPsrForJumpRequired_WithMultipleDestroyedHipActuators_ReturnsTrue()
    {
        // Arrange
        var sut = new Mech("Test", "TST-1A", 50, 5, CreateBasicPartsData());
        var hipActuators = sut.GetAllComponents<HipActuator>().ToList();

        // Destroy all hip actuators
        foreach (var actuator in hipActuators)
        {
            actuator.Hit();
        }

        // Act
        var result = sut.IsPsrForJumpRequired();

        // Assert
        result.ShouldBeTrue();
        hipActuators.ShouldAllBe(actuator => actuator.IsDestroyed);
    }

    [Fact]
    public void IsPsrForJumpRequired_WithOneDestroyedLowerLegActuator_ReturnsTrue()
    {
        // Arrange
        var sut = new Mech("Test", "TST-1A", 50, 5, CreateBasicPartsData());
        var lowerLegActuator = sut.GetAllComponents<LowerLegActuator>().First();
        lowerLegActuator.Hit(); // Destroy the lower leg actuator

        // Act
        var result = sut.IsPsrForJumpRequired();

        // Assert
        result.ShouldBeTrue();
        lowerLegActuator.IsDestroyed.ShouldBeTrue();
    }

    [Fact]
    public void IsPsrForJumpRequired_WithMultipleDestroyedLowerLegActuators_ReturnsTrue()
    {
        // Arrange
        var sut = new Mech("Test", "TST-1A", 50, 5, CreateBasicPartsData());
        var lowerLegActuators = sut.GetAllComponents<LowerLegActuator>().ToList();

        // Destroy all lower leg actuators
        foreach (var actuator in lowerLegActuators)
        {
            actuator.Hit();
        }

        // Act
        var result = sut.IsPsrForJumpRequired();

        // Assert
        result.ShouldBeTrue();
        lowerLegActuators.ShouldAllBe(actuator => actuator.IsDestroyed);
    }

    [Fact]
    public void IsPsrForJumpRequired_WithOneDestroyedUpperLegActuator_ReturnsTrue()
    {
        // Arrange
        var sut = new Mech("Test", "TST-1A", 50, 5, CreateBasicPartsData());
        var upperLegActuator = sut.GetAllComponents<UpperLegActuator>().First();
        upperLegActuator.Hit(); // Destroy the upper leg actuator

        // Act
        var result = sut.IsPsrForJumpRequired();

        // Assert
        result.ShouldBeTrue();
        upperLegActuator.IsDestroyed.ShouldBeTrue();
    }

    [Fact]
    public void IsPsrForJumpRequired_WithMultipleDestroyedUpperLegActuators_ReturnsTrue()
    {
        // Arrange
        var sut = new Mech("Test", "TST-1A", 50, 5, CreateBasicPartsData());
        var upperLegActuators = sut.GetAllComponents<UpperLegActuator>().ToList();

        // Destroy all upper leg actuators
        foreach (var actuator in upperLegActuators)
        {
            actuator.Hit();
        }

        // Act
        var result = sut.IsPsrForJumpRequired();

        // Assert
        result.ShouldBeTrue();
        upperLegActuators.ShouldAllBe(actuator => actuator.IsDestroyed);
    }

    [Fact]
    public void HeatDissipation_ShouldReduceHeatAndRestoreMovement()
    {
        // Arrange
        var sut = new Mech("Test", "TST-1A", 50, 5, CreateBasicPartsData());

        // Set the initial heat to 15 (3 MP penalty)
        sut.ApplyHeat(new HeatData
        {
            MovementHeatSources = [],
            WeaponHeatSources =
            [
                new WeaponHeatData
                {
                    WeaponName = "TestWeapon",
                    HeatPoints = 15
                }
            ],
            DissipationData = default
        });

        // Verify initial state
        sut.GetMovementPoints(MovementType.Walk).ShouldBe(2, "Initial walking MP should be reduced by 3");

        // Act - apply heat with dissipation that reduces heat to 3
        sut.ApplyHeat(new HeatData
        {
            MovementHeatSources = [],
            WeaponHeatSources = [],
            DissipationData = new HeatDissipationData
            {
                DissipationPoints = 12,
                HeatSinks = 0,
                EngineHeatSinks = 0
            }
        });

        // Assert
        sut.CurrentHeat.ShouldBe(3, "Heat should be reduced by dissipation");
        sut.GetMovementPoints(MovementType.Walk).ShouldBe(5, "Walking MP should be fully restored");
        sut.GetMovementPoints(MovementType.Run).ShouldBe(8, "Running MP should be fully restored");
    }

    [Theory]
    [InlineData(0, 0)] // No heat, no penalty
    [InlineData(7, 0)] // Below the first threshold, no penalty
    [InlineData(8, 1)] // At the first threshold, +1 penalty
    [InlineData(12, 1)] // Between the first and second threshold, +1 penalty
    [InlineData(13, 2)] // At the second threshold, +2 penalty
    [InlineData(16, 2)] // Between the second and third threshold, +2 penalty
    [InlineData(17, 3)] // At the third threshold, +3 penalty
    [InlineData(23, 3)] // Between the third and fourth threshold, +3 penalty
    [InlineData(24, 4)] // At the fourth threshold, +4 penalty
    [InlineData(30, 4)] // Above the fourth threshold, +4 penalty
    public void AttackHeatPenalty_ShouldReturnCorrectPenalty(int heat, int expectedPenalty)
    {
        // Arrange
        var sut = new Mech("Test", "TST-1A", 50, 5, CreateBasicPartsData());

        // Act
        // Set heat and apply effects
        var heatData = new HeatData
        {
            MovementHeatSources = [],
            WeaponHeatSources =
            [
                new WeaponHeatData
                {
                    WeaponName = "TestWeapon",
                    HeatPoints = heat
                }
            ],
            DissipationData = default
        };

        // Apply heat effects
        sut.ApplyHeat(heatData);

        // Assert
        sut.AttackHeatPenalty?.Value.ShouldBe(expectedPenalty,
            $"Heat level {heat} should result in attack penalty of {expectedPenalty}");
    }

    [Fact]
    public void HeatDissipation_ShouldReduceHeatAndRestoreAttackPenalty()
    {
        // Arrange
        var sut = new Mech("Test", "TST-1A", 50, 5, CreateBasicPartsData());

        // Set initial heat to 17 (attack penalty +3)
        sut.ApplyHeat(new HeatData
        {
            MovementHeatSources = [],
            WeaponHeatSources =
            [
                new WeaponHeatData
                {
                    WeaponName = "TestWeapon",
                    HeatPoints = 17
                }
            ],
            DissipationData = default
        });

        // Verify initial state
        sut.AttackHeatPenalty?.Value.ShouldBe(3, "Initial attack penalty should be +3");

        // Act - apply heat with dissipation that reduces heat to 7
        sut.ApplyHeat(new HeatData
        {
            MovementHeatSources = [],
            WeaponHeatSources = [],
            DissipationData = new HeatDissipationData
            {
                DissipationPoints = 10,
                HeatSinks = 0,
                EngineHeatSinks = 0
            }
        });

        // Assert
        sut.CurrentHeat.ShouldBe(7, "Heat should be reduced by dissipation");
        sut.AttackHeatPenalty.ShouldBeNull();
    }

    [Fact]
    public void EngineHeatPenalty_ReturnsCorrectValue_WhenEngineHasHits()
    {
        // Arrange
        var parts = CreateBasicPartsData();
        var centerTorso = parts.Single(p => p.Location == PartLocation.CenterTorso);
        var engine = centerTorso.GetComponent<Engine>()!;
        var mech = new Mech("Test", "TST-1A", 50, 4, parts);

        // Apply one hit to the engine
        engine.Hit();

        // Act
        var engineHeatPenalty = mech.EngineHeatPenalty;

        // Assert
        engineHeatPenalty?.Value.ShouldBe(5, "Engine with one hit should have +5 heat penalty");
    }
    
    [Theory]
    [InlineData(2, 0)]
    [InlineData(15, 1)]
    [InlineData(26, 2)]
    [InlineData(30, 2)]
    [InlineData(15, 0, false)]
    [InlineData(26, 0, false)]
    [InlineData(30, 0, false)]
    public void ApplyHeat_ShouldDamagePilot_WhenHeatIsHigh_AndLifeSupportIsDestroyed(int heatPoints, int injuriesExpected, bool destroyLifeSupport = true)
    {
        // Arrange
        var sut = new Mech("Test", "TST-1A", 50, 5, CreateBasicPartsData());
        sut.AssignPilot(new MechWarrior("John", "Doe"));
        if (destroyLifeSupport)
        {
            var lifeSupport = sut.GetAllComponents<LifeSupport>().First();
            lifeSupport.Hit();
        }

        // Act
        sut.ApplyHeat(new HeatData
        {
            MovementHeatSources = [],
            WeaponHeatSources =
            [
                new WeaponHeatData
                {
                    WeaponName = "TestWeapon",
                    HeatPoints = heatPoints
                }
            ],
            DissipationData = default
        });

        // Assert
        sut.Pilot?.Injuries.ShouldBe(injuriesExpected);
        if (injuriesExpected <= 0) return;
        var uiEvent = sut.DequeueNotification();
        uiEvent.ShouldNotBeNull();
        uiEvent.Type.ShouldBe(UiEventType.PilotDamage);
        uiEvent.Parameters.Length.ShouldBe(2);
        uiEvent.Parameters[0].ShouldBe("John");
        uiEvent.Parameters[1].ShouldBe(injuriesExpected);
    }

    [Fact]
    public void GetHeatData_IncludesEngineHeatInTotalHeatPoints()
    {
        // Arrange
        var parts = CreateBasicPartsData();
        var centerTorso = parts.Single(p => p.Location == PartLocation.CenterTorso);
        var engine = centerTorso.GetComponent<Engine>()!;
        var mech = new Mech("Test", "TST-1A", 50, 4, parts);

        // Apply one hit to the engine
        engine.Hit();

        // Act
        var heatData = mech.GetHeatData(Substitute.For<IRulesProvider>());

        // Assert
        heatData.TotalHeatPoints.ShouldBe(5, "Total heat should include engine heat penalty");
        heatData.EngineHeatSource.ShouldNotBeNull();
        heatData.EngineHeatSource.EngineHits.ShouldBe(1);
        heatData.EngineHeatSource.Value.ShouldBe(5);
    }

    [Fact]
    public void CanStandup_ShouldReturnTrue_WhenHasMovementPointsAndPilotConscious()
    {
        // Arrange
        var parts = CreateBasicPartsData();
        var sut = new Mech("Test", "TST-1A", 50, 4, parts);
        sut.AssignPilot(new MechWarrior("John", "Doe"));
        sut.SetProne();

        // Act
        var canStandup = sut.CanStandup();

        // Assert
        canStandup.ShouldBeTrue("Mech should be able to stand up when it has movement points and pilot is conscious");
    }

    [Theory]
    [InlineData(0, false)] // No movement points, unconscious pilot
    [InlineData(0, true)] // No movement points, conscious pilot 
    [InlineData(4, true)] // Has movement points, unconscious pilot
    public void CanStandup_ShouldReturnFalse_WhenPilotUnconscious(int walkMp, bool pilotUnconscious)
    {
        // Arrange
        var parts = CreateBasicPartsData();
        var sut = new Mech("Test", "TST-1A", 50, walkMp, parts);
        sut.SetProne();

        // Mock pilot with a specified consciousness state
        var pilot = Substitute.For<IPilot>();
        pilot.IsConscious.Returns(!pilotUnconscious);
        typeof(Mech).GetProperty("Pilot")?.SetValue(sut, pilot);

        // Act
        var canStandup = sut.CanStandup();

        // Assert
        canStandup.ShouldBe(false,
            $"Mech should not stand up with walkMP={walkMp} and pilotUnconscious={pilotUnconscious}");
    }

    [Fact]
    public void AttemptStandup_WhenCalledMultipleTimes_ShouldIncrementCounterCorrectly()
    {
        // Arrange
        var mech = new Mech("Test", "TST-1A", 50, 4, CreateBasicPartsData());

        // Act
        mech.AttemptStandup();
        mech.AttemptStandup();
        // Assert
        mech.StandupAttempts.ShouldBe(2);
        mech.MovementPointsSpent.ShouldBe(4);
        mech.GetMovementPoints(MovementType.Walk).ShouldBe(0); // 4 initial - 2*2 spent
    }

    [Theory]
    [InlineData(1, 1)] // Less movement than standup cost
    [InlineData(2, 2)] // Exactly standup cost  
    [InlineData(4, 2)] // More movement than standup cost
    public void AttemptStandup_ShouldSpendCorrectMovementPoints(int initialMovement, int expectedSpent)
    {
        // Arrange
        var mech = new Mech("Test", "TST-1A", 50, initialMovement, CreateBasicPartsData());

        // Act
        mech.AttemptStandup();
        // Assert
        mech.StandupAttempts.ShouldBe(1);
        mech.MovementPointsSpent.ShouldBe(expectedSpent);
        mech.GetMovementPoints(MovementType.Walk).ShouldBe(initialMovement - expectedSpent);
    }

    [Fact]
    public void CanStandup_ShouldReturnFalse_WhenHasMovementPointsAndPilotConsciousAndShutdown()
    {
        // Arrange
        var parts = CreateBasicPartsData();
        var sut = new Mech("Test", "TST-1A"  , 50, 4, parts);
        sut.AssignPilot(new MechWarrior("John", "Doe"));
        sut.SetProne();
        var shutdownData = new ShutdownData { Reason = ShutdownReason.Voluntary, Turn = 1 };
        sut.Shutdown(shutdownData);

        // Act
        var canStandup = sut.CanStandup();

        // Assert
        canStandup.ShouldBeFalse(
            "Mech should not be able to stand up when it has movement points and pilot is conscious but shutdown");
    }

    [Fact]
    public void CanStandup_ShouldReturnFalse_WhenBothLegsDestroyed()
    {
        // Arrange
        var parts = CreateBasicPartsData();
        var sut = new Mech("Test", "TST-1A", 50, 4, parts);
        sut.SetProne();

        var leftLeg = sut.Parts[PartLocation.LeftLeg];
        leftLeg.ApplyDamage(100, HitDirection.Front);
        leftLeg.IsDestroyed.ShouldBeTrue();
        var rightLeg = sut.Parts[PartLocation.RightLeg];
        rightLeg.ApplyDamage(100, HitDirection.Front);
        rightLeg.IsDestroyed.ShouldBeTrue();

        // Act
        var canStandup = sut.CanStandup();

        // Assert
        canStandup.ShouldBeFalse("Mech should not be able to stand up when both legs are destroyed");
    }

    [Fact]
    public void CanStandup_ShouldReturnFalse_WhenBothLegsBlownOff()
    {
        // Arrange
        var parts = CreateBasicPartsData();
        var sut = new Mech("Test", "TST-1A", 50, 4, parts);
        sut.SetProne();

        var leftLeg = sut.Parts[PartLocation.LeftLeg];
        leftLeg.BlowOff();
        leftLeg.IsBlownOff.ShouldBeTrue();
        var rightLeg = sut.Parts[PartLocation.RightLeg];
        rightLeg.BlowOff();
        rightLeg.IsBlownOff.ShouldBeTrue();

        // Act
        var canStandup = sut.CanStandup();

        // Assert
        canStandup.ShouldBeFalse("Mech should not be able to stand up when both legs are blown off");
    }

    [Fact]
    public void CanStandup_ShouldReturnFalse_WhenOneLegIsBlownOffAndAnotherIsDestroyed()
    {
        // Arrange
        var parts = CreateBasicPartsData();
        var sut = new Mech("Test", "TST-1A", 50, 4, parts);
        sut.SetProne();

        var leftLeg = sut.Parts[PartLocation.LeftLeg];
        leftLeg.BlowOff();
        leftLeg.IsBlownOff.ShouldBeTrue();
        var rightLeg = sut.Parts[PartLocation.RightLeg];
        rightLeg.ApplyDamage(100, HitDirection.Front);
        rightLeg.IsDestroyed.ShouldBeTrue();

        // Act
        var canStandup = sut.CanStandup();

        // Assert
        canStandup.ShouldBeFalse("Mech should not be able to stand up when both legs are not available");
    }

    [Fact]
    public void CanStandup_ShouldReturnFalse_WhenGyroIsDestroyed()
    {
        // Arrange
        var parts = CreateBasicPartsData();
        var sut = new Mech("Test", "TST-1A", 50, 4, parts);
        sut.SetProne();

        var gyro = sut.GetAvailableComponents<Gyro>().First();
        gyro.Hit();
        gyro.Hit();
        gyro.IsDestroyed.ShouldBeTrue();

        // Act
        var canStandup = sut.CanStandup();

        // Assert
        canStandup.ShouldBeFalse("Mech should not be able to stand up when gyro is destroyed");
    }
    
    [Fact]
    public void CanChangeFacingWhileProne_WhenMechIsNotProne_ShouldReturnFalse()
    {
        // Arrange
        var sut = new Mech("Test", "TST-1A", 50, 4, CreateBasicPartsData());

        // Act & Assert
        sut.CanChangeFacingWhileProne()
            .ShouldBeFalse("Non-prone mechs should not be able to change facing while prone");
    }

    [Fact]
    public void CanChangeFacingWhileProne_WhenMechIsProneAndHasMovementPoints_ShouldReturnTrue()
    {
        // Arrange
        var sut = new Mech("Test", "TST-1A", 50, 4, CreateBasicPartsData());
        sut.SetProne();

        // Act & Assert
        sut.CanChangeFacingWhileProne()
            .ShouldBeTrue("Prone mechs with movement points should be able to change facing");
    }

    [Fact]
    public void CanChangeFacingWhileProne_WhenMechIsProneButShutdown_ShouldReturnFalse()
    {
        // Arrange
        var sut = new Mech("Test", "TST-1A", 50, 4, CreateBasicPartsData());
        sut.SetProne();
        var shutdownData = new ShutdownData { Reason = ShutdownReason.Voluntary, Turn = 1 };
        sut.Shutdown(shutdownData);

        // Act & Assert
        sut.CanChangeFacingWhileProne().ShouldBeFalse("Shutdown mechs should not be able to change facing");
    }

    [Fact]
    public void CanChangeFacingWhileProne_WhenMechIsProneButNoMovementPoints_ShouldReturnFalse()
    {
        // Arrange
        var sut = new Mech("Test", "TST-1A", 50, 0, CreateBasicPartsData());
        sut.SetProne();

        // Act & Assert
        sut.CanChangeFacingWhileProne()
            .ShouldBeFalse("Mechs without movement points should not be able to change facing");
    }

    [Fact]
    public void CanFireWeapons_ShouldReturnFalse_WhenUnitIsDestroyed()
    {
        // Arrange
        var sut = new Mech("Test", "TST-1A", 50, 0, CreateBasicPartsData());
        sut.ApplyDamage([CreateHitDataForLocation(PartLocation.Head, 100)], HitDirection.Front);

        // Act
        var result = sut.CanFireWeapons;

        // Assert
        result.ShouldBeFalse();
    }
    
    [Fact]
    public void CanFireWeapons_ShouldReturnFalse_WhenUnitIsImmobile()
    {
        // Arrange
        var sut = new Mech("Test", "TST-1A", 50, 0, CreateBasicPartsData());
        sut.AssignPilot(new MechWarrior("John", "Doe"));
        sut.Shutdown(new ShutdownData { Reason = ShutdownReason.Voluntary, Turn = 1 });

        // Act
        var result = sut.CanFireWeapons;

        // Assert
        result.ShouldBeFalse();
    }
    
    [Fact]
    public void CanFireWeapons_ShouldReturnTrue_WhenSensorsAreIntact()
    {
        // Arrange
        var sut = new Mech("Test", "TST-1A", 50, 0, CreateBasicPartsData());
        sut.AssignPilot(new MechWarrior("John", "Doe"));

        // Act
        var result = sut.CanFireWeapons;

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void CanFireWeapons_ShouldReturnTrue_WhenSensorsHaveOneHit()
    {
        // Arrange
        var sut = new Mech("Test", "TST-1A", 50, 0, CreateBasicPartsData());
        sut.AssignPilot(new MechWarrior("John", "Doe"));
        var sensors = sut.GetAllComponents<Sensors>().First();
        sensors.Hit();

        // Act
        var result = sut.CanFireWeapons;

        // Assert
        result.ShouldBeTrue();
        sensors.Hits.ShouldBe(1);
        sensors.IsDestroyed.ShouldBeFalse();
    }

    [Fact]
    public void CanFireWeapons_ShouldReturnFalse_WhenSensorsAreDestroyed()
    {
        // Arrange
        var sut = new Mech("Test", "TST-1A", 50, 0, CreateBasicPartsData());
        sut.AssignPilot(new MechWarrior("John", "Doe"));
        var sensors = sut.GetAllComponents<Sensors>().First();
        sensors.Hit(); // First hit
        sensors.Hit(); // Second hit - destroys sensors

        // Act
        var result = sut.CanFireWeapons;

        // Assert
        result.ShouldBeFalse();
        sensors.Hits.ShouldBe(2);
        sensors.IsDestroyed.ShouldBeTrue();
    }

    [Fact]
    public void GetMovementPoints_WithDestroyedHipActuator_ShouldHalveWalkingMP()
    {
        // Arrange
        var sut = new Mech("Test", "TST-1A", 50, 6, CreateBasicPartsData());
        var hipActuator = sut.GetAllComponents<HipActuator>().First();

        // Act
        hipActuator.Hit(); // Destroy hip actuator

        // Assert
        sut.GetMovementPoints(MovementType.Walk).ShouldBe(3, "Hip actuator damage should halve walking MP (6/2=3)");
        sut.GetMovementPoints(MovementType.Run)
            .ShouldBe(5, "Running MP should be 1.5 times walking MP rounded up (3*1.5=4.5→5)");
        hipActuator.IsDestroyed.ShouldBeTrue();
    }

    [Fact]
    public void GetMovementPoints_WithTwoDestroyedHipActuators_ShouldReduceWalkingMPToZero()
    {
        // Arrange
        var sut = new Mech("Test", "TST-1A", 50, 6, CreateBasicPartsData());
        var hipActuators = sut.GetAllComponents<HipActuator>().ToList();

        // Act
        foreach (var actuator in hipActuators)
        {
            actuator.Hit(); // Destroy both hip actuators
        }

        // Assert
        sut.GetMovementPoints(MovementType.Walk)
            .ShouldBe(0, "Two destroyed hip actuators should reduce walking MP to 0");
        sut.GetMovementPoints(MovementType.Run).ShouldBe(0, "Running MP should also be 0");
        hipActuators.ShouldAllBe(actuator => actuator.IsDestroyed);
    }

    [Fact]
    public void GetMovementPoints_WithDestroyedFootActuator_ShouldReduceWalkingMPByOne()
    {
        // Arrange
        var sut = new Mech("Test", "TST-1A", 50, 6, CreateBasicPartsData());
        var footActuator = sut.GetAllComponents<FootActuator>().First();

        // Act
        footActuator.Hit(); // Destroy foot actuator

        // Assert
        sut.GetMovementPoints(MovementType.Walk)
            .ShouldBe(5, "Destroyed foot actuator should reduce walking MP by 1 (6-1=5)");
        sut.GetMovementPoints(MovementType.Run)
            .ShouldBe(8, "Running MP should be 1.5 times walking MP rounded up (5*1.5=7.5→8)");
        footActuator.IsDestroyed.ShouldBeTrue();
    }

    [Fact]
    public void GetMovementPoints_WithDestroyedLowerLegActuator_ShouldReduceWalkingMPByOne()
    {
        // Arrange
        var sut = new Mech("Test", "TST-1A", 50, 6, CreateBasicPartsData());
        var lowerLegActuator = sut.GetAllComponents<LowerLegActuator>().First();

        // Act
        lowerLegActuator.Hit(); // Destroy the lower leg actuator

        // Assert
        sut.GetMovementPoints(MovementType.Walk)
            .ShouldBe(5, "Destroyed lower leg actuator should reduce walking MP by 1 (6-1=5)");
        sut.GetMovementPoints(MovementType.Run)
            .ShouldBe(8, "Running MP should be 1.5 times walking MP rounded up (5*1.5=7.5→8)");
        lowerLegActuator.IsDestroyed.ShouldBeTrue();
    }

    [Fact]
    public void GetMovementPoints_WithDestroyedUpperLegActuator_ShouldReduceWalkingMPByOne()
    {
        // Arrange
        var sut = new Mech("Test", "TST-1A", 50, 6, CreateBasicPartsData());
        var upperLegActuator = sut.GetAllComponents<UpperLegActuator>().First();

        // Act
        upperLegActuator.Hit(); // Destroy upper leg actuator

        // Assert
        sut.GetMovementPoints(MovementType.Walk)
            .ShouldBe(5, "Destroyed upper leg actuator should reduce walking MP by 1 (6-1=5)");
        sut.GetMovementPoints(MovementType.Run)
            .ShouldBe(8, "Running MP should be 1.5 times walking MP rounded up (5*1.5=7.5→8)");
        upperLegActuator.IsDestroyed.ShouldBeTrue();
    }

    [Fact]
    public void GetMovementPoints_WithMultipleDestroyedActuators_ShouldStackPenalties()
    {
        // Arrange
        var sut = new Mech("Test", "TST-1A", 50, 6, CreateBasicPartsData());
        var footActuator = sut.GetAllComponents<FootActuator>().First();
        var lowerLegActuator = sut.GetAllComponents<LowerLegActuator>().First();
        var upperLegActuator = sut.GetAllComponents<UpperLegActuator>().First();

        // Act
        footActuator.Hit(); // -1 MP
        lowerLegActuator.Hit(); // -1 MP
        upperLegActuator.Hit(); // -1 MP

        // Assert
        sut.GetMovementPoints(MovementType.Walk)
            .ShouldBe(3, "Three destroyed actuators should reduce walking MP by 3 (6-3=3)");
        sut.GetMovementPoints(MovementType.Run)
            .ShouldBe(5, "Running MP should be 1.5 times walking MP rounded up (3*1.5=4.5→5)");
    }

    [Fact]
    public void GetMovementPoints_WithBlownOffLeg_ShouldSetWalkingMPToOne()
    {
        // Arrange
        var sut = new Mech("Test", "TST-1A", 50, 6, CreateBasicPartsData());
        var leftLeg = sut.Parts[PartLocation.LeftLeg];

        // Act
        leftLeg.BlowOff();

        // Assert
        sut.GetMovementPoints(MovementType.Walk).ShouldBe(1, "Blown off leg should set walking MP to 1");
        sut.CanRun.ShouldBeFalse();
        leftLeg.IsBlownOff.ShouldBeTrue();
    }

    [Theory]
    [InlineData(6, 3, 2, 1, 2)] // Base 6 MP: Hip halves to 3, foot -1 = 2, heat -1 = 1, run = 2
    [InlineData(4, 2, 1, 0, 0)] // Base 4 MP: Hip halves to 2, foot -1 = 1, heat -1 = 0, run = 0
    [InlineData(8, 4, 3, 2, 3)] // Base 8 MP: Hip halves to 4, foot -1 = 3, heat -1 = 2, run = 3
    public void GetMovementPoints_ScenarioTest_HipFootAndHeatDamage(int baseMp, int afterHip, int afterFoot,
        int expectedWalk, int expectedRun)
    {
        // Arrange - Scenario: Destroyed Hip, Destroyed Foot, Heat Level 6 Points
        var sut = new Mech("Test", "TST-1A", 50, baseMp, CreateBasicPartsData());

        // Step 1: Apply Hip Actuator Critical Hit (halves walking MP, rounded up)
        var hipActuator = sut.GetAllComponents<HipActuator>().First();
        hipActuator.Hit();
        sut.GetMovementPoints(MovementType.Walk).ShouldBe(afterHip, $"After hip damage: {baseMp}/2 = {afterHip}");

        // Step 2: Apply Foot Actuator Critical Hit (reduce by 1)
        var footActuator = sut.GetAllComponents<FootActuator>().First();
        footActuator.Hit();
        sut.GetMovementPoints(MovementType.Walk).ShouldBe(afterFoot, $"After foot damage: {afterHip}-1 = {afterFoot}");

        // Step 3: Apply Heat Level Penalty (6 heat points = -1 MP)
        var heatData = new HeatData
        {
            MovementHeatSources = [],
            WeaponHeatSources =
            [
                new WeaponHeatData
                {
                    WeaponName = "TestWeapon",
                    HeatPoints = 6
                }
            ],
            DissipationData = default
        };
        sut.ApplyHeat(heatData);

        // Final assertions
        sut.GetMovementPoints(MovementType.Walk)
            .ShouldBe(expectedWalk, $"Final walking MP after all penalties: {expectedWalk}");
        sut.GetMovementPoints(MovementType.Run)
            .ShouldBe(expectedRun, $"Final running MP: {expectedWalk} * 1.5 = {expectedRun}");
    }

    [Fact]
    public void GetMovementPoints_ForUnknownMovement_ShouldReturnZero()
    {
        // Arrange
        var sut = new Mech("Test", "TST-1A", 50, 6, CreateBasicPartsData());

        // Act
        var result = sut.GetMovementPoints((MovementType)233);

        // Assert
        result.ShouldBe(0);
    }
    
    [Fact]
    public void IsImmobile_ShouldReturnTrue_WhenPilotIsNotAssigned()
    {
        // Arrange
        var sut = new Mech("Test", "TST-1A", 50, 6, CreateBasicPartsData());

        // Act & Assert
        sut.IsImmobile.ShouldBeTrue("A mech without a pilot should be immobile");
    }

    [Fact]
    public void IsImmobile_ShouldReturnTrue_WhenPilotIsUnconscious()
    {
        // Arrange
        var sut = new Mech("Test", "TST-1A", 50, 6, CreateBasicPartsData());
        var pilot = Substitute.For<IPilot>();
        pilot.IsConscious.Returns(false);
        sut.AssignPilot(pilot);

        // Act & Assert
        sut.IsImmobile.ShouldBeTrue("A mech with unconscious pilot should be immobile");
    }

    [Fact]
    public void Status_ShouldBeImmobile_WhenIsImmobileIsTrue()
    {
        // Arrange
        var sut = new Mech("Test", "TST-1A", 50, 6, CreateBasicPartsData());
        var pilot = Substitute.For<IPilot>();
        pilot.IsConscious.Returns(false);
        sut.AssignPilot(pilot);

        // Act
        sut.Status.ShouldHaveFlag(UnitStatus.Immobile);
    }

    [Fact]
    public void IsImmobile_WhenPilotIsConscious_ShouldReturnFalse()
    {
        // Arrange
        var sut = new Mech("Test", "TST-1A", 50, 6, CreateBasicPartsData());
        var pilot = Substitute.For<IPilot>();
        pilot.IsConscious.Returns(true);
        sut.AssignPilot(pilot);

        // Act & Assert
        sut.IsImmobile.ShouldBeFalse("A mech with conscious pilot should not be immobile");
    }

    [Fact]
    public void IsImmobile_WhenMechIsShutdown_ShouldReturnTrue()
    {
        // Arrange
        var sut = new Mech("Test", "TST-1A", 50, 6, CreateBasicPartsData());
        var shutdownData = new ShutdownData { Reason = ShutdownReason.Voluntary, Turn = 1 };
        sut.Shutdown(shutdownData);

        // Act & Assert
        sut.IsImmobile.ShouldBeTrue("A shutdown mech should be immobile");
    }

    [Fact]
    public void IsImmobile_WhenBothLegsAndBothArmsDestroyed_ShouldReturnTrue()
    {
        // Arrange
        var sut = new Mech("Test", "TST-1A", 50, 6, CreateBasicPartsData());

        // Destroy both legs
        var leftLeg = sut.Parts[PartLocation.LeftLeg];
        leftLeg.ApplyDamage(100, HitDirection.Front);
        var rightLeg = sut.Parts[PartLocation.RightLeg];
        rightLeg.ApplyDamage(100, HitDirection.Front);

        // Destroy both arms
        var leftArm = sut.Parts[PartLocation.LeftArm];
        leftArm.ApplyDamage(100, HitDirection.Front);
        var rightArm = sut.Parts[PartLocation.RightArm];
        rightArm.ApplyDamage(100, HitDirection.Front);

        // Act & Assert
        sut.IsImmobile.ShouldBeTrue("A mech with both legs and both arms destroyed should be immobile");
    }

    [Fact]
    public void IsImmobile_WhenBothLegsButOnlyOneArmDestroyed_ShouldReturnFalse()
    {
        // Arrange
        var sut = new Mech("Test", "TST-1A", 50, 6, CreateBasicPartsData());
        var pilot = Substitute.For<IPilot>();
        pilot.IsConscious.Returns(true);
        sut.AssignPilot(pilot);

        // Destroy both legs
        var leftLeg = sut.Parts[PartLocation.LeftLeg];
        leftLeg.ApplyDamage(20, HitDirection.Front);
        var rightLeg = sut.Parts[PartLocation.RightLeg];
        rightLeg.ApplyDamage(20, HitDirection.Front);

        // Destroy one arm
        var leftArm = sut.Parts[PartLocation.LeftArm];
        leftArm.ApplyDamage(20, HitDirection.Front);

        // Act & Assert
        sut.IsImmobile.ShouldBeFalse("A mech with both legs but only one arm destroyed should not be immobile");
    }

    [Fact]
    public void IsImmobile_WhenOnlyLegsDestroyed_ShouldReturnFalse()
    {
        // Arrange
        var sut = new Mech("Test", "TST-1A", 50, 6, CreateBasicPartsData());
        var pilot = Substitute.For<IPilot>();
        pilot.IsConscious.Returns(true);
        sut.AssignPilot(pilot);

        // Destroy both legs
        var leftLeg = sut.Parts[PartLocation.LeftLeg];
        leftLeg.ApplyDamage(20, HitDirection.Front);
        var rightLeg = sut.Parts[PartLocation.RightLeg];
        rightLeg.ApplyDamage(20, HitDirection.Front);

        // Act & Assert
        sut.IsImmobile.ShouldBeFalse("A mech with only legs destroyed should not be immobile");
    }

    [Fact]
    public void IsImmobile_WhenOnlyArmsDestroyed_ShouldReturnFalse()
    {
        // Arrange
        var sut = new Mech("Test", "TST-1A", 50, 6, CreateBasicPartsData());
        var pilot = Substitute.For<IPilot>();
        pilot.IsConscious.Returns(true);
        sut.AssignPilot(pilot);

        // Destroy both arms
        var leftArm = sut.Parts[PartLocation.LeftArm];
        leftArm.ApplyDamage(20, HitDirection.Front);
        var rightArm = sut.Parts[PartLocation.RightArm];
        rightArm.ApplyDamage(20, HitDirection.Front);

        // Act & Assert
        sut.IsImmobile.ShouldBeFalse("A mech with only arms destroyed should not be immobile");
    }

    [Fact]
    public void IsImmobile_WithMixOfBlownOffAndDestroyedParts_ShouldReturnTrue()
    {
        // Arrange
        var sut = new Mech("Test", "TST-1A", 50, 6, CreateBasicPartsData());

        // Destroy one leg, blow off another
        var leftLeg = sut.Parts[PartLocation.LeftLeg];
        leftLeg.ApplyDamage(100, HitDirection.Front);
        var rightLeg = sut.Parts[PartLocation.RightLeg];
        rightLeg.BlowOff();

        // Destroy one arm, blow off another
        var leftArm = sut.Parts[PartLocation.LeftArm];
        leftArm.ApplyDamage(100, HitDirection.Front);
        var rightArm = sut.Parts[PartLocation.RightArm];
        rightArm.BlowOff();

        // Act & Assert
        sut.IsImmobile.ShouldBeTrue("A mech with both legs and arms lost (mix of destroyed/blown off) should be immobile");
    }

    [Fact]
    public void MovementModifiers_ShouldIncludeDestroyedLeg()
    {
        // Arrange
        var sut = new Mech("Test", "TST-1A", 50, 6, CreateBasicPartsData());
        var leg = sut.Parts[PartLocation.LeftLeg];

        // Act
        leg.BlowOff();

        // Assert
        sut.MovementModifiers.ShouldContain(m => m is LegDestroyedPenalty);
    }

    [Fact]
    public void GetAttackModifiers_ReturnsNoModifiers_WhenArmIsDestroyed()
    {
        // Arrange
        var sut = new Mech("Test", "TST-1A", 50, 6, CreateBasicPartsData());
        var leftArm = sut.Parts[PartLocation.LeftArm];
        leftArm.BlowOff();

        // Act
        var modifiers = sut.GetAttackModifiers(PartLocation.LeftArm);

        // Assert
        modifiers.ShouldBeEmpty();
    }

    [Fact]
    public void GetAttackModifiers_ReturnsOnlyShoulderModifier_WhenShoulderIsDestroyed()
    {
        // Arrange
        var sut = new Mech("Test", "TST-1A", 50, 6, CreateBasicPartsData());
        var leftArm = sut.Parts[PartLocation.LeftArm];
        var shoulderActuator = leftArm.GetComponent<ShoulderActuator>();
        shoulderActuator!.Hit();
    
        // Act
        var modifiers = sut.GetAttackModifiers(PartLocation.LeftArm);
    
        // Assert
        modifiers.ShouldHaveSingleItem().ShouldBeOfType<ShoulderActuatorHitModifier>();
        var modifier = (ShoulderActuatorHitModifier)modifiers[0];
        modifier.ArmLocation.ShouldBe(PartLocation.LeftArm);
        modifier.Value.ShouldBe(4);
    }
    
    [Fact]
    public void GetAttackModifiers_ReturnsBothUpperAndLowerArmModifiers_WhenActuatorsAreDestroyed()
    {
        // Arrange
        var sut = new Mech("Test", "TST-1A", 50, 6, CreateBasicPartsData());
        var leftArm = sut.Parts[PartLocation.LeftArm];
        var upperArmActuator = new UpperArmActuator();
        leftArm.TryAddComponent(upperArmActuator);
        var lowerArmActuator = new LowerArmActuator();
        leftArm.TryAddComponent(lowerArmActuator);
        upperArmActuator.Hit();
        lowerArmActuator.Hit();
    
        // Act
        var modifiers = sut.GetAttackModifiers(PartLocation.LeftArm);
    
        // Assert
        modifiers.Count.ShouldBe(2);
    
        var upperArmModifier = modifiers.OfType<UpperArmActuatorHitModifier>().Single();
        upperArmModifier.ArmLocation.ShouldBe(PartLocation.LeftArm);
        upperArmModifier.Value.ShouldBe(1);
    
        var lowerArmModifier = modifiers.OfType<LowerArmActuatorHitModifier>().Single();
        lowerArmModifier.ArmLocation.ShouldBe(PartLocation.LeftArm);
        lowerArmModifier.Value.ShouldBe(1);
    }
    
    [Fact]
    public void GetAttackModifiers_ReturnsNoModifiers_ForNonArmLocation()
    {
        // Act
        var sut = new Mech("Test", "TST-1A", 50, 6, CreateBasicPartsData());
        var modifiers = sut.GetAttackModifiers(PartLocation.CenterTorso);
    
        // Assert
        modifiers.ShouldBeEmpty();
    }
    
    [Fact]
    public void GetAttackModifiers_ReturnsShoulderModifier_WhenAllActuatorsAreDestroyed()
    {
        // Arrange - destroy all actuators, but shoulder should take precedence
        var sut = new Mech("Test", "TST-1A", 50, 6, CreateBasicPartsData());
        var leftArm = sut.Parts[PartLocation.LeftArm];
        var upperArmActuator = new UpperArmActuator();
        leftArm.TryAddComponent(upperArmActuator);
        var lowerArmActuator = new LowerArmActuator();
        leftArm.TryAddComponent(lowerArmActuator);
        var shoulderActuator = leftArm.GetComponent<ShoulderActuator>();
        shoulderActuator!.Hit();
        upperArmActuator.Hit();
        lowerArmActuator.Hit();
    
        // Act
        var modifiers = sut.GetAttackModifiers(PartLocation.LeftArm);
    
        // Assert - Should only return shoulder modifier
        var modifier = modifiers.ShouldHaveSingleItem().ShouldBeOfType<ShoulderActuatorHitModifier>();
        modifier.ArmLocation.ShouldBe(PartLocation.LeftArm);
        modifier.Value.ShouldBe(4);
    }
    
    [Fact]
    public void GetAttackModifiers_WithProneMech_ShouldIncludeProneModifier()
    {
        // Arrange
        var sut = new Mech("Test", "TST-1A", 50, 6, CreateBasicPartsData());
        
        // Make the attacker prone
        sut.SetProne();

        // Act
        var result = sut.GetAttackModifiers(PartLocation.CenterTorso);

        // Assert
        var proneModifier = result.OfType<ProneAttackerModifier>().ShouldHaveSingleItem();
        proneModifier.Value.ShouldBe(2);
    }

    [Fact]
    public void GetAttackModifiers_WithNonProneMech_ShouldNotIncludeProneModifier()
    {
        // Arrange
        var sut = new Mech("Test", "TST-1A", 50, 6, CreateBasicPartsData());
        // Act
        var result = sut.GetAttackModifiers(PartLocation.CenterTorso);

        // Assert
        result.OfType<ProneAttackerModifier>().ShouldBeEmpty();
    }

    
    [Fact]
    public void IsMinimumMovement_ShouldReturnFalse_ByDefault()
    {
        // Arrange
        var sut = new Mech("Test", "TST-1A", 50, 6, CreateBasicPartsData());
        
        // Act
        var result = sut.IsMinimumMovement;
        
        // Assert
        result.ShouldBeFalse();
    }
    
    [Fact]
    public void IsMinimumMovement_ShouldReturnTrue_WhenProneAndOneMovementPointAndNoStandupAttempts()
    {
        // Arrange
        var sut = new Mech("Test", "TST-1A", 50, 1, CreateBasicPartsData());
        sut.SetProne();
        
        // Act
        var result = sut.IsMinimumMovement;
        
        // Assert
        result.ShouldBeTrue();
    }
    
    [Fact]
    public void IsMinimumMovement_ShouldReturnTrue_WhenProneAndOneLegIsDestroyed()
    {
        // Arrange
        var sut = new Mech("Test", "TST-1A", 50, 5, CreateBasicPartsData());
        sut.SetProne();
        var leg = sut.Parts[PartLocation.LeftLeg];
        leg.ApplyDamage(100, HitDirection.Front);
        
        // Act
        var result = sut.IsMinimumMovement;
        
        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void CalculateCriticalHitsData_ShouldReturnNull_WhenPartHasNoStructure()
    {
        // Arrange - This tests lines 579-580 (null check for part with no structure)
        var mech = new Mech("Test", "TST-1A", 50, 4, CreateBasicPartsData());
        var leftArm = mech.Parts[PartLocation.LeftArm];

        // Destroy the left arm by reducing structure to 0
        leftArm.ApplyDamage(leftArm.CurrentArmor + leftArm.CurrentStructure, HitDirection.Front);
        
        // Act
        var result = mech.CalculateCriticalHitsData(PartLocation.LeftArm,
            _diceRoller,
            _damageTransferCalculator);

        // Assert
        result.ShouldBeNull(); // Should return null for the destroyed part
        _diceRoller.DidNotReceive().Roll2D6(); // Should not roll dice for the destroyed part
    }

    [Fact]
    public void CalculateCriticalHitsData_ShouldReturnNull_WhenPartNotFound()
    {
        // Arrange - This tests lines 579-580 (null check when part is not found)
        var mech = new Mech("Test", "TST-1A", 50, 4, CreateBasicPartsData());

        // Act
        var result = mech.CalculateCriticalHitsData((PartLocation)999,
            _diceRoller,
            _damageTransferCalculator); // Invalid location

        // Assert
        result.ShouldBeNull(); // Should return null for a non-existent part
        _diceRoller.DidNotReceive().Roll2D6(); // Should not roll dice for a non-existent part
    }

    [Fact]
    public void CalculateCriticalHitsData_ShouldProceed_WhenPartHasStructure()
    {
        // Arrange 
        var mech = new Mech("Test", "TST-1A", 50, 4, CreateBasicPartsData());

        // Setup dice roller to return valid critical hit roll
        _diceRoller.Roll2D6().Returns([new DiceResult(4), new DiceResult(4)]); // Roll of 8
        _diceRoller.RollD6().Returns(new DiceResult(3)); // Slot roll

        // Act
        var result = mech.CalculateCriticalHitsData(PartLocation.CenterTorso,
            _diceRoller,
            _damageTransferCalculator);

        // Assert
        result.ShouldNotBeNull(); // Should return valid data for part with structure
        _diceRoller.Received(1).Roll2D6(); // Should roll dice for a valid part
    }

    [Fact]
    public void CalculateCriticalHitsData_ShouldReturnNull_WhenPartStructureIsZero()
    {
        // Arrange - This specifically tests the CurrentStructure > 0 condition in lines 579-580
        var mech = new Mech("Test", "TST-1A", 50, 4, CreateBasicPartsData());
        var centerTorso = mech.Parts[PartLocation.CenterTorso];

        // Destroy the center torso by applying enough damage to reduce structure to 0
        centerTorso.ApplyDamage(centerTorso.CurrentArmor + centerTorso.CurrentStructure, HitDirection.Front);
        
        // Act
        var result = mech.CalculateCriticalHitsData(PartLocation.CenterTorso,
            _diceRoller,
            _damageTransferCalculator);

        // Assert
        result.ShouldBeNull(); // Should return null when the structure is exactly 0
        _diceRoller.DidNotReceive().Roll2D6(); // Should not roll dice when structure is 0
    }
}
