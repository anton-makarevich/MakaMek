using NSubstitute;
using Sanet.MakaMek.Core.Data.Community;
using Sanet.MakaMek.Core.Models.Game.Dice;
using Shouldly;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons.Energy;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Core.Models.Units.Pilots;

namespace Sanet.MakaMek.Core.Tests.Models.Units.Mechs;

public class MechTests
{
    private static List<UnitPart> CreateBasicPartsData()
    {
        return
        [
            new Head("Head", 9, 3),
            new CenterTorso("CenterTorso", 31, 10, 6),
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
    [InlineData(105, WeightClass.Unknown)]
    [InlineData(20, WeightClass.Light)]
    [InlineData(25, WeightClass.Light)]
    [InlineData(30, WeightClass.Light)]
    [InlineData(35, WeightClass.Light)]
    [InlineData(40, WeightClass.Medium)]
    [InlineData(45, WeightClass.Medium)]
    [InlineData(50, WeightClass.Medium)]
    [InlineData(55, WeightClass.Medium)]
    [InlineData(60, WeightClass.Heavy)]
    [InlineData(65, WeightClass.Heavy)]
    [InlineData(70, WeightClass.Heavy)]
    [InlineData(75, WeightClass.Heavy)]
    [InlineData(80, WeightClass.Assault)]
    [InlineData(85, WeightClass.Assault)]
    [InlineData(90, WeightClass.Assault)]
    [InlineData(95, WeightClass.Assault)]
    [InlineData(100, WeightClass.Assault)]
    public void WeightClass_Calculation_ReturnsCorrectClass(int tonnage, WeightClass expectedClass)
    {
        // Arrange
        var mech = new Mech("Test", "TST-1A", tonnage, 4, CreateBasicPartsData());

        // Act
        var weightClass = mech.Class;

        // Assert
        weightClass.ShouldBe(expectedClass);
    }

    [Fact]
    public void ApplyDamage_DestroysMech_WhenHeadOrCenterTorsoIsDestroyed()
    {
        // Arrange
        var mech = new Mech("Test", "TST-1A", 50, 4, CreateBasicPartsData());
        var headPart = mech.Parts.First(p => p.Location == PartLocation.Head);

        // Act
        mech.ApplyArmorAndStructureDamage(100, headPart);
        // Assert
        mech.Status.ShouldBe(UnitStatus.Destroyed);

        // Reset mech for next test
        mech = new Mech("Test", "TST-1A", 50, 4, CreateBasicPartsData());
        var centerTorsoPart = mech.Parts.First(p => p.Location == PartLocation.CenterTorso);

        // Act
        mech.ApplyArmorAndStructureDamage(100, centerTorsoPart);
        // Assert
        mech.Status.ShouldBe(UnitStatus.Destroyed);
    }

    [Fact]
    public void Deploy_SetsPosition_WhenNotDeployed()
    {
        // Arrange
        var mech = new Mech("Test", "TST-1A", 50, 4, CreateBasicPartsData());
        var coordinate = new HexCoordinates(1, 1);

        // Act
        mech.Deploy(new HexPosition(coordinate, HexDirection.Bottom));

        // Assert
        mech.Position?.Coordinates.ShouldBe(coordinate);
        mech.Position?.Facing.ShouldBe(HexDirection.Bottom);
        mech.IsDeployed.ShouldBeTrue();
    }

    [Fact]
    public void Deploy_ThrowsException_WhenAlreadyDeployed()
    {
        // Arrange
        var mech = new Mech("Test", "TST-1A", 50, 4, CreateBasicPartsData());
        var coordinate = new HexCoordinates(1, 1);
        mech.Deploy(new HexPosition(coordinate, HexDirection.Bottom));

        // Act & Assert
        var ex = Should.Throw<InvalidOperationException>(() =>
            mech.Deploy(new HexPosition(new HexCoordinates(2, 2), HexDirection.Bottom)));
        ex.Message.ShouldBe("Test TST-1A is already deployed.");
    }

    [Fact]
    public void MoveTo_ShouldNotUpdatePosition_WhenMovementTypeIsStandingStill()
    {
        // Arrange
        var mech = new Mech("Test", "TST-1A", 50, 4, CreateBasicPartsData());
        var position = new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom);
        mech.Deploy(position);

        // Act
        mech.Move(MovementType.StandingStill, []);

        // Assert
        mech.Position.ShouldBe(position); // Position should remain the same
        mech.HasMoved.ShouldBeTrue(); // Unit should be marked as moved
        mech.MovementTypeUsed.ShouldBe(MovementType.StandingStill);
        mech.DistanceCovered.ShouldBe(0); // Distance should be 0
        mech.MovementPointsSpent.ShouldBe(0); // No movement points spent
    }

    [Fact]
    public void DeclareWeaponAttack_ShouldThrowException_WhenNotDeployed()
    {
        // Arrange
        var mech = new Mech("Test", "TST-1A", 50, 4, CreateBasicPartsData());

        // Act
        var act = () => mech.DeclareWeaponAttack([], []);

        // Assert
        var ex = Should.Throw<InvalidOperationException>(act);
        ex.Message.ShouldBe("Unit is not deployed.");
    }

    [Fact]
    public void DeclareWeaponAttack_ShouldSetHasDeclaredWeaponAttack_WhenDeployed()
    {
        // Arrange
        var mech = new Mech("Test", "TST-1A", 50, 4, CreateBasicPartsData());
        var position = new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom);
        mech.Deploy(position);

        // Act
        mech.DeclareWeaponAttack([], []);

        // Assert
        mech.HasDeclaredWeaponAttack.ShouldBeTrue();
    }

