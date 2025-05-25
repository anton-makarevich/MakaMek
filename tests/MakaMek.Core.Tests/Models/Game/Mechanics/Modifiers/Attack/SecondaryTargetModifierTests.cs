using NSubstitute;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Attack;
using Sanet.MakaMek.Core.Services.Localization;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Mechanics.Modifiers.Attack;

public class SecondaryTargetModifierTests
{
    private readonly ILocalizationService _localizationService = Substitute.For<ILocalizationService>();

    [Fact]
    public void Render_FrontArc_ShouldFormatCorrectly()
    {
        // Arrange
        var modifier = new SecondaryTargetModifier
        {
            Value = 1,
            IsInFrontArc = true
        };
        _localizationService.GetString("Attack_SecondaryTargetFrontArc").Returns("Secondary target (front arc): +{0}");

        // Act
        var result = modifier.Render(_localizationService);

        // Assert
        result.ShouldBe("Secondary target (front arc): +1");
        _localizationService.Received(1).GetString("Attack_SecondaryTargetFrontArc");
    }

    [Fact]
    public void Render_OtherArc_ShouldFormatCorrectly()
    {
        // Arrange
        var modifier = new SecondaryTargetModifier
        {
            Value = 2,
            IsInFrontArc = false
        };
        _localizationService.GetString("Attack_SecondaryTargetOtherArc").Returns("Secondary target (other arc): +{0}");

        // Act
        var result = modifier.Render(_localizationService);

        // Assert
        result.ShouldBe("Secondary target (other arc): +2");
        _localizationService.Received(1).GetString("Attack_SecondaryTargetOtherArc");
    }
}
