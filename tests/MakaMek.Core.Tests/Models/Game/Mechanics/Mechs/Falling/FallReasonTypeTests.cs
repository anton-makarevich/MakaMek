using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Mechs.Falling;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Mechanics.Mechs.Falling;

public class FallReasonTypeTests
{
    [Theory]
    [InlineData(FallReasonType.GyroHit, PilotingSkillRollType.GyroHit)]
    [InlineData(FallReasonType.LowerLegActuatorHit, PilotingSkillRollType.LowerLegActuatorHit)]
    [InlineData(FallReasonType.UpperLegActuatorHit, PilotingSkillRollType.UpperLegActuatorHit)]
    [InlineData(FallReasonType.HeavyDamage, PilotingSkillRollType.HeavyDamage)]
    [InlineData(FallReasonType.StandUpAttempt, PilotingSkillRollType.StandupAttempt)]
    [InlineData(FallReasonType.JumpWithDamage, PilotingSkillRollType.JumpWithDamage)]
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
    [InlineData(FallReasonType.HipActuatorHit, true)]
    [InlineData(FallReasonType.FootActuatorHit, true)]
    [InlineData(FallReasonType.HeavyDamage, true)]
    [InlineData(FallReasonType.GyroDestroyed, false)]
    [InlineData(FallReasonType.LegDestroyed, false)]
    [InlineData(FallReasonType.StandUpAttempt, true)]
    [InlineData(FallReasonType.JumpWithDamage, true)]
    public void RequiresPilotingSkillRoll_ReturnsCorrectValue(FallReasonType reasonType, bool expected)
    {
        // Act
        var result = reasonType.RequiresPilotingSkillRoll();

        // Assert
        result.ShouldBe(expected);
    }
}
