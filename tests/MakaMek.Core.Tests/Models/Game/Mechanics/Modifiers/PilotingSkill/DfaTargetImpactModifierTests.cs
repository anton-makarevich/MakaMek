using NSubstitute;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.PilotingSkill;
using Sanet.Localization;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Mechanics.Modifiers.PilotingSkill;

public class DfaTargetImpactModifierTests
{
    private readonly ILocalizationService _localizationService = Substitute.For<ILocalizationService>();

    public DfaTargetImpactModifierTests()
    {
        _localizationService.GetString("Modifier_DfaTargetImpact").Returns("DFA Target Impact: +{0}");
    }

    [Fact]
    public void Render_ShouldFormatTheModifier()
    {
        // Arrange
        var sut = new DfaTargetImpactModifier { Value = 2 };

        // Act
        var result = sut.Render(_localizationService);

        // Assert
        result.ShouldBe("DFA Target Impact: +2");
        _localizationService.Received(1).GetString("Modifier_DfaTargetImpact");
    }
}
