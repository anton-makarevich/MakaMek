using NSubstitute;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Attack;
using Sanet.Localization;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Mechanics.Modifiers.Attack;

public class JumpedTargetModifierTests
{
    private readonly ILocalizationService _localizationService = Substitute.For<ILocalizationService>();

    [Fact]
    public void Render_ShouldFormatCorrectly()
    {
        // Arrange
        var sut = new JumpedTargetModifier
        {
            Value = 1
        };
        _localizationService.GetString("Modifier_JumpedTarget").Returns("Target Jumped: +{0}");

        // Act
        var result = sut.Render(_localizationService);

        // Assert
        result.ShouldBe("Target Jumped: +1");
        _localizationService.Received(1).GetString("Modifier_JumpedTarget");
    }
}
