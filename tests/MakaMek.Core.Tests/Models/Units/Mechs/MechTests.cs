using NSubstitute;
using Sanet.MakaMek.Core.Data.Community;
using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Models.Game.Dice;
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
using Sanet.MakaMek.Core.Utils.TechRules;

namespace Sanet.MakaMek.Core.Tests.Models.Units.Mechs;

public class MechTests
{
    private static List<UnitPart> CreateBasicPartsData()
    {
        var centerTorso = new CenterTorso("CenterTorso", 31, 10, 6);
        centerTorso.TryAddComponent(new Engine(250));
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
        mech.Parts.ShouldContain(p => p.Location == PartLocation.Head);
        mech.Parts.ShouldContain(p => p.Location == PartLocation.CenterTorso);
        mech.Parts.ShouldContain(p => p.Location == PartLocation.LeftTorso);
        mech.Parts.ShouldContain(p => p.Location == PartLocation.RightTorso);
        mech.Parts.ShouldContain(p => p.Location == PartLocation.LeftArm);
        mech.Parts.ShouldContain(p => p.Location == PartLocation.RightArm);
        mech.Parts.ShouldContain(p => p.Location == PartLocation.LeftLeg);
        mech.Parts.ShouldContain(p => p.Location == PartLocation.RightLeg);
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
    public void MoveTo_ShouldUpdatePosition()
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
    public void MoveTo_ShouldThrowException_WhenNotDeployed()
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
        dissipation.ShouldBe(12, "2 heat sinks + 10 engine HS");
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
        var mech = new Mech("Test", "TST-1A", 50, 4, CreateBasicPartsData());

        // Assert
        mech.Status.ShouldBe(UnitStatus.Active);
    }

    [Fact]
    public void Shutdown_ChangesStatusToShutdown()
    {
        // Arrange
        var mech = new Mech("Test", "TST-1A", 50, 4, CreateBasicPartsData());

        // Act
        mech.Shutdown();

        // Assert
        mech.Status.ShouldBe(UnitStatus.Shutdown);
    }

    [Fact]
    public void Startup_ChangesStatusToActive()
    {
        // Arrange
        var mech = new Mech("Test", "TST-1A", 50, 4, CreateBasicPartsData());
        mech.Shutdown();

        // Act
        mech.Startup();

        // Assert
        mech.Status.ShouldBe(UnitStatus.Active);
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
        mech.SetProne();

        // Act
        mech.StandUp();

        // Assert
        (mech.Status & UnitStatus.Prone).ShouldNotBe(UnitStatus.Prone);
        mech.IsProne.ShouldBeFalse();
    }
    
        [Theory]
    [InlineData(0, HexDirection.Top, HexDirection.TopRight, false)] // No rotation allowed
    [InlineData(1, HexDirection.Top, HexDirection.TopRight, true)] // 60 degrees allowed, within limit
    [InlineData(1, HexDirection.Top, HexDirection.Bottom, false)] // 60 degrees allowed, beyond limit
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
    public void Constructor_AssignsDefaultMechwarrior()
    {
        // Arrange & Act
        var mech = new Mech("Test", "TST-1A", 50, 4, CreateBasicPartsData());

        // Assert
        mech.Crew.ShouldNotBeNull();
        mech.Crew.ShouldBeOfType<MechWarrior>();
        var pilot = (MechWarrior)mech.Crew;
        pilot.FirstName.ShouldBe("MechWarrior");
        pilot.LastName.Length.ShouldBe(6); // Random GUID substring
        pilot.Gunnery.ShouldBe(MechWarrior.DefaultGunnery);
        pilot.Piloting.ShouldBe(MechWarrior.DefaultPiloting);
    }

