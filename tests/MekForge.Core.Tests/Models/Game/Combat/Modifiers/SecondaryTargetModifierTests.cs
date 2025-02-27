using NSubstitute;
using Sanet.MekForge.Core.Models.Game.Combat.Modifiers;
using Sanet.MekForge.Core.Services.Localization;
using Shouldly;

namespace Sanet.MekForge.Core.Tests.Models.Game.Combat.Modifiers;

public class SecondaryTargetModifierTests
{
    private readonly ILocalizationService _localizationService = Substitute.For<ILocalizationService>();

    [Fact]
    public void Format_FrontArc_ShouldFormatCorrectly()
    {
        // Arrange
        var modifier = new SecondaryTargetModifier
        {
            Value = 1,
            IsInFrontArc = true
        };
        _localizationService.GetString("Attack_SecondaryTargetFrontArc").Returns("Secondary target (front arc): +{0}");

        // Act
        var result = modifier.Format(_localizationService);

        // Assert
        result.ShouldBe("Secondary target (front arc): +1");
        _localizationService.Received(1).GetString("Attack_SecondaryTargetFrontArc");
    }

    [Fact]
    public void Format_OtherArc_ShouldFormatCorrectly()
    {
        // Arrange
        var modifier = new SecondaryTargetModifier
        {
            Value = 2,
            IsInFrontArc = false
        };
        _localizationService.GetString("Attack_SecondaryTargetOtherArc").Returns("Secondary target (other arc): +{0}");

        // Act
        var result = modifier.Format(_localizationService);

        // Assert
        result.ShouldBe("Secondary target (other arc): +2");
        _localizationService.Received(1).GetString("Attack_SecondaryTargetOtherArc");
    }
}
