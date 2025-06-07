using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Data.Game.Mechanics;

public class FallReasonTypeTests
{
    [Theory]
    [InlineData(FallReasonType.GyroHit, PilotingSkillRollType.GyroHit)]
    [InlineData(FallReasonType.LowerLegActuatorHit, PilotingSkillRollType.LowerLegActuatorHit)]
    [InlineData(FallReasonType.HeavyDamage, PilotingSkillRollType.HeavyDamage)]
    public void ToPilotingSkillRollType_ForTypesRequiringPSR_ReturnsCorrectType(FallReasonType reasonType, PilotingSkillRollType expected)
    {
        // Act
        var result = reasonType.ToPilotingSkillRollType();

        // Assert
        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData(FallReasonType.GyroDestroyed)]
    [InlineData(FallReasonType.LegDestroyed)]
    public void ToPilotingSkillRollType_ForTypesWithNoRequiredPSR_ReturnsNull(FallReasonType reasonType)
    {
        // Act
        var result = reasonType.ToPilotingSkillRollType();

        // Assert
        result.ShouldBeNull();
    }

    [Theory]
    [InlineData(FallReasonType.GyroHit, true)]
    [InlineData(FallReasonType.LowerLegActuatorHit, true)]
    [InlineData(FallReasonType.HeavyDamage, true)]
    [InlineData(FallReasonType.GyroDestroyed, false)]
    [InlineData(FallReasonType.LegDestroyed, false)]
    public void RequiresPilotingSkillRoll_ReturnsCorrectValue(FallReasonType reasonType, bool expected)
    {
        // Act
        var result = reasonType.RequiresPilotingSkillRoll();

        // Assert
        result.ShouldBe(expected);
    }
}
