using NSubstitute;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Physical;
using Sanet.Localization;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Mechanics.Modifiers.Physical;

public class ChargeReplacementTargetModifierTests
{
    private readonly ILocalizationService _localizationService = Substitute.For<ILocalizationService>();

    public ChargeReplacementTargetModifierTests()
    {
        _localizationService.GetString("Modifier_ChargeReplacementTarget")
            .Returns("Replacement Target: +{0}");
    }

    [Fact]
    public void Render_ShouldFormatTheDefaultModifier()
    {
        // Arrange
        var sut = new ChargeReplacementTargetModifier
        {
            Value = ChargeReplacementTargetModifier.DefaultValue
        };

        // Act
        var result = sut.Render(_localizationService);

        // Assert
        result.ShouldBe("Replacement Target: +1");
        _localizationService.Received(1).GetString("Modifier_ChargeReplacementTarget");
    }
}
