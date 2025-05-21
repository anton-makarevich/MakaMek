using NSubstitute;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.PilotingSkill;
using Sanet.MakaMek.Core.Services.Localization;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Mechanics.Modifiers.PilotingSkill;

public class DamagedGyroModifierTests
{
    private readonly ILocalizationService _localizationService = Substitute.For<ILocalizationService>();

    [Fact]
    public void Format_ShouldFormatCorrectly()
    {
        // Arrange
        var modifier = new DamagedGyroModifier
        {
            Value = 3,
            HitsCount = 1
        };
        _localizationService.GetString("Modifier_DamagedGyro").Returns("Damaged Gyro");
        _localizationService.GetString("Hits").Returns("Hits");

        // Act
        var result = modifier.Format(_localizationService);

        // Assert
        result.ShouldBe("3 (Damaged Gyro 1 Hits)");
        _localizationService.Received(1).GetString("Modifier_DamagedGyro");
        _localizationService.Received(1).GetString("Hits");
    }

    [Theory]
    [InlineData(1, 3)]
    [InlineData(2, 6)]
    public void Value_ShouldReflectHitsCount(int hitsCount, int expectedValue)
    {
        // Arrange & Act
        var modifier = new DamagedGyroModifier
        {
            Value = expectedValue,
            HitsCount = hitsCount
        };

        // Assert
        modifier.Value.ShouldBe(expectedValue);
        modifier.HitsCount.ShouldBe(hitsCount);
    }
}
