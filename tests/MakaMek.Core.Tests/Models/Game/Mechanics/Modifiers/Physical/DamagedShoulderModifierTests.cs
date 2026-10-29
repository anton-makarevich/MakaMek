using NSubstitute;
using Sanet.Localization;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Physical;
using Sanet.MakaMek.TestHelpers;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Mechanics.Modifiers.Physical;

public class DamagedShoulderModifierTests
{
    private readonly ILocalizationService _localizationService = Substitute.For<ILocalizationService>();

    [Theory]
    [InlineData(1, 2)]
    [InlineData(2, 4)]
    public void Render_ShouldFormatTheDamagedShoulderCountAndValue(int damagedShoulders, int value)
    {
        // Arrange
        var sut = new DamagedShoulderModifier
        {
            DamagedShoulders = damagedShoulders,
            Value = value
        };
        _localizationService.GetString("Modifier_DamagedShoulder").Returns("Damaged Shoulder ({0}): +{1}");

        // Act
        var result = sut.Render(_localizationService);

        // Assert
        result.ShouldBe($"Damaged Shoulder ({damagedShoulders}): +{value}");
        _localizationService.Received(1).GetString("Modifier_DamagedShoulder");
    }

    [Fact]
    public void Render_WithRealResources_FormatsBothPlaceholders()
    {
        // Arrange
        var sut = new DamagedShoulderModifier
        {
            DamagedShoulders = 2,
            Value = 4
        };

        // Act
        var result = sut.Render(TestLocalizationService.Create());

        // Assert
        result.ShouldBe("Damaged Shoulder (2): +4");
    }
}
