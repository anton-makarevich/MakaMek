using NSubstitute;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.PilotingSkill;
using Sanet.MakaMek.Core.Services.Localization;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Mechanics.Modifiers.PilotingSkill;

public class UpperLegActuatorHitModifierTests
{
    private readonly ILocalizationService _localizationService = Substitute.For<ILocalizationService>();

    [Fact]
    public void Render_ShouldFormatCorrectly()
    {
        // Arrange
        var sut = new UpperLegActuatorHitModifier
        {
            Value = 1
        };
        _localizationService.GetString("Modifier_UpperLegActuatorHit").Returns("Upper Leg Actuator Hit: +{0}");

        // Act
        var result = sut.Render(_localizationService);

        // Assert
        result.ShouldBe("Upper Leg Actuator Hit: +1");
        _localizationService.Received(1).GetString("Modifier_UpperLegActuatorHit");
    }
}
