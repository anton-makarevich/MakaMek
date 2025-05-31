using NSubstitute;
using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.PilotingSkill;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components.Internal;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Core.Utils.TechRules;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Mechanics
{
    public class PilotingSkillCalculatorTests
    {
        private readonly IRulesProvider _mockRulesProvider;
        private readonly IPilotingSkillCalculator _sut;
        
        public PilotingSkillCalculatorTests()
        {
            _mockRulesProvider = Substitute.For<IRulesProvider>();
            _sut = new PilotingSkillCalculator(_mockRulesProvider);
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
            var mech = new Mech("Test", "TST-1A", 50, 4, [torso]);
            
            // Set up the rules provider to return a modifier value for gyro hits
            _mockRulesProvider.GetPilotingSkillRollModifier(PilotingSkillRollType.GyroHit).Returns(3);

            // Act
            var result = _sut.GetPsrBreakdown(mech, [PilotingSkillRollType.GyroHit]);

            // Assert
            result.BasePilotingSkill.ShouldBe(mech.Crew!.Piloting);
            result.Modifiers.Count.ShouldBe(1);
            result.Modifiers[0].ShouldBeOfType<DamagedGyroModifier>();
            var gyroModifier = (DamagedGyroModifier)result.Modifiers[0];
            gyroModifier.Value.ShouldBe(3);
            gyroModifier.HitsCount.ShouldBe(1);
            result.ModifiedPilotingSkill.ShouldBe(mech.Crew.Piloting + 3);
        }

        [Fact]
        public void GetPsrBreakdown_NoGyroHits_NoGyroModifierAdded()
        {
            // Arrange
            // Create a torso with a gyro that has no hits
            var torso = new CenterTorso("Test Torso", 10, 3, 5);
            
            // Create a mech with the torso
            var mech = new Mech("Test", "TST-1A", 50, 4, [torso]);

            // Act
            var result = _sut.GetPsrBreakdown(mech,[]);

            // Assert
            result.BasePilotingSkill.ShouldBe(mech.Crew!.Piloting);
            result.Modifiers.Count.ShouldBe(0);
            result.ModifiedPilotingSkill.ShouldBe(mech.Crew.Piloting); // No modifiers applied
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
            var mech = new Mech("Test", "TST-1A", 50, 4, [torso]);
            
            // Set up the rules provider to return a high modifier value
            _mockRulesProvider.GetPilotingSkillRollModifier(PilotingSkillRollType.GyroHit).Returns(10);

            // Act
            var result = _sut.GetPsrBreakdown(mech,[PilotingSkillRollType.GyroHit]);

            // Assert
            // The base piloting skill + 10 should be >= 13, which is impossible on 2d6
            result.BasePilotingSkill.ShouldBe(mech.Crew!.Piloting);
            result.Modifiers.Count.ShouldBe(1);
            result.ModifiedPilotingSkill.ShouldBe(mech.Crew.Piloting + 10);
            result.IsImpossible.ShouldBe(result.ModifiedPilotingSkill >= PsrBreakdown.ImpossibleRoll);
        }

        [Fact]
        public void GetPsrBreakdown_SideTorsoWithoutGyro_ThrowsArgumentException()
        {
            // Arrange
            // Create a side torso (which doesn't have a gyro)
            var sideTorso = new SideTorso("Left Torso", PartLocation.LeftTorso, 10, 3, 5);
            
            // Create a mech with only the side torso
            var mech = new Mech("Test", "TST-1A", 50, 4, [sideTorso]);
            
            // Set up the rules provider to return a modifier value for gyro hits
            _mockRulesProvider.GetPilotingSkillRollModifier(PilotingSkillRollType.GyroHit).Returns(3);

            // Act & Assert
            Should.Throw<ArgumentException>(() => _sut.GetPsrBreakdown(mech, [PilotingSkillRollType.GyroHit]))
                .Message.ShouldContain("No gyro found");
        }

        [Fact]
        public void GetPsrBreakdown_WarriorDamageFromFall_AddsZeroValueModifier()
        {
            // Arrange
            var mech = new Mech("Test", "TST-1A", 50, 4, []);
            
            // Act
            var result = _sut.GetPsrBreakdown(mech, [PilotingSkillRollType.PilotDamageFromFall]);

            // Assert
            result.BasePilotingSkill.ShouldBe(mech.Crew!.Piloting);
            result.Modifiers.Count.ShouldBe(1); // One modifier should be added
            result.Modifiers[0].ShouldBeOfType<FallingLevelsModifier>();
            var fallingModifier = (FallingLevelsModifier)result.Modifiers[0];
            fallingModifier.Value.ShouldBe(0); // 0 levels fallen means no modifier
            fallingModifier.LevelsFallen.ShouldBe(0);
            result.ModifiedPilotingSkill.ShouldBe(mech.Crew.Piloting); // No change to difficulty
        }

        [Fact]
        public void GetPsrBreakdown_MultipleRollTypes_CalculatesAllRequestedModifiers()
        {
            // Arrange
            var torso = new CenterTorso("Test Torso", 10, 3, 5);
            var gyro = torso.GetComponent<Gyro>()!;
            gyro.Hit(); // Apply 1 hit to the gyro
            var mech = new Mech("Test", "TST-1A", 50, 4, [torso]);
            
            // Set up the rules provider to return a modifier value for gyro hits
            _mockRulesProvider.GetPilotingSkillRollModifier(PilotingSkillRollType.GyroHit).Returns(3);

            // Act - request both GyroHit and WarriorDamageFromFall modifiers
            var result = _sut.GetPsrBreakdown(
                mech, 
                [PilotingSkillRollType.GyroHit, PilotingSkillRollType.PilotDamageFromFall]
            );

            // Assert
            result.BasePilotingSkill.ShouldBe(mech.Crew!.Piloting);
            result.Modifiers.Count.ShouldBe(2); // Both modifiers should be present
            
            // Check for gyro modifier
            result.Modifiers.ShouldContain(m => m is DamagedGyroModifier);
            var gyroModifier = result.Modifiers.OfType<DamagedGyroModifier>().First();
            gyroModifier.Value.ShouldBe(3);
            gyroModifier.HitsCount.ShouldBe(1);
            
            // Check for falling levels modifier
            result.Modifiers.ShouldContain(m => m is FallingLevelsModifier);
            var fallingModifier = result.Modifiers.OfType<FallingLevelsModifier>().First();
            fallingModifier.Value.ShouldBe(0); // 0 levels fallen = no modifier
            fallingModifier.LevelsFallen.ShouldBe(0);
            
            // Total modifier should be sum of both
            result.ModifiedPilotingSkill.ShouldBe(mech.Crew.Piloting + 3); // Only gyro modifier affects the total
        }

        [Fact]
        public void GetPsrBreakdown_OnlyRequestedModifiersAreApplied()
        {
            // Arrange
            var torso = new CenterTorso("Test Torso", 10, 3, 5);
            var gyro = torso.GetComponent<Gyro>()!;
            gyro.Hit(); // Apply 1 hit to the gyro
            var mech = new Mech("Test", "TST-1A", 50, 4, [torso]);
            
            // Set up the rules provider to return a modifier value for gyro hits
            _mockRulesProvider.GetPilotingSkillRollModifier(PilotingSkillRollType.GyroHit).Returns(3);

            // Act - only request WarriorDamageFromFall, not GyroHit
            var result = _sut.GetPsrBreakdown(mech, [PilotingSkillRollType.PilotDamageFromFall]);

            // Assert
            result.BasePilotingSkill.ShouldBe(mech.Crew!.Piloting);
            result.Modifiers.Count.ShouldBe(1); // Only the falling modifier should be present
            result.Modifiers.ShouldNotContain(m => m is DamagedGyroModifier); // No gyro modifier
            result.Modifiers[0].ShouldBeOfType<FallingLevelsModifier>();
            result.ModifiedPilotingSkill.ShouldBe(mech.Crew.Piloting); // No change to difficulty
        }

        [Fact]
        public void GetPsrBreakdown_EmptyRollTypesList_NoModifiersApplied()
        {
            // Arrange
            var torso = new CenterTorso("Test Torso", 10, 3, 5);
            var gyro = torso.GetComponent<Gyro>()!;
            gyro.Hit(); // Apply 1 hit to the gyro
            var mech = new Mech("Test", "TST-1A", 50, 4, [torso]);
            
            // Set up the rules provider to return a modifier value for gyro hits
            _mockRulesProvider.GetPilotingSkillRollModifier(PilotingSkillRollType.GyroHit).Returns(3);

            // Act - provide an empty list of roll types
            var result = _sut.GetPsrBreakdown(mech, []);

            // Assert
            result.BasePilotingSkill.ShouldBe(mech.Crew!.Piloting);
            result.Modifiers.Count.ShouldBe(0); // No modifiers should be applied
            result.ModifiedPilotingSkill.ShouldBe(mech.Crew.Piloting); // No change to difficulty
        }

        [Fact]
        public void GetPsrBreakdown_LowerLegActuatorHitRequested_AddsModifier()
        {
            // Arrange
            var mech = new Mech("Test", "TST-1A", 50, 4, []);
            _mockRulesProvider.GetPilotingSkillRollModifier(PilotingSkillRollType.LowerLegActuatorHit).Returns(1);

            // Act
            var result = _sut.GetPsrBreakdown(mech, [PilotingSkillRollType.LowerLegActuatorHit]);

            // Assert
            result.BasePilotingSkill.ShouldBe(mech.Crew!.Piloting);
            result.Modifiers.Count.ShouldBe(1);
            result.Modifiers[0].ShouldBeOfType<LowerLegActuatorHitModifier>();
            var actuatorModifier = (LowerLegActuatorHitModifier)result.Modifiers[0];
            actuatorModifier.Value.ShouldBe(1);
            result.ModifiedPilotingSkill.ShouldBe(mech.Crew.Piloting + 1);
        }

        [Fact]
        public void GetPsrBreakdown_LowerLegActuatorHitAndGyroHitRequested_AddsBothModifiers()
        {
            // Arrange
            var torso = new CenterTorso("Test Torso", 10, 3, 5);
            var gyro = torso.GetComponent<Gyro>()!;
            gyro.Hit(); // Apply 1 hit to the gyro
            var mech = new Mech("Test", "TST-1A", 50, 4, [torso]);
            
            _mockRulesProvider.GetPilotingSkillRollModifier(PilotingSkillRollType.LowerLegActuatorHit).Returns(1);
            _mockRulesProvider.GetPilotingSkillRollModifier(PilotingSkillRollType.GyroHit).Returns(3);

            // Act
            var result = _sut.GetPsrBreakdown(mech, 
                [PilotingSkillRollType.LowerLegActuatorHit, PilotingSkillRollType.GyroHit]);

            // Assert
            result.BasePilotingSkill.ShouldBe(mech.Crew!.Piloting);
            result.Modifiers.Count.ShouldBe(2);
            
            result.Modifiers.ShouldContain(m => m is LowerLegActuatorHitModifier);
            var actuatorModifier = result.Modifiers.OfType<LowerLegActuatorHitModifier>().First();
            actuatorModifier.Value.ShouldBe(1);
            
            result.Modifiers.ShouldContain(m => m is DamagedGyroModifier);
            var gyroModifier = result.Modifiers.OfType<DamagedGyroModifier>().First();
            gyroModifier.Value.ShouldBe(3);
            gyroModifier.HitsCount.ShouldBe(1);
            
            result.ModifiedPilotingSkill.ShouldBe(mech.Crew.Piloting + 1 + 3);
        }

        [Fact]
        public void GetPsrBreakdown_OnlyGyroHitRequested_LowerLegActuatorHitModifierNotAdded()
        {
            // Arrange
            var torso = new CenterTorso("Test Torso", 10, 3, 5);
            var gyro = torso.GetComponent<Gyro>()!;
            gyro.Hit(); // Apply 1 hit to the gyro
            var mech = new Mech("Test", "TST-1A", 50, 4, [torso]);
            
            _mockRulesProvider.GetPilotingSkillRollModifier(PilotingSkillRollType.GyroHit).Returns(3);
            // No setup for LowerLegActuatorHit, to ensure it's not called or added if not requested

            // Act
            var result = _sut.GetPsrBreakdown(mech, [PilotingSkillRollType.GyroHit]);

            // Assert
            result.BasePilotingSkill.ShouldBe(mech.Crew!.Piloting);
            result.Modifiers.Count.ShouldBe(1);
            result.Modifiers[0].ShouldBeOfType<DamagedGyroModifier>();
            result.Modifiers.ShouldNotContain(m => m is LowerLegActuatorHitModifier);
            result.ModifiedPilotingSkill.ShouldBe(mech.Crew.Piloting + 3);
            _mockRulesProvider.DidNotReceive().GetPilotingSkillRollModifier(PilotingSkillRollType.LowerLegActuatorHit);
        }
    }
}
