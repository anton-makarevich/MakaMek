using NSubstitute;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Physical;
using Sanet.Localization;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Mechanics.Modifiers.Physical;

public class PhysicalAttackBaseModifierTests
{
    private readonly ILocalizationService _localizationService = Substitute.For<ILocalizationService>();

    [Theory]
    [InlineData(PhysicalAttackType.Punch, 0)]
    [InlineData(PhysicalAttackType.Kick, -2)]
    public void Render_ShouldFormatCorrectly(PhysicalAttackType attackType, int value)
    {
        // Arrange
        var sut = new PhysicalAttackBaseModifier
        {
            Value = value,
            AttackType = attackType
        };
        _localizationService.GetString("Modifier_PhysicalAttackBase").Returns("{0} Base: +{1}");

        // Act
        var result = sut.Render(_localizationService);

        // Assert
        result.ShouldBe($"{attackType} Base: +{value}");
        _localizationService.Received(1).GetString("Modifier_PhysicalAttackBase");
    }
}
