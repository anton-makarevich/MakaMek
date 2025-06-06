using NSubstitute;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Attack;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;
using Sanet.MakaMek.Core.Services.Localization;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Mechanics.Modifiers.Attack;

public class RangeRollModifierTests
{
    private readonly ILocalizationService _localizationService = Substitute.For<ILocalizationService>();

    [Fact]
    public void Render_ShouldFormatCorrectly()
    {
        // Arrange
        var sut = new RangeRollModifier
        {
            Value = 2,
            Range = WeaponRange.Medium,
            Distance = 7,
            WeaponName = "Medium Laser"
        };
        _localizationService.GetString("Modifier_Range").Returns("{0} at {1} hexes ({2} range): +{3}");

        // Act
        var result = sut.Render(_localizationService);

        // Assert
        result.ShouldBe("Medium Laser at 7 hexes (Medium range): +2");
        _localizationService.Received(1).GetString("Modifier_Range");
    }
}
