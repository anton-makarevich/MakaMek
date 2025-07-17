using NSubstitute;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Attack;
using Sanet.MakaMek.Core.Services.Localization;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Mechanics.Modifiers.Attack;

public class SensorHitModifierTests
{
    private readonly ILocalizationService _localizationService = Substitute.For<ILocalizationService>();

    [Fact]
    public void Render_ShouldFormatCorrectly()
    {
        // Arrange
        var sut = new SensorHitModifier
        {
            Value = 2,
            SensorHits = 1
        };
        _localizationService.GetString("Modifier_SensorHit").Returns("Sensor Hit: +{0}");

        // Act
        var result = sut.Render(_localizationService);

        // Assert
        result.ShouldBe("Sensor Hit: +2");
        _localizationService.Received(1).GetString("Modifier_SensorHit");
    }

    [Theory]
    [InlineData(1, 2)]
    [InlineData(2, 4)]
    [InlineData(3, 6)]
    public void SensorHitModifier_ShouldHaveCorrectProperties(int sensorHits, int expectedValue)
    {
        // Arrange & Act
        var sut = new SensorHitModifier
        {
            Value = expectedValue,
            SensorHits = sensorHits
        };

        // Assert
        sut.Value.ShouldBe(expectedValue);
        sut.SensorHits.ShouldBe(sensorHits);
    }
}
