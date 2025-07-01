using NSubstitute;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.PilotingSkill;
using Sanet.MakaMek.Core.Services.Localization;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Mechanics.Modifiers.PilotingSkill;

public class HipActuatorHitModifierTests
{
    private readonly ILocalizationService _localizationService = Substitute.For<ILocalizationService>();

    [Fact]
    public void Render_ShouldFormatCorrectly()
    {
        // Arrange
        var sut = new HipActuatorHitModifier
        {
            Value = 2
        };
        _localizationService.GetString("Modifier_HipActuatorHit").Returns("Hip Actuator Hit: +{0}");

        // Act
        var result = sut.Render(_localizationService);

        // Assert
        result.ShouldBe("Hip Actuator Hit: +2");
        _localizationService.Received(1).GetString("Modifier_HipActuatorHit");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Value_ShouldBeSetCorrectly(int expectedValue)
    {
        // Arrange & Act
        var sut = new HipActuatorHitModifier
        {
            Value = expectedValue
        };

        // Assert
        sut.Value.ShouldBe(expectedValue);
    }
}
