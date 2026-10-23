using NSubstitute;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Physical;
using Sanet.Localization;
using Sanet.MakaMek.TestHelpers;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Mechanics.Modifiers.Physical;

public class PhysicalAttackBaseModifierTests
{
    private readonly ILocalizationService _localizationService = Substitute.For<ILocalizationService>();

    [Theory]
    [InlineData(PhysicalAttackType.Punch, "Punch", 0)]
    [InlineData(PhysicalAttackType.Kick, "Kick", -2)]
    public void Render_ShouldFormatCorrectly(PhysicalAttackType attackType, string localizedAttackType, int value)
    {
        // Arrange
        var sut = new PhysicalAttackBaseModifier
        {
            Value = value,
            AttackType = attackType
        };
        _localizationService.GetString("Modifier_PhysicalAttackBase").Returns("{0} Base: {1:+#;-#;0}");
        _localizationService.GetString($"PhysicalAttackType_{attackType}").Returns(localizedAttackType);

        // Act
        var result = sut.Render(_localizationService);

        // Assert
        result.ShouldBe($"{localizedAttackType} Base: {value:+#;-#;0}");
        _localizationService.Received(1).GetString("Modifier_PhysicalAttackBase");
        _localizationService.Received(1).GetString($"PhysicalAttackType_{attackType}");
    }

    [Theory]
    [InlineData(PhysicalAttackType.Punch, "Punch Base: 0")]
    [InlineData(PhysicalAttackType.Kick, "Kick Base: -2")]
    public void Render_WithRealResources_FormatsAttackTypeName(PhysicalAttackType attackType, string expected)
    {
        // Arrange
        var sut = new PhysicalAttackBaseModifier
        {
            Value = attackType == PhysicalAttackType.Kick ? -2 : 0,
            AttackType = attackType
        };

        // Act
        var result = sut.Render(TestLocalizationService.Create());

        // Assert
        result.ShouldBe(expected);
    }
}
