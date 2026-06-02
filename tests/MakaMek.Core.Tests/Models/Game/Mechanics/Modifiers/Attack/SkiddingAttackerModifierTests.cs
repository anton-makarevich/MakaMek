using NSubstitute;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Attack;
using Sanet.MakaMek.Localization;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Mechanics.Modifiers.Attack;

public class SkiddingAttackerModifierTests
{
    private readonly ILocalizationService _localizationService = Substitute.For<ILocalizationService>();

    [Fact]
    public void Render_ShouldFormatCorrectly()
    {
        // Arrange
        var sut = new SkiddingAttackerModifier
        {
            Value = 1
        };
        _localizationService.GetString("Modifier_SkiddingAttacker").Returns("Skidding Attacker: +{0}");

        // Act
        var result = sut.Render(_localizationService);

        // Assert
        result.ShouldBe("Skidding Attacker: +1");
        _localizationService.Received(1).GetString("Modifier_SkiddingAttacker");
    }
}
