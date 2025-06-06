using NSubstitute;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.PilotingSkill;
using Sanet.MakaMek.Core.Services.Localization;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Mechanics.Modifiers.PilotingSkill;

public class DamagedGyroModifierTests
{
    private readonly ILocalizationService _localizationService = Substitute.For<ILocalizationService>();

    [Fact]
    public void Render_ShouldFormatCorrectly()
    {
        // Arrange
        var sut = new DamagedGyroModifier
        {
            Value = 3,
            HitsCount = 1
        };
        _localizationService.GetString("Modifier_DamagedGyro").Returns("Damaged Gyro ({0} {1}): +{2}");
        _localizationService.GetString("Hits").Returns("Hits");

        // Act
        var result = sut.Render(_localizationService);

        // Assert
        result.ShouldBe("Damaged Gyro (1 Hits): +3");
        _localizationService.Received(1).GetString("Modifier_DamagedGyro");
        _localizationService.Received(1).GetString("Hits");
    }

    [Theory]
    [InlineData(1, 3)]
    [InlineData(2, 6)]
    public void Value_ShouldReflectHitsCount(int hitsCount, int expectedValue)
    {
        // Arrange & Act
        var sut = new DamagedGyroModifier
        {
            Value = expectedValue,
            HitsCount = hitsCount
        };

        // Assert
        sut.Value.ShouldBe(expectedValue);
        sut.HitsCount.ShouldBe(hitsCount);
    }
}
