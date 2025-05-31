using NSubstitute;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Attack;
using Sanet.MakaMek.Core.Services.Localization;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Mechanics.Modifiers.Attack;

public class GunneryRollModifierTests
{
    private readonly ILocalizationService _localizationService = Substitute.For<ILocalizationService>();

    [Fact]
    public void Render_ShouldFormatCorrectly()
    {
        // Arrange
        var sut = new GunneryRollModifier
        {
            Value = 4
        };
        _localizationService.GetString("Modifier_GunnerySkill").Returns("Gunnery Skill: +{0}");

        // Act
        var result = sut.Render(_localizationService);

        // Assert
        result.ShouldBe("Gunnery Skill: +4");
        _localizationService.Received(1).GetString("Modifier_GunnerySkill");
    }
}
