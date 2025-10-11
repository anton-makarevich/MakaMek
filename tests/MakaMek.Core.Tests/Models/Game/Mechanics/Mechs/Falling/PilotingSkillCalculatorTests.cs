using NSubstitute;
using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Mechs.Falling;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.PilotingSkill;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components.Internal;
using Sanet.MakaMek.Core.Models.Units.Components.Internal.Actuators;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Core.Models.Units.Pilots;
using Sanet.MakaMek.Core.Tests.Models.Units;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Mechanics.Mechs.Falling
{
    public class PilotingSkillCalculatorTests
    {
        private readonly IRulesProvider _mockRulesProvider;
        private readonly IDiceRoller _mockDiceRoller;
        private readonly IPilotingSkillCalculator _sut;

        public PilotingSkillCalculatorTests()
        {
            _mockRulesProvider = Substitute.For<IRulesProvider>();
            _mockDiceRoller = Substitute.For<IDiceRoller>();
            _sut = new PilotingSkillCalculator(_mockRulesProvider, _mockDiceRoller);
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
        public void GetPsrBreakdown_SpecificRollTypeRequested_OnlyCalculatesRequestedModifier()
        {
            // Arrange
            // Create a torso with a gyro
            var torso = new CenterTorso("Test Torso", 10, 3, 5);
            var gyro = torso.GetComponent<Gyro>()!;
            gyro.Hit(); // Apply 1 hit to the gyro
            
            // Create a mech with the torso
            var mech = new Mech("Test", "TST-1A", 50, [torso]);
            mech.AssignPilot(new MechWarrior("John", "Doe"));
            
            // Set up the rules provider to return a modifier value for gyro hits
            _mockRulesProvider.GetPilotingSkillRollModifier(PilotingSkillRollType.GyroHit).Returns(3);

            // Act
            var result = _sut.GetPsrBreakdown(mech, PilotingSkillRollType.GyroHit);

            // Assert
            result.BasePilotingSkill.ShouldBe(mech.Pilot!.Piloting);
            result.Modifiers.Count.ShouldBe(1); // Only gyro modifier (standard modifier applies because gyro is hit)
            result.Modifiers.ShouldContain(m => m is DamagedGyroModifier);
            var gyroModifier = (DamagedGyroModifier)result.Modifiers.First(m => m is DamagedGyroModifier);
            gyroModifier.Value.ShouldBe(3);
            gyroModifier.HitsCount.ShouldBe(1);
            result.ModifiedPilotingSkill.ShouldBe(mech.Pilot.Piloting + 3);
        }

        [Fact]
        public void GetPsrBreakdown_NoGyroHits_NoGyroModifierAdded()
        {
            // Arrange
            // Create a torso with a gyro that has no hits
            var torso = new CenterTorso("Test Torso", 10, 3, 5);
            
            // Create a mech with the torso
            var mech = new Mech("Test", "TST-1A", 50, [torso]);
            mech.AssignPilot(new MechWarrior("John", "Doe"));

            // Act
            var result = _sut.GetPsrBreakdown(mech, PilotingSkillRollType.GyroHit);

            // Assert
            result.BasePilotingSkill.ShouldBe(mech.Pilot!.Piloting);
            result.Modifiers.Count.ShouldBe(0); // No modifiers because no conditions are met
            result.ModifiedPilotingSkill.ShouldBe(mech.Pilot.Piloting); // No modifiers applied
        }

        [Fact]
        public void GetPsrBreakdown_SideTorsoWithoutGyro_ThrowsArgumentException()
        {
            // Arrange
            // Create a side torso (which doesn't have a gyro)
            var sideTorso = new SideTorso("Left Torso", PartLocation.LeftTorso, 10, 3, 5);
            
            // Create a mech with only the side torso
            var mech = new Mech("Test", "TST-1A", 50, [sideTorso]);
            mech.AssignPilot(new MechWarrior("John", "Doe"));
            
            // Set up the rules provider to return a modifier value for gyro hits
            _mockRulesProvider.GetPilotingSkillRollModifier(PilotingSkillRollType.GyroHit).Returns(3);

            // Act & Assert
            Should.Throw<ArgumentException>(() => _sut.GetPsrBreakdown(mech, PilotingSkillRollType.GyroHit))
                .Message.ShouldContain("No gyro found");
        }

        [Fact]
        public void GetPsrBreakdown_ImpossibleRoll_IsImpossibleReturnsTrue()
        {
            // Arrange
            // Create a torso with a gyro
            var torso = new CenterTorso("Test Torso", 10, 3, 5);
            var gyro = torso.GetComponent<Gyro>()!;
            gyro.Hit(); // Apply 1 hit to the gyro
            
            // Create a mech with the torso
            var mech = new Mech("Test", "TST-1A", 50, [torso]);
            mech.AssignPilot(new MechWarrior("John", "Doe"));
            
            // Set up the rules provider to return a high modifier value
            _mockRulesProvider.GetPilotingSkillRollModifier(PilotingSkillRollType.GyroHit).Returns(10);

            // Act
            var result = _sut.GetPsrBreakdown(mech, PilotingSkillRollType.GyroHit);

            // Assert
            // The base piloting skill + 10 should be >= 13, which is impossible on 2d6
            result.BasePilotingSkill.ShouldBe(mech.Pilot!.Piloting);
            result.Modifiers.Count.ShouldBe(1); // Only gyro modifier
            result.ModifiedPilotingSkill.ShouldBe(mech.Pilot.Piloting + 10);
            result.IsImpossible.ShouldBe(result.ModifiedPilotingSkill >= 13);
        }

        [Fact]
        public void GetPsrBreakdown_PilotDamageFromFallZeroLevels_AddsZeroValueModifier()
        {
            // Arrange
            var torso = new CenterTorso("Test Torso", 10, 3, 5);
            var mech = new Mech("Test", "TST-1A", 50, [torso]);
            mech.AssignPilot(new MechWarrior("John", "Doe"));
            
            // Act
            var result = _sut.GetPsrBreakdown(mech, PilotingSkillRollType.PilotDamageFromFall);

            // Assert
            result.BasePilotingSkill.ShouldBe(mech.Pilot!.Piloting);
            result.Modifiers.Count.ShouldBe(1); 
            result.Modifiers.ShouldContain(m => m is FallingLevelsModifier);
            var fallingModifier = (FallingLevelsModifier)result.Modifiers.First(m => m is FallingLevelsModifier);
            fallingModifier.Value.ShouldBe(0); // 0 levels fallen means no modifier
            fallingModifier.LevelsFallen.ShouldBe(0);
            result.ModifiedPilotingSkill.ShouldBe(mech.Pilot.Piloting); // No change to difficulty
        }
        
        [Fact]
        public void GetPsrBreakdown_OnlyRequestedModifiersAreApplied()
        {
            // Arrange
            var torso = new CenterTorso("Test Torso", 10, 3, 5);
            var gyro = torso.GetComponent<Gyro>()!;
            gyro.Hit(); // Apply 1 hit to the gyro
            var mech = new Mech("Test", "TST-1A", 50, [torso]);
            mech.AssignPilot(new MechWarrior("John", "Doe"));
            
            // Set up the rules provider to return a modifier value for gyro hits
            _mockRulesProvider.GetPilotingSkillRollModifier(PilotingSkillRollType.GyroHit).Returns(3);

            // Act - request PilotDamageFromFall, but gyro is hit so standard modifier should apply
            var result = _sut.GetPsrBreakdown(mech, PilotingSkillRollType.PilotDamageFromFall);

            // Assert
            result.BasePilotingSkill.ShouldBe(mech.Pilot!.Piloting);
            result.Modifiers.Count.ShouldBe(2); // Standard gyro modifier + falling modifier
            result.Modifiers.ShouldContain(m => m is FallingLevelsModifier);
            result.Modifiers.ShouldContain(m => m is DamagedGyroModifier);
            result.ModifiedPilotingSkill.ShouldBe(mech.Pilot.Piloting + 3); // Only gyro modifier has value
        }

        [Fact]
        public void GetPsrBreakdown_LowerLegActuator_ShouldNotAddModifier_WhenNoActuatorsAreDestroyed()
        {
            // Arrange
            var torso = new CenterTorso("Test Torso", 10, 3, 5);
            var mech = new Mech("Test", "TST-1A", 50, [torso]);
            mech.AssignPilot(new MechWarrior("John", "Doe"));
            _mockRulesProvider.GetPilotingSkillRollModifier(PilotingSkillRollType.LowerLegActuatorHit).Returns(1);

            // Act
            var result = _sut.GetPsrBreakdown(mech, PilotingSkillRollType.LowerLegActuatorHit);

            // Assert
            result.BasePilotingSkill.ShouldBe(mech.Pilot!.Piloting);
            result.Modifiers.Count.ShouldBe(0); // No modifiers because no actuators are destroyed
            result.ModifiedPilotingSkill.ShouldBe(mech.Pilot.Piloting);
        }
        
        [Fact]
        public void GetPsrBreakdown_OnlyGyroHitRequested_LowerLegActuatorHitModifierNotAdded()
        {
            // Arrange
            var torso = new CenterTorso("Test Torso", 10, 3, 5);
            var gyro = torso.GetComponent<Gyro>()!;
            gyro.Hit(); // Apply 1 hit to the gyro
            var mech = new Mech("Test", "TST-1A", 50, [torso]);
            mech.AssignPilot(new MechWarrior("John", "Doe"));
            
            _mockRulesProvider.GetPilotingSkillRollModifier(PilotingSkillRollType.GyroHit).Returns(3);
            // No setup for LowerLegActuatorHit, to ensure it's not called or added if not requested

            // Act
            var result = _sut.GetPsrBreakdown(mech, PilotingSkillRollType.GyroHit);

            // Assert
            result.BasePilotingSkill.ShouldBe(mech.Pilot!.Piloting);
            result.Modifiers.Count.ShouldBe(1); // Only gyro modifier
            result.Modifiers.ShouldContain(m => m is DamagedGyroModifier);
            result.ModifiedPilotingSkill.ShouldBe(mech.Pilot.Piloting + 3);
            _mockRulesProvider.DidNotReceive().GetPilotingSkillRollModifier(PilotingSkillRollType.LowerLegActuatorHit);
        }

        [Fact]
        public void GetPsrBreakdown_HeavyDamage_ModifierHasCorrectDamageTakenValue()
        {
            // Arrange
            var torso = new CenterTorso("Test Torso", 10, 3, 5);
            var mech = new Mech("Test", "TST-1A", 50, [torso]);
            mech.AssignPilot(new MechWarrior("John", "Doe"));
            _mockRulesProvider.GetPilotingSkillRollModifier(PilotingSkillRollType.HeavyDamage).Returns(1);
            _mockRulesProvider.GetHeavyDamageThreshold().Returns(20);
            // apply damage
            const int specificDamage = 33;
            mech.ApplyDamage(
                [CreateHitDataForLocation(PartLocation.CenterTorso, specificDamage, [],[])
                    ,], HitDirection.Front);

            // Act
            var result = _sut.GetPsrBreakdown(mech, PilotingSkillRollType.HeavyDamage);

            // Assert
            var heavyDamageModifier = result.Modifiers.OfType<HeavyDamageModifier>().FirstOrDefault();
            heavyDamageModifier.ShouldNotBeNull();
            heavyDamageModifier.Value.ShouldBe(1);
            heavyDamageModifier.DamageTaken.ShouldBe(specificDamage);
        }

        [Fact]
        public void GetPsrBreakdown_HeavyDamageBelowThreshold_NoHeavyDamageModifier()
        {
            // Arrange
            var torso = new CenterTorso("Test Torso", 10, 3, 5);
            var mech = new Mech("Test", "TST-1A", 50, [torso]);
            mech.AssignPilot(new MechWarrior("John", "Doe"));
            _mockRulesProvider.GetHeavyDamageThreshold().Returns(20);
            const int lowDamage = 15;
            mech.ApplyDamage(
                [CreateHitDataForLocation(PartLocation.CenterTorso, lowDamage, [],[]),], HitDirection.Front);

            // Act
            var result = _sut.GetPsrBreakdown(mech, PilotingSkillRollType.HeavyDamage);

            // Assert
            result.Modifiers.ShouldNotContain(m => m is HeavyDamageModifier);
        }

        [Fact]
        public void GetPsrBreakdown_DestroyedLowerLegActuator_AddsModifier()
        {
            // Arrange
            var torso = new CenterTorso("Test Torso", 10, 3, 5);
            var leg = new Leg("Right Leg", PartLocation.RightLeg, 10, 5);
            var actuator = leg.GetComponents<LowerLegActuator>().First();
            actuator.Hit(); // Destroy the actuator (assuming 1 health point)
            
            var mech = new Mech("Test", "TST-1A", 50, [torso,leg]);
            mech.AssignPilot(new MechWarrior("John", "Doe"));
            _mockRulesProvider.GetPilotingSkillRollModifier(PilotingSkillRollType.LowerLegActuatorHit).Returns(1);

            // Act
            var result = _sut.GetPsrBreakdown(mech, PilotingSkillRollType.GyroHit);

            // Assert
            result.Modifiers.ShouldContain(m => m is LowerLegActuatorHitModifier);
        }

        [Fact]
        public void GetPsrBreakdown_UndamagedLowerLegActuator_NoActuatorModifier()
        {
            // Arrange
            var torso = new CenterTorso("Test Torso", 10, 3, 5);
            var leg = new Leg("Right Leg", PartLocation.RightLeg, 10, 5);
            var mech = new Mech("Test", "TST-1A", 50, [torso,leg]);
            mech.AssignPilot(new MechWarrior("John", "Doe"));

            // Act
            var result = _sut.GetPsrBreakdown(mech, PilotingSkillRollType.GyroHit);

            // Assert
            result.Modifiers.ShouldNotContain(m => m is LowerLegActuatorHitModifier);
        }

        [Fact]
        public void GetPsrBreakdown_DestroyedUpperLegActuator_AddsModifier()
        {
            // Arrange
            var torso = new CenterTorso("Test Torso", 10, 3, 5);
            var leg = new Leg("Right Leg", PartLocation.RightLeg, 10, 5);
            var actuator = leg.GetComponents<UpperLegActuator>().First();
            actuator.Hit(); // Destroy the actuator (assuming 1 health point)

            var mech = new Mech("Test", "TST-1A", 50, [torso,leg]);
            mech.AssignPilot(new MechWarrior("John", "Doe"));
            _mockRulesProvider.GetPilotingSkillRollModifier(PilotingSkillRollType.UpperLegActuatorHit).Returns(1);

            // Act
            var result = _sut.GetPsrBreakdown(mech, PilotingSkillRollType.GyroHit);

            // Assert
            result.Modifiers.ShouldContain(m => m is UpperLegActuatorHitModifier);
        }

        [Fact]
        public void GetPsrBreakdown_ShouldReturnNoModifiers_ForNonMech()
        {
            // Arrange
            var notMech = new UnitTests.TestUnit("Test", "TST-1A", 50, []);
            notMech.AssignPilot(new MechWarrior("Test", "Test"));

            // Act
            var result = _sut.GetPsrBreakdown(notMech, PilotingSkillRollType.GyroHit);

            // Assert
            result.Modifiers.Count.ShouldBe(0);
        }
        
        [Fact]
        public void GetPsrBreakdown_ShouldThrow_WhenNoPilot()
        {
            // Arrange
            var notMech = new UnitTests.TestUnit("Test", "TST-1A", 50, []);

            // Act & Assert
            Should.Throw<ArgumentException>(() => _sut.GetPsrBreakdown(notMech, PilotingSkillRollType.GyroHit))
                .Message.ShouldContain("pilot");
        }

        [Fact]
        public void GetPsrBreakdown_DestroyedHipActuator_AddsModifier()
        {
            // Arrange
            var torso = new CenterTorso("Test Torso", 10, 3, 5);
            var leg = new Leg("Right Leg", PartLocation.RightLeg, 10, 5);
            var hipActuator = leg.GetComponents<HipActuator>().First();
            hipActuator.Hit(); // Destroy the hip actuator (assuming 1 health point)

            var mech = new Mech("Test", "TST-1A", 50, [torso, leg]);
            mech.AssignPilot(new MechWarrior("John", "Doe"));
            _mockRulesProvider.GetPilotingSkillRollModifier(PilotingSkillRollType.HipActuatorHit).Returns(2);

            // Act
            var result = _sut.GetPsrBreakdown(mech, PilotingSkillRollType.GyroHit);

            // Assert
            result.Modifiers.ShouldContain(m => m is HipActuatorHitModifier && m.Value == 2);
        }

        [Fact]
        public void GetPsrBreakdown_DestroyedFootActuator_AddsModifier()
        {
            // Arrange
            var torso = new CenterTorso("Test Torso", 10, 3, 5);
            var leg = new Leg("Right Leg", PartLocation.RightLeg, 10, 5);
            var footActuator = leg.GetComponents<FootActuator>().First();
            footActuator.Hit(); // Destroy the foot actuator (assuming 1 health point)

            var mech = new Mech("Test", "TST-1A", 50, [torso, leg]);
            mech.AssignPilot(new MechWarrior("John", "Doe"));
            _mockRulesProvider.GetPilotingSkillRollModifier(PilotingSkillRollType.FootActuatorHit).Returns(1);

            // Act
            var result = _sut.GetPsrBreakdown(mech, PilotingSkillRollType.GyroHit);

            // Assert
            result.Modifiers.ShouldContain(m => m is FootActuatorHitModifier && m.Value == 1);
        }
        
        [Fact]
        public void GetPsrBreakdown_BlownOffLeg_ForPilotDamageFromFall_AddsModifier()
        {
            // Arrange
            var torso = new CenterTorso("Test Torso", 10, 3, 5);
            var leftLeg = new Leg("Left Leg", PartLocation.LeftLeg, 10, 5);
            leftLeg.BlowOff(); // Blow off the left leg

            var mech = new Mech("Test", "TST-1A", 50, [torso, leftLeg]);
            mech.AssignPilot(new MechWarrior("John", "Doe"));
            _mockRulesProvider.GetPilotingSkillRollModifier(PilotingSkillRollType.LegDestroyed).Returns(5);

            // Act
            var result = _sut.GetPsrBreakdown(mech, PilotingSkillRollType.PilotDamageFromFall);

            // Assert
            result.Modifiers.ShouldContain(m => m is LegDestroyedModifier && m.Value == 5);
        }

        [Fact]
        public void GetPsrBreakdown_TwoDestroyedLegs_ForPilotDamageFromFall_AddsTwoSeparateModifiers()
        {
            // Arrange
            var torso = new CenterTorso("Test Torso", 10, 3, 5);
            var leftLeg = new Leg("Left Leg", PartLocation.LeftLeg, 10, 5);
            var rightLeg = new Leg("Right Leg", PartLocation.RightLeg, 10, 5);
            leftLeg.ApplyDamage(100, HitDirection.Front); // Destroy the left leg
            rightLeg.ApplyDamage(100, HitDirection.Front); // Destroy the right leg

            var mech = new Mech("Test", "TST-1A", 50, [torso, leftLeg, rightLeg]);
            mech.AssignPilot(new MechWarrior("John", "Doe"));
            _mockRulesProvider.GetPilotingSkillRollModifier(PilotingSkillRollType.LegDestroyed).Returns(5);

            // Act
            var result = _sut.GetPsrBreakdown(mech, PilotingSkillRollType.PilotDamageFromFall);

            // Assert
            var legModifiers = result.Modifiers.OfType<LegDestroyedModifier>().ToList();
            legModifiers.Count.ShouldBe(2); // Two separate modifiers, one for each destroyed leg
            legModifiers.All(m => m.Value == 5).ShouldBeTrue(); // Each modifier should have base value of 5
        }

        [Fact]
        public void GetPsrBreakdown_GyroDestroyed_ForPilotDamageFromFall_AddsDestroyedGyroModifier()
        {
            // Arrange
            var torso = new CenterTorso("Test Torso", 10, 3, 5);
            var gyro = torso.GetComponents<Gyro>().First();
            gyro.Hit(); // First hit
            gyro.Hit(); // Second hit - destroys gyro (2 health points)

            var mech = new Mech("Test", "TST-1A", 50, [torso]);
            mech.AssignPilot(new MechWarrior("John", "Doe"));
            _mockRulesProvider.GetPilotingSkillRollModifier(PilotingSkillRollType.GyroDestroyed).Returns(6);

            // Act
            var result = _sut.GetPsrBreakdown(mech, PilotingSkillRollType.PilotDamageFromFall);

            // Assert
            var gyroModifiers = result.Modifiers.OfType<DamagedGyroModifier>().ToList();
            gyroModifiers.Count.ShouldBe(1);
            gyroModifiers.First().Value.ShouldBe(6); // Destroyed gyro modifier
            gyroModifiers.First().HitsCount.ShouldBe(2);
        }

        [Fact]
        public void GetPsrBreakdown_GyroHit_AddsHitModifierNotDestroyedModifier()
        {
            // Arrange
            var torso = new CenterTorso("Test Torso", 10, 3, 5);
            var gyro = torso.GetComponents<Gyro>().First();
            gyro.Hit(); // Only one hit - damaged but not destroyed

            var mech = new Mech("Test", "TST-1A", 50, [torso]);
            mech.AssignPilot(new MechWarrior("John", "Doe"));
            _mockRulesProvider.GetPilotingSkillRollModifier(PilotingSkillRollType.GyroHit).Returns(3);

            // Act
            var result = _sut.GetPsrBreakdown(mech, PilotingSkillRollType.GyroHit);

            // Assert
            var gyroModifiers = result.Modifiers.OfType<DamagedGyroModifier>().ToList();
            gyroModifiers.Count.ShouldBe(1);
            gyroModifiers.First().Value.ShouldBe(3); // Hit modifier, not destroyed modifier
            gyroModifiers.First().HitsCount.ShouldBe(1);
        }

        [Fact]
        public void EvaluateRoll_UnconsciousPilot_ShouldReturnAutomaticFailure()
        {
            // Arrange
            var torso = new CenterTorso("Test Torso", 10, 3, 5);
            var mech = new Mech("Test", "TST-1A", 50, [torso]);
            var pilot = new MechWarrior("John", "Doe");
            pilot.KnockUnconscious(1); // Make pilot unconscious
            mech.AssignPilot(pilot);

            var psrBreakdown = new PsrBreakdown
            {
                BasePilotingSkill = 5,
                Modifiers = new List<RollModifier>()
            };

            // Act
            var result = _sut.EvaluateRoll(psrBreakdown, mech, PilotingSkillRollType.GyroHit);

            // Assert
            result.RollType.ShouldBe(PilotingSkillRollType.GyroHit);
            result.IsSuccessful.ShouldBeFalse();
            result.DiceResults.ShouldBe([]); // Automatic failure representation
            result.PsrBreakdown.ShouldBe(psrBreakdown);
            // Verify no dice were rolled for unconscious pilot
            _mockDiceRoller.DidNotReceive().Roll2D6();
        }

        [Fact]
        public void EvaluateRoll_ConsciousPilotSuccessfulRoll_ShouldReturnSuccessData()
        {
            // Arrange
            var torso = new CenterTorso("Test Torso", 10, 3, 5);
            var mech = new Mech("Test", "TST-1A", 50, [torso]);
            mech.AssignPilot(new MechWarrior("John", "Doe"));

            var psrBreakdown = new PsrBreakdown
            {
                BasePilotingSkill = 5,
                Modifiers = new List<RollModifier>()
            };

            // Mock dice roll that succeeds (7 >= 5)
            _mockDiceRoller.Roll2D6().Returns([new DiceResult(3), new DiceResult(4)]);

            // Act
            var result = _sut.EvaluateRoll(psrBreakdown, mech, PilotingSkillRollType.StandupAttempt);

            // Assert
            result.RollType.ShouldBe(PilotingSkillRollType.StandupAttempt);
            result.IsSuccessful.ShouldBeTrue();
            result.DiceResults.ShouldBe([3, 4]);
            result.PsrBreakdown.ShouldBe(psrBreakdown);
            _mockDiceRoller.Received(1).Roll2D6();
        }

        [Fact]
        public void EvaluateRoll_ConsciousPilotFailedRoll_ShouldReturnFailureData()
        {
            // Arrange
            var torso = new CenterTorso("Test Torso", 10, 3, 5);
            var mech = new Mech("Test", "TST-1A", 50, [torso]);
            mech.AssignPilot(new MechWarrior("John", "Doe"));

            var psrBreakdown = new PsrBreakdown
            {
                BasePilotingSkill = 8,
                Modifiers = new List<RollModifier>()
            };

            // Mock dice roll that fails (4 < 8)
            _mockDiceRoller.Roll2D6().Returns([new DiceResult(2), new DiceResult(2)]);

            // Act
            var result = _sut.EvaluateRoll(psrBreakdown, mech, PilotingSkillRollType.JumpWithDamage);

            // Assert
            result.RollType.ShouldBe(PilotingSkillRollType.JumpWithDamage);
            result.IsSuccessful.ShouldBeFalse();
            result.DiceResults.ShouldBe([2, 2]);
            result.PsrBreakdown.ShouldBe(psrBreakdown);
            _mockDiceRoller.Received(1).Roll2D6();
        }

        [Fact]
        public void EvaluateRoll_NoPilot_ShouldThrowArgumentException()
        {
            // Arrange
            var torso = new CenterTorso("Test Torso", 10, 3, 5);
            var mech = new Mech("Test", "TST-1A", 50, [torso]);
            // No pilot assigned

            var psrBreakdown = new PsrBreakdown
            {
                BasePilotingSkill = 5,
                Modifiers = new List<RollModifier>()
            };

            // Act & Assert
            Should.Throw<ArgumentException>(() => _sut.EvaluateRoll(psrBreakdown, mech, PilotingSkillRollType.GyroHit))
                .Message.ShouldContain("pilot");
        }
    }
}
