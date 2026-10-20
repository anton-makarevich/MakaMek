using NSubstitute;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Physical;
using Sanet.Localization;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Mechanics.Modifiers.Physical;

public class PilotingRollModifierTests
{
    private readonly ILocalizationService _localizationService = Substitute.For<ILocalizationService>();

    [Fact]
    public void Render_ShouldFormatCorrectly()
    {
        // Arrange
        var sut = new PilotingRollModifier
        {
            Value = 5
        };
        _localizationService.GetString("Modifier_PilotingSkill").Returns("Piloting Skill: +{0}");

        // Act
        var result = sut.Render(_localizationService);

        // Assert
        result.ShouldBe("Piloting Skill: +5");
        _localizationService.Received(1).GetString("Modifier_PilotingSkill");
    }
}