    [Fact]
    public void ResetTurnState_ShouldResetTorsoRotation()
    {
        // Arrange
        var parts = CreateBasicPartsData();
        var torsos = parts.OfType<Torso>().ToList();
        var mech = new Mech("Test", "TST-1A", 50, 4, parts);
        mech.Deploy(new HexPosition(new HexCoordinates(1, 1), HexDirection.BottomRight));

        // Rotate torsos to a different direction
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
        // Attach weapon to a part (e.g., right arm)
        var rightArm = sut.Parts.First(p => p.Location == PartLocation.RightArm);
        rightArm.TryAddComponent(weapon);
        // Set a dummy target
        var dummyTarget = new Mech("Dummy", "DMY-1A", 50, 4, CreateBasicPartsData());
        weapon.Target = dummyTarget;
        weapon.Target.ShouldNotBeNull();

        // Act
        sut.ResetTurnState();

        // Assert
        sut.HasDeclaredWeaponAttack.ShouldBeFalse();
        weapon.Target.ShouldBeNull();
    }


    [Theory]
    [InlineData(5, 8, 2)] // Standard mech without jump jets
    [InlineData(4, 6, 0)] // Fast mech with jump jets
    [InlineData(3, 5, 2)] // Slow mech with lots of jump jets
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
            part.TryAddComponent(new TestComponent([i]));

        var diceRoller = Substitute.For<IDiceRoller>();
        diceRoller.Roll2D6().Returns(
            [new DiceResult(5), new DiceResult(5)] // 10 total for crit roll
        );
        diceRoller.RollD6().Returns(new DiceResult(2), new DiceResult(3), new DiceResult(4), new DiceResult(5));

        // Act
        var critsData = mech.CalculateCriticalHitsData(PartLocation.RightArm, diceRoller);

