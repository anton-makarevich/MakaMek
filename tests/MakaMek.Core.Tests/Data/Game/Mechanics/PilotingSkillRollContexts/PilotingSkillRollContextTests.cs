using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Data.Game.Mechanics.PilotingSkillRollContexts;
using Sanet.MakaMek.Localization;
using Shouldly;
using Xunit;

namespace Sanet.MakaMek.Core.Tests.Data.Game.Mechanics.PilotingSkillRollContexts;

public class PilotingSkillRollContextTests
{
    private readonly ILocalizationService _localizationService = new FakeLocalizationService();

    [Fact]
    public void Constructor_WhenCalled_SetsRollType()
    {
        // Arrange
        var rollType = PilotingSkillRollType.GyroHit;

        // Act
        var sut = new PilotingSkillRollContext(rollType);

        // Assert
        sut.RollType.ShouldBe(rollType);
    }

    [Fact]
    public void Render_WhenCalled_ReturnsLocalizedRollTypeName()
    {
        // Arrange
        var sut = new PilotingSkillRollContext(PilotingSkillRollType.GyroHit);

        // Act
        var result = sut.Render(_localizationService);

        // Assert
        result.ShouldBe("Gyro Hit");
    }

    [Theory]
    [InlineData(PilotingSkillRollType.GyroHit, "Gyro Hit")]
    [InlineData(PilotingSkillRollType.GyroDestroyed, "Gyro Destroyed")]
    [InlineData(PilotingSkillRollType.PilotDamageFromFall, "Pilot Damage From Fall")]
    [InlineData(PilotingSkillRollType.LowerLegActuatorHit, "Lower Leg Actuator Hit")]
    [InlineData(PilotingSkillRollType.UpperLegActuatorHit, "Upper Leg Actuator Hit")]
    [InlineData(PilotingSkillRollType.HeavyDamage, "Heavy Damage")]
    [InlineData(PilotingSkillRollType.HipActuatorHit, "Hip Actuator Hit")]
    [InlineData(PilotingSkillRollType.FootActuatorHit, "Foot Actuator Hit")]
    [InlineData(PilotingSkillRollType.LegDestroyed, "Leg Destroyed")]
    [InlineData(PilotingSkillRollType.StandupAttempt, "Standup Attempt")]
    [InlineData(PilotingSkillRollType.JumpWithDamage, "Jump with damage")]
    public void Render_WithDifferentRollTypes_ReturnsCorrectLocalizedString(PilotingSkillRollType rollType, string expected)
    {
        // Arrange
        var sut = new PilotingSkillRollContext(rollType);

        // Act
        var result = sut.Render(_localizationService);

        // Assert
        result.ShouldBe(expected);
    }
}
