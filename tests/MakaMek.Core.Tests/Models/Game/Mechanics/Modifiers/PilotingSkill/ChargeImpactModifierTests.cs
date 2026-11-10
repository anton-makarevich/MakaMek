using NSubstitute;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.PilotingSkill;
using Sanet.Localization;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Mechanics.Modifiers.PilotingSkill;

public class ChargeImpactModifierTests
{
    private readonly ILocalizationService _localizationService = Substitute.For<ILocalizationService>();

    public ChargeImpactModifierTests()
    {
        _localizationService.GetString("Modifier_ChargeImpact").Returns("Charge Impact: +{0}");
    }

    [Fact]
    public void Render_ShouldFormatTheModifier()
    {
        // Arrange
        var sut = new ChargeImpactModifier { Value = 2 };

        // Act
        var result = sut.Render(_localizationService);

        // Assert
        result.ShouldBe("Charge Impact: +2");
        _localizationService.Received(1).GetString("Modifier_ChargeImpact");
    }
}
