using NSubstitute;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.PilotingSkill;
using Sanet.MakaMek.Core.Services.Localization;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Mechanics.Modifiers.PilotingSkill;

public class FootActuatorHitModifierTests
{
    private readonly ILocalizationService _localizationService = Substitute.For<ILocalizationService>();

    [Fact]
    public void Render_ShouldFormatCorrectly()
    {
        // Arrange
        var sut = new FootActuatorHitModifier
        {
            Value = 1
        };
        _localizationService.GetString("Modifier_FootActuatorHit").Returns("Foot Actuator Hit: +{0}");

        // Act
        var result = sut.Render(_localizationService);

        // Assert
        result.ShouldBe("Foot Actuator Hit: +1");
        _localizationService.Received(1).GetString("Modifier_FootActuatorHit");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Value_ShouldBeSetCorrectly(int expectedValue)
    {
        // Arrange & Act
        var sut = new FootActuatorHitModifier
        {
            Value = expectedValue
        };

        // Assert
        sut.Value.ShouldBe(expectedValue);
    }
}
