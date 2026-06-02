using NSubstitute;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Attack;
using Sanet.MakaMek.Localization;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Mechanics.Modifiers.Attack;

public class SkiddingTargetModifierTests
{
    private readonly ILocalizationService _localizationService = Substitute.For<ILocalizationService>();

    [Fact]
    public void Render_ShouldFormatCorrectly()
    {
        // Arrange
        var sut = new SkiddingTargetModifier
        {
            Value = 2
        };
        _localizationService.GetString("Modifier_SkiddingTarget").Returns("Skidding Target: +{0}");

        // Act
        var result = sut.Render(_localizationService);

        // Assert
        result.ShouldBe("Skidding Target: +2");
        _localizationService.Received(1).GetString("Modifier_SkiddingTarget");
    }
}
