using NSubstitute;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.PilotingSkill;
using Sanet.MakaMek.Core.Services.Localization;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Mechanics.Modifiers.PilotingSkill;

public class HeavyDamageModifierTests
{
    public HeavyDamageModifierTests()
    {
        _localizationService.GetString("Modifier_HeavyDamage").Returns("Heavy Damage ({0} points): +{1}");
    }
    private readonly ILocalizationService _localizationService = Substitute.For<ILocalizationService>();

    [Theory]
    [InlineData(10, 1, "Heavy Damage (10 points): +1")]
    [InlineData(25, 2, "Heavy Damage (25 points): +2")]
    public void Render_ReturnsCorrectlyFormattedString(int damageTaken, int value, string expectedString)
    {
        // Arrange
        var modifier = new HeavyDamageModifier { DamageTaken = damageTaken, Value = value };

        // Act
        var result = modifier.Render(_localizationService);

        // Assert
        result.ShouldBe(expectedString);
    }
}