    [Fact]
    public void HasDeclaredWeaponAttack_ShouldBeFalse_ByDefault()
    {
        // Arrange & Act
        var mech = new Mech("Test", "TST-1A", 50, 4, CreateBasicPartsData());

        // Assert
        mech.HasDeclaredWeaponAttack.ShouldBeFalse();
    }

    [Fact]
    public void Deploy_ShouldResetTorsoRotation()
    {
        // Arrange
        var parts = CreateBasicPartsData();
        var torsos = parts.OfType<Torso>().ToList();
        var mech = new Mech("Test", "TST-1A", 50, 4, parts);

        // Set initial torso rotation
        foreach (var torso in torsos)
        {
            torso.Rotate(HexDirection.Bottom);
        }

        // Act
        mech.Deploy(new HexPosition(new HexCoordinates(0, 0), HexDirection.TopRight));

        // Assert
        foreach (var torso in torsos)
        {
            torso.Facing.ShouldBe(HexDirection.TopRight,
                $"Torso {torso.Name} facing should be reset to match unit facing");
        }
    }

    [Theory]
    [InlineData(0, HexDirection.Top, HexDirection.TopRight, false)] // No rotation allowed
    [InlineData(1, HexDirection.Top, HexDirection.TopRight, true)]  // 60 degrees allowed, within limit
    [InlineData(1, HexDirection.Top, HexDirection.Bottom, false)]   // 60 degrees allowed, beyond limit
    [InlineData(2, HexDirection.Top, HexDirection.BottomRight, true)] // 120 degrees allowed, within limit
    [InlineData(3, HexDirection.Top, HexDirection.Bottom, true)]    // 180 degrees allowed, within limit
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
    [InlineData(0, false)]  // No rotation possible
    [InlineData(1, true)]   // Normal rotation
    [InlineData(2, true)]   // Extended rotation
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
            new List<DiceResult> { new(5), new(5) } // 10 total for crit roll
        );
        diceRoller.RollD6().Returns(new DiceResult(2), new DiceResult(3), new DiceResult(4), new DiceResult(5));

        // Act
        var critsData = mech.CalculateCriticalHitsData(PartLocation.RightArm, 5, diceRoller);

        // Assert
        critsData.ShouldNotBeNull();
        critsData.CriticalHits.ShouldNotBeNull();
        critsData.CriticalHits.Length.ShouldBe(2);
        critsData.CriticalHits.ShouldContain(2);
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
            new List<DiceResult> { new(5), new(5) } // 10 total for crit roll
        );
        diceRoller.RollD6().Returns(new DiceResult(4), new DiceResult(3), new DiceResult(4), new DiceResult(5));

        // Act
        var critsData = mech.CalculateCriticalHitsData(PartLocation.RightArm, 5, diceRoller);

        // Assert
        critsData.ShouldNotBeNull();
        critsData.CriticalHits.ShouldNotBeNull();
        critsData.CriticalHits.Length.ShouldBe(2);
        critsData.CriticalHits.ShouldContain(6);
    }

    [Fact]
    public void CalculateCriticalHitsData_ShouldSetCriticalHits_InSmallPart_WhenDamageExceedsArmorAndCritsRolled()
    {
        // Arrange
        var part = new Leg("TestLeg", PartLocation.RightLeg, 0, 10);
        var mech = new Mech("TestChassis", "TestModel", 50, 5, [part]);

        var diceRoller = Substitute.For<IDiceRoller>();
        diceRoller.Roll2D6().Returns(
            new List<DiceResult> { new(5), new(5) } // 10 total for crit roll
        );
        diceRoller.RollD6().Returns(new DiceResult(2), new DiceResult(3), new DiceResult(4), new DiceResult(5));

        // Act
        var critsData = mech.CalculateCriticalHitsData(PartLocation.RightLeg, 5, diceRoller);

        // Assert
        critsData.ShouldNotBeNull();
        critsData.CriticalHits.ShouldNotBeNull();
        critsData.CriticalHits.Length.ShouldBe(2);
        critsData.CriticalHits.ShouldContain(1);
        critsData.CriticalHits.ShouldContain(2);
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
            new List<DiceResult> { new(3), new(3) } // 6 total for crit roll (no crits)
        );

        // Act
        var critsData = mech.CalculateCriticalHitsData(PartLocation.RightArm, 5, diceRoller);

        // Assert
        critsData.ShouldNotBeNull();
        critsData.Roll.ShouldBe(6);
        critsData.NumCriticalHits.ShouldBe(0);
        critsData.CriticalHits.ShouldBeNull();
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
            new List<DiceResult> { new(4), new(5) } // 9 total for crit roll
        );

        // Act
        var critsData = mech.CalculateCriticalHitsData(location, 5, diceRoller);

        // Assert
        critsData.ShouldNotBeNull();
        critsData.CriticalHits.ShouldNotBeNull();
        critsData.CriticalHits.Length.ShouldBe(1);
        critsData.CriticalHits[0].ShouldBe(0);
    }

    [Fact]
    public void CalculateCriticalHitsData_ShouldReturnNull_WhenNoSlotsAvailable()
    {
        // Arrange
        var part = new Arm("TestArm", PartLocation.RightArm, 0, 10);
        part.Components[0].Hit(); // destroy shoulder
        var mech = new Mech("TestChassis", "TestModel", 50, 5, [part]);

        var diceRoller = Substitute.For<IDiceRoller>();
        diceRoller.Roll2D6().Returns(
            new List<DiceResult> { new(4), new(5) } // 9 total for crit roll
        );

        // Act
        var critsData = mech.CalculateCriticalHitsData(PartLocation.RightArm, 5, diceRoller);

        // Assert
        critsData?.CriticalHits.ShouldBeNull();
    }

    [Fact]
    public void CalculateCriticalHitsData_ShouldNotSetCriticalHits_WhenDamageDoesNotExceedArmor()
    {
        // Arrange
        var part = new Arm("TestArm", PartLocation.RightArm, 5, 10);
        var mech = new Mech("TestChassis", "TestModel", 50, 5, [part]);
        for (var i = 1; i < part.TotalSlots; i++)
            part.TryAddComponent(new TestComponent([i]));

        var diceRoller = Substitute.For<IDiceRoller>();

        // Act
        var critsData = mech.CalculateCriticalHitsData(PartLocation.RightArm, 3, diceRoller);

        // Assert
        critsData.ShouldBeNull();
        diceRoller.DidNotReceive().Roll2D6(); // Shouldn't even roll for crits
    }

    [Fact]
    public void CalculateCriticalHitsData_ShouldSetIsBlownOff_WhenCriticalRollIs12AndLocationCanBeBlownOff()
    {
        // Arrange
        var part = new Arm("TestArm", PartLocation.RightArm, 0, 10);
        var mech = new Mech("TestChassis", "TestModel", 50, 5, [part]);

        var diceRoller = Substitute.For<IDiceRoller>();
        diceRoller.Roll2D6().Returns(
            new List<DiceResult> { new(6), new(6) } // 12 total for crit roll
        );

        // Act
        var critsData = mech.CalculateCriticalHitsData(PartLocation.RightArm, 5, diceRoller);

        // Assert
        critsData.ShouldNotBeNull();
        critsData.IsBlownOff.ShouldBeTrue();
        critsData.NumCriticalHits.ShouldBe(0);
        critsData.CriticalHits.ShouldBeNull();
    }

    [Fact]
    public void CalculateCriticalHitsData_ShouldNotSetIsBlownOff_WhenCriticalRollIs12AndLocationCannotBeBlownOff()
    {
        // Arrange
        var part = new CenterTorso("TestTorso", 0, 10, 6);
        var mech = new Mech("TestChassis", "TestModel", 50, 5, [part]);

        var diceRoller = Substitute.For<IDiceRoller>();
        diceRoller.Roll2D6().Returns(
            new List<DiceResult> { new(6), new(6) } // 12 total for crit roll
        );
        diceRoller.RollD6().Returns(new DiceResult(2), new DiceResult(3), new DiceResult(4));

        // Act
        var critsData = mech.CalculateCriticalHitsData(PartLocation.CenterTorso, 5, diceRoller);

        // Assert
        critsData.ShouldNotBeNull();
        critsData.IsBlownOff.ShouldBeFalse();
        critsData.NumCriticalHits.ShouldBe(3);
        critsData.CriticalHits.ShouldNotBeNull();
    }

    [Fact]
    public void GetNumCriticalHits_ShouldReturnCorrectValues()
    {
        // Arrange
        var mech = new Mech("TestChassis", "TestModel", 50, 5, CreateBasicPartsData());

        // Act & Assert - Using reflection to access internal method
        var method = typeof(Mech).GetMethod("GetNumCriticalHits",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        // Test all possible roll values
        for (int roll = 2; roll <= 12; roll++)
        {
            int expected = roll switch
            {
                <= 7 => 0,
                8 or 9 => 1,
                10 or 11 => 2,
                12 => 3,
                _ => 0
            };

            int actual = (int)method.Invoke(mech, new object[] { roll });
            actual.ShouldBe(expected, $"Roll of {roll} should give {expected} critical hits");
        }
    }
}

