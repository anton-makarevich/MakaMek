using NSubstitute;
using Sanet.MakaMek.Core.Models.Game.Combat.Modifiers;
using Sanet.MakaMek.Core.Models.Game.Combat.Modifiers.Attack;
using Sanet.MakaMek.Core.Services.Localization;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Combat.Modifiers;

public class GunneryRollModifierTests
{
    private readonly ILocalizationService _localizationService = Substitute.For<ILocalizationService>();

    [Fact]
    public void Format_ShouldFormatCorrectly()
    {
        // Arrange
        var modifier = new GunneryRollModifier
        {
            Value = 4
        };
        _localizationService.GetString("Modifier_GunnerySkill").Returns("Gunnery Skill: +{0}");

        // Act
        var result = modifier.Format(_localizationService);

        // Assert
        result.ShouldBe("Gunnery Skill: +4");
        _localizationService.Received(1).GetString("Modifier_GunnerySkill");
    }
}