        // Assert
        critsData.ShouldNotBeNull();
        critsData.HitComponents.ShouldNotBeNull();
        critsData.HitComponents.Length.ShouldBe(2);
        critsData.HitComponents.FirstOrDefault(c => c.Slot == 2).ShouldNotBeNull();
        critsData.Roll.ShouldBe(10);
        critsData.NumCriticalHits.ShouldBe(2);
    }

    private class TestComponent(int[] slots) : Component("Test", slots)
    {
        public override MakaMekComponent ComponentType => MakaMekComponent.MachineGun;
    }

    [Fact]
    public void CalculateCriticalHitsData_ShouldReturnOnlyAvailableInSecondGroup()
    {
        // Arrange
        var part = new Arm("TestArm", PartLocation.RightArm, 0, 10);
        var mech = new Mech("TestChassis", "TestModel", 50, 5, [part]);
        for (var i = 1; i < 7; i++)
            part.TryAddComponent(new TestComponent([i]));

        var diceRoller = Substitute.For<IDiceRoller>();
        diceRoller.Roll2D6().Returns(
            [new DiceResult(5), new DiceResult(5)] // 10 total for crit roll
        );
        diceRoller.RollD6().Returns(
            new DiceResult(4),
            new DiceResult(3),
            new DiceResult(4),
            new DiceResult(5));

        // Act
        var critsData = mech.CalculateCriticalHitsData(PartLocation.RightArm, diceRoller);

        // Assert
        critsData.ShouldNotBeNull();
        critsData.HitComponents.ShouldNotBeNull();
        critsData.HitComponents.Length.ShouldBe(2);
        critsData.HitComponents.FirstOrDefault(c=>c.Slot==6).ShouldNotBeNull();
    }

    [Fact]
    public void CalculateCriticalHitsData_ShouldSetCriticalHits_InSmallPart_WhenDamageExceedsArmorAndCritsRolled()
    {
        // Arrange
        var part = new Leg("TestLeg", PartLocation.RightLeg, 0, 10);
        var mech = new Mech("TestChassis", "TestModel", 50, 5, [part]);

        var diceRoller = Substitute.For<IDiceRoller>();
        diceRoller.Roll2D6().Returns(
            [new DiceResult(5), new DiceResult(5)] // 10 total for crit roll
        );
        diceRoller.RollD6().Returns(new DiceResult(2), new DiceResult(3), new DiceResult(4), new DiceResult(5));

        // Act
        var critsData = mech.CalculateCriticalHitsData(PartLocation.RightLeg, diceRoller);

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
            part.TryAddComponent(new TestComponent([i]));

        var diceRoller = Substitute.For<IDiceRoller>();
        diceRoller.Roll2D6().Returns(
            [new(3), new(3)] // 6 total for crit roll (no crits)
        );

        // Act
        var critsData = mech.CalculateCriticalHitsData(PartLocation.RightArm, diceRoller);

        // Assert
        critsData.ShouldNotBeNull();
        critsData.Roll.ShouldBe(6);
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
            if (component.MountedAtSlots[0] != 0)
                component.Hit(); // destroy all components but first
        }

        var mech = new Mech("TestChassis", "TestModel", 50, 5, [part]);

        var diceRoller = Substitute.For<IDiceRoller>();
        diceRoller.Roll2D6().Returns(
            [new(4), new(5)] // 9 total for crit roll
        );
        diceRoller.RollD6().Returns(new DiceResult(1));

        // Act
        var critsData = mech.CalculateCriticalHitsData(location, diceRoller);

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

        var diceRoller = Substitute.For<IDiceRoller>();
        diceRoller.Roll2D6().Returns(
            [new(4), new(5)] // 9 total for crit roll
        );

        // Act
        var critsData = mech.CalculateCriticalHitsData(PartLocation.RightArm, diceRoller);

        // Assert
        critsData?.HitComponents.ShouldBeNull();
    }

    [Fact]
    public void CalculateCriticalHitsData_ShouldSetIsBlownOff_WhenCriticalRollIs12AndLocationCanBeBlownOff()
    {
        // Arrange
        var part = new Arm("TestArm", PartLocation.RightArm, 0, 10);
        var mech = new Mech("TestChassis", "TestModel", 50, 5, [part]);

        var diceRoller = Substitute.For<IDiceRoller>();
        diceRoller.Roll2D6().Returns(
            [new(6), new(6)] // 12 total for crit roll
        );

        // Act
        var critsData = mech.CalculateCriticalHitsData(PartLocation.RightArm, diceRoller);

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

        var diceRoller = Substitute.For<IDiceRoller>();
        diceRoller.Roll2D6().Returns(
            [new DiceResult(6), new DiceResult(6)] // 12 total for crit roll
        );
        diceRoller.RollD6().Returns(
            new DiceResult(2),
            new DiceResult(3),
            new DiceResult(2),
            new DiceResult(4),
            new DiceResult(2),
            new DiceResult(5));

        // Act
        var critsData = mech.CalculateCriticalHitsData(PartLocation.CenterTorso, diceRoller);

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
        var centerTorso = mech.Parts.First(p => p.Location == PartLocation.CenterTorso);
        var diceRoller = Substitute.For<IDiceRoller>();

        // Gyro is in slots 3-6 of center torso
        // First, hit slot 3
        centerTorso.CriticalHit(3);

        // Setup dice roller to return values that would hit slot 1
        diceRoller.Roll2D6().Returns(
            [new DiceResult(5), new DiceResult(4)] // Total 9 for crit roll (1 crit)
        );
        diceRoller.RollD6()
            .Returns(new DiceResult(4)); //that should go to the second group where only one slot is available (6) 

        // Act
        var critData = mech.CalculateCriticalHitsData(PartLocation.CenterTorso, diceRoller);

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
            WeaponHeatSources = [new WeaponHeatData
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
        sut.MovementHeatPenalty.ShouldBe(baseMovement-expectedWalkMp);

        // Jumping MP should not be affected by heat
        var jumpJets = new JumpJets();
        var leftLeg = sut.Parts.First(p => p.Location == PartLocation.LeftLeg);
        leftLeg.TryAddComponent(jumpJets);

        var originalJumpMp = jumpJets.JumpMp;
        sut.GetMovementPoints(MovementType.Jump).ShouldBe(originalJumpMp, "Jump MP should not be affected by heat");

        // Check shutdown status for high heat
        if (heat >= 30)
        {
            sut.Status.ShouldBe(UnitStatus.Shutdown, "Mech should shutdown at 30+ heat");
        }
    }

    [Fact]
    public void CanJump_WhenMechIsProne_ShouldReturnFalse()
    {
        // Arrange
        var parts = CreateBasicPartsData();
        var leftLeg = parts.Single(p => p.Location == PartLocation.LeftLeg);
        leftLeg.TryAddComponent(new JumpJets(2));

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
        leftLeg.TryAddComponent(new JumpJets(2));

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
        var jumpJets = new JumpJets(2);
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
        leftLeg.TryAddComponent(new JumpJets(2));

        var sut = new Mech("Test", "TST-1A", 50, 5, parts);

        // Act & Assert
        sut.CanJump.ShouldBeTrue("Mechs with functional jump jets that are not prone and haven't stood up should be able to jump");
    }

    [Fact]
    public void CanJump_AfterResetTurnState_ShouldReturnTrue()
    {
        // Arrange
        var parts = CreateBasicPartsData();
        var leftLeg = parts.Single(p => p.Location == PartLocation.LeftLeg);
        leftLeg.TryAddComponent(new JumpJets(2));

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
    public void CanRun_WhenMechIsProne_ShouldReturnFalse()
    {
        // Arrange
        var sut = new Mech("Test", "TST-1A", 50, 5,CreateBasicPartsData());
        sut.SetProne();

        // Act & Assert
        sut.CanRun.ShouldBeFalse();
    }
    
    [Fact]
    public void CanRun_ShouldReturnFalse_WhenLegIsBlownOff()
    {
        // Arrange
        var sut = new Mech("Test", "TST-1A", 50, 0, CreateBasicPartsData());
        var leg = sut.Parts.First(p => p.Location == PartLocation.LeftLeg);
        leg.BlowOff();
        
        // Act & Assert
        sut.CanRun.ShouldBeFalse();
    }
    
    [Fact]
    public void CanRun_ShouldReturnFalse_WhenLegIsDestroyed()
    {
        // Arrange
        var sut = new Mech("Test", "TST-1A", 50, 0, CreateBasicPartsData());
        var leg = sut.Parts.First(p => p.Location == PartLocation.LeftLeg);
        leg.ApplyDamage(100);
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
        gyro.Hit(); // Damage the gyro (1 hit)
        
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
    public void EngineHeatSinks_ShouldBeTen()
    {
        var sut = new Mech("Test", "TST-1A", 50, 4, CreateBasicPartsData());
        
        sut.EngineHeatSinks.ShouldBe(10);
    }

    [Fact]
    public void HeatDissipation_ShouldReduceHeatAndRestoreMovement()
    {
        // Arrange
        var sut = new Mech("Test", "TST-1A", 50, 5, CreateBasicPartsData());

        // Set initial heat to 15 (3 MP penalty)
        sut.ApplyHeat(new HeatData
        {
            MovementHeatSources = [],
            WeaponHeatSources = [new WeaponHeatData
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
    [InlineData(0, 0)]  // No heat, no penalty
    [InlineData(7, 0)]  // Below first threshold, no penalty
    [InlineData(8, 1)]  // At first threshold, +1 penalty
    [InlineData(12, 1)] // Between first and second threshold, +1 penalty
    [InlineData(13, 2)] // At second threshold, +2 penalty
    [InlineData(16, 2)] // Between second and third threshold, +2 penalty
    [InlineData(17, 3)] // At third threshold, +3 penalty
    [InlineData(23, 3)] // Between third and fourth threshold, +3 penalty
    [InlineData(24, 4)] // At fourth threshold, +4 penalty
    [InlineData(30, 4)] // Above fourth threshold, +4 penalty
    public void AttackHeatPenalty_ShouldReturnCorrectPenalty(int heat, int expectedPenalty)
    {
        // Arrange
        var sut = new Mech("Test", "TST-1A", 50, 5, CreateBasicPartsData());
        
        // Act
        // Set heat and apply effects
        var heatData = new HeatData
        {
            MovementHeatSources = [],
            WeaponHeatSources = [new WeaponHeatData
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
        sut.AttackHeatPenalty.ShouldBe(expectedPenalty, $"Heat level {heat} should result in attack penalty of {expectedPenalty}");
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
            WeaponHeatSources = [new WeaponHeatData
                {
                    WeaponName = "TestWeapon",
                    HeatPoints = 17
                }
            ],
            DissipationData = default
        });

        // Verify initial state
        sut.AttackHeatPenalty.ShouldBe(3, "Initial attack penalty should be +3");

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
        sut.AttackHeatPenalty.ShouldBe(0, "Attack penalty should be removed");
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
        engineHeatPenalty.ShouldBe(5, "Engine with one hit should have +5 heat penalty");
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
        heatData.EngineHeatSource.Value.Hits.ShouldBe(1);
        heatData.EngineHeatSource.Value.HeatPoints.ShouldBe(5);
    }

    [Fact]
    public void CanStandup_WhenHasMovementPointsAndPilotConscious_ShouldReturnTrue()
    {
        // Arrange
        var parts = CreateBasicPartsData();
        var mech = new Mech("Test", "TST-1A", 50, 4, parts);
        mech.SetProne();
        
        // Mock pilot - ensure it's conscious
        var pilot = Substitute.For<IPilot>();
        pilot.IsUnconscious.Returns(false);
        typeof(Mech).GetProperty("Crew")?.SetValue(mech, pilot);

        // Act
        var canStandup = mech.CanStandup();

        // Assert
        canStandup.ShouldBeTrue("Mech should be able to stand up when it has movement points and pilot is conscious");
    }

    [Theory]
    [InlineData(0, false)]  // No movement points, unconscious pilot
    [InlineData(0, true)]   // No movement points, conscious pilot 
    [InlineData(4, true)]   // Has movement points, unconscious pilot
    public void CanStandup_WhenMissingRequirements_ShouldReturnFalse(int walkMp, bool pilotUnconscious)
    {
        // Arrange
        var parts = CreateBasicPartsData();
        var mech = new Mech("Test", "TST-1A", 50, walkMp, parts);
        mech.SetProne();
        
        // Mock pilot with specified consciousness state
        var pilot = Substitute.For<IPilot>();
        pilot.IsUnconscious.Returns(pilotUnconscious);
        typeof(Mech).GetProperty("Crew")?.SetValue(mech, pilot);

        // Act
        var canStandup = mech.CanStandup();

        // Assert
        canStandup.ShouldBe(false, $"Mech should not stand up with walkMP={walkMp} and pilotUnconscious={pilotUnconscious}");
    }

    [Fact]
    public void CanStandup_WhenHasMovementPointsAndPilotConsciousAndNotShutdown_ShouldReturnTrue()
    {
        // Arrange
        var parts = CreateBasicPartsData();
        var mech = new Mech("Test", "TST-1A", 50, 4, parts);
        mech.SetProne();
        
        // Mock pilot - ensure it's conscious
        var pilot = Substitute.For<IPilot>();
        pilot.IsUnconscious.Returns(false);
        typeof(Mech).GetProperty("Crew")?.SetValue(mech, pilot);

        // Act
        var canStandup = mech.CanStandup();

        // Assert
        canStandup.ShouldBeTrue("Mech should be able to stand up when it has movement points and pilot is conscious and not shutdown");
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
    public void CanStandup_WhenHasMovementPointsAndPilotConsciousAndShutdown_ShouldReturnFalse()
    {
        // Arrange
        var parts = CreateBasicPartsData();
        var mech = new Mech("Test", "TST-1A", 50, 4, parts);
        mech.SetProne();
        mech.Shutdown();
        
        // Mock pilot - ensure it's conscious
        var pilot = Substitute.For<IPilot>();
        pilot.IsUnconscious.Returns(false);
        typeof(Mech).GetProperty("Crew")?.SetValue(mech, pilot);

        // Act
        var canStandup = mech.CanStandup();

        // Assert
        canStandup.ShouldBeFalse("Mech should not be able to stand up when it has movement points and pilot is conscious but shutdown");
    }
    
    [Fact]
    public void CanStandup_WhenBothLegsDestroyed_ShouldReturnFalse()
    {
        // Arrange
        var parts = CreateBasicPartsData();
        var mech = new Mech("Test", "TST-1A", 50, 4, parts);
        mech.SetProne();
        
        var leftLeg = mech.Parts.First(p=> p.Location == PartLocation.LeftLeg);
        leftLeg.ApplyDamage(100);
        leftLeg.IsDestroyed.ShouldBeTrue();
        var rightLeg = mech.Parts.First(p=> p.Location == PartLocation.RightLeg);
        rightLeg.ApplyDamage(100);
        rightLeg.IsDestroyed.ShouldBeTrue();
        
        // Act
        var canStandup = mech.CanStandup();

        // Assert
        canStandup.ShouldBeFalse("Mech should not be able to stand up when both legs are destroyed");
    }
    
    [Fact]
    public void CanStandup_WhenBothLegsBlownOff_ShouldReturnFalse()
    {
        // Arrange
        var parts = CreateBasicPartsData();
        var mech = new Mech("Test", "TST-1A", 50, 4, parts);
        mech.SetProne();
        
        var leftLeg = mech.Parts.First(p=> p.Location == PartLocation.LeftLeg);
        leftLeg.BlowOff();
        leftLeg.IsBlownOff.ShouldBeTrue();
        var rightLeg = mech.Parts.First(p=> p.Location == PartLocation.RightLeg);
        rightLeg.BlowOff();
        rightLeg.IsBlownOff.ShouldBeTrue();
        
        // Act
        var canStandup = mech.CanStandup();

        // Assert
        canStandup.ShouldBeFalse("Mech should not be able to stand up when both legs are blown off");
    }
    
    [Fact]
    public void CanStandup_WhenOneLegIsBlownOffAndAnotherIsDestroyed_ShouldReturnFalse()
    {
        // Arrange
        var parts = CreateBasicPartsData();
        var mech = new Mech("Test", "TST-1A", 50, 4, parts);
        mech.SetProne();
        
        var leftLeg = mech.Parts.First(p=> p.Location == PartLocation.LeftLeg);
        leftLeg.BlowOff();
        leftLeg.IsBlownOff.ShouldBeTrue();
        var rightLeg = mech.Parts.First(p=> p.Location == PartLocation.RightLeg);
        rightLeg.ApplyDamage(100);
        rightLeg.IsDestroyed.ShouldBeTrue();
        
        // Act
        var canStandup = mech.CanStandup();

        // Assert
        canStandup.ShouldBeFalse("Mech should not be able to stand up when both legs are not available");
    }

    [Fact]
    public void CanChangeFacingWhileProne_WhenMechIsNotProne_ShouldReturnFalse()
    {
        // Arrange
        var sut = new Mech("Test", "TST-1A", 50, 4, CreateBasicPartsData());

        // Act & Assert
        sut.CanChangeFacingWhileProne().ShouldBeFalse("Non-prone mechs should not be able to change facing while prone");
    }

    [Fact]
    public void CanChangeFacingWhileProne_WhenMechIsProneAndHasMovementPoints_ShouldReturnTrue()
    {
        // Arrange
        var sut = new Mech("Test", "TST-1A", 50, 4, CreateBasicPartsData());
        sut.SetProne();

        // Act & Assert
        sut.CanChangeFacingWhileProne().ShouldBeTrue("Prone mechs with movement points should be able to change facing");
    }

    [Fact]
    public void CanChangeFacingWhileProne_WhenMechIsProneButShutdown_ShouldReturnFalse()
    {
        // Arrange
        var sut = new Mech("Test", "TST-1A", 50, 4, CreateBasicPartsData());
        sut.SetProne();
        sut.Shutdown();

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
        sut.CanChangeFacingWhileProne().ShouldBeFalse("Mechs without movement points should not be able to change facing");
    }
    
    
    [Fact]
    public void CanFireWeapons_ShouldReturnTrue_WhenSensorsAreIntact()
    {
        // Arrange
        var sut = new Mech("Test", "TST-1A", 50, 0, CreateBasicPartsData());

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
        sut.GetMovementPoints(MovementType.Run).ShouldBe(5, "Running MP should be 1.5 times walking MP rounded up (3*1.5=4.5→5)");
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
        sut.GetMovementPoints(MovementType.Walk).ShouldBe(0, "Two destroyed hip actuators should reduce walking MP to 0");
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
        sut.GetMovementPoints(MovementType.Walk).ShouldBe(5, "Destroyed foot actuator should reduce walking MP by 1 (6-1=5)");
        sut.GetMovementPoints(MovementType.Run).ShouldBe(8, "Running MP should be 1.5 times walking MP rounded up (5*1.5=7.5→8)");
        footActuator.IsDestroyed.ShouldBeTrue();
    }

    [Fact]
    public void GetMovementPoints_WithDestroyedLowerLegActuator_ShouldReduceWalkingMPByOne()
    {
        // Arrange
        var sut = new Mech("Test", "TST-1A", 50, 6, CreateBasicPartsData());
        var lowerLegActuator = sut.GetAllComponents<LowerLegActuator>().First();

        // Act
        lowerLegActuator.Hit(); // Destroy lower leg actuator

        // Assert
        sut.GetMovementPoints(MovementType.Walk).ShouldBe(5, "Destroyed lower leg actuator should reduce walking MP by 1 (6-1=5)");
        sut.GetMovementPoints(MovementType.Run).ShouldBe(8, "Running MP should be 1.5 times walking MP rounded up (5*1.5=7.5→8)");
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
        sut.GetMovementPoints(MovementType.Walk).ShouldBe(5, "Destroyed upper leg actuator should reduce walking MP by 1 (6-1=5)");
        sut.GetMovementPoints(MovementType.Run).ShouldBe(8, "Running MP should be 1.5 times walking MP rounded up (5*1.5=7.5→8)");
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
        sut.GetMovementPoints(MovementType.Walk).ShouldBe(3, "Three destroyed actuators should reduce walking MP by 3 (6-3=3)");
        sut.GetMovementPoints(MovementType.Run).ShouldBe(5, "Running MP should be 1.5 times walking MP rounded up (3*1.5=4.5→5)");
    }

    [Fact]
    public void MovementPoints_WithBlownOffLeg_ShouldSetWalkingMPToOne()
    {
        // Arrange
        var sut = new Mech("Test", "TST-1A", 50, 6, CreateBasicPartsData());
        var leftLeg = sut.Parts.First(p => p.Location == PartLocation.LeftLeg);

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
    public void MovementPoints_ScenarioTest_HipFootAndHeatDamage(int baseMp, int afterHip, int afterFoot, int expectedWalk, int expectedRun)
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
            WeaponHeatSources = [new WeaponHeatData
            {
                WeaponName = "TestWeapon",
                HeatPoints = 6
            }],
            DissipationData = default
        };
        sut.ApplyHeat(heatData);

        // Final assertions
        sut.GetMovementPoints(MovementType.Walk).ShouldBe(expectedWalk, $"Final walking MP after all penalties: {expectedWalk}");
        sut.GetMovementPoints(MovementType.Run).ShouldBe(expectedRun, $"Final running MP: {expectedWalk} * 1.5 = {expectedRun}");
    }
}
