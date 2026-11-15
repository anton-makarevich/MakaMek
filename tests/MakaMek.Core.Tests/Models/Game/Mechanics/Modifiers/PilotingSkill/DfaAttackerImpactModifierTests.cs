using NSubstitute;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.PilotingSkill;
using Sanet.Localization;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Mechanics.Modifiers.PilotingSkill;

public class DfaAttackerImpactModifierTests
{
    private readonly ILocalizationService _localizationService = Substitute.For<ILocalizationService>();

    public DfaAttackerImpactModifierTests()
    {
        _localizationService.GetString("Modifier_DfaAttackerImpact").Returns("DFA Attacker Impact: +{0}");
    }

    [Fact]
    public void Render_ShouldFormatTheModifier()
    {
        // Arrange
        var sut = new DfaAttackerImpactModifier { Value = 4 };

        // Act
        var result = sut.Render(_localizationService);

        // Assert
        result.ShouldBe("DFA Attacker Impact: +4");
        _localizationService.Received(1).GetString("Modifier_DfaAttackerImpact");
    }
}
