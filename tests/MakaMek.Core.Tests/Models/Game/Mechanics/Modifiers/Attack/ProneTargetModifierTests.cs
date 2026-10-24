using NSubstitute;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Attack;
using Sanet.Localization;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Mechanics.Modifiers.Attack;

public class ProneTargetModifierTests
{
    private readonly ILocalizationService _localizationService = Substitute.For<ILocalizationService>();

    public ProneTargetModifierTests()
    {
        _localizationService.GetString("Modifier_ProneTarget").Returns("Prone Target: {0:+#;-#;0}");
    }

    [Fact]
    public void Render_WithNegativeValue_ShouldFormatWithMinusSign()
    {
        // Arrange
        var sut = new ProneTargetModifier
        {
            Value = -2
        };

        // Act
        var result = sut.Render(_localizationService);

        // Assert
        result.ShouldBe("Prone Target: -2");
        _localizationService.Received(1).GetString("Modifier_ProneTarget");
    }

    [Fact]
    public void Render_WithPositiveValue_ShouldFormatWithPlusSign()
    {
        // Arrange
        var sut = new ProneTargetModifier
        {
            Value = 1
        };

        // Act
        var result = sut.Render(_localizationService);

        // Assert
        result.ShouldBe("Prone Target: +1");
        _localizationService.Received(1).GetString("Modifier_ProneTarget");
    }
}
