using NSubstitute;
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
    }
}
