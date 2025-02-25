using NSubstitute;
using Sanet.MekForge.Core.Models.Game.Combat.Modifiers;
using Sanet.MekForge.Core.Models.Units.Components.Weapons;
using Sanet.MekForge.Core.Services.Localization;
using Shouldly;

namespace Sanet.MekForge.Core.Tests.Models.Game.Combat.Modifiers;

public class RangeAttackModifierTests
{
    private readonly ILocalizationService _localizationService = Substitute.For<ILocalizationService>();

    [Fact]
    public void Format_ShouldFormatCorrectly()
    {
        // Arrange
        var modifier = new RangeAttackModifier
        {
            Value = 2,
            Range = WeaponRange.Medium,
            Distance = 7,
            WeaponName = "Medium Laser"
        };
        _localizationService.GetString("Modifier_Range").Returns("{0} at {1} hexes ({2} range): {3}");

        // Act
        var result = modifier.Format(_localizationService);

        // Assert
        result.ShouldBe("Medium Laser at 7 hexes (Medium range): 2");
        _localizationService.Received(1).GetString("Modifier_Range");
    }
}
