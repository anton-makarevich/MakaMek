using NSubstitute;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Attack;
using Sanet.MakaMek.Core.Services.Localization;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Mechanics.Modifiers.Attack;

public class HeatRollModifierTests
{
    private readonly ILocalizationService _localizationService = Substitute.For<ILocalizationService>();

    [Fact]
    public void Render_ShouldFormatCorrectly()
    {
        // Arrange
        var sut = new HeatRollModifier
        {
            Value = 2,
            HeatLevel = 15
        };
        _localizationService.GetString("Modifier_Heat").Returns("Heat Level ({0}): {1}");

        // Act
        var result = sut.Render(_localizationService);

        // Assert
        result.ShouldBe("Heat Level (15): 2");
        _localizationService.Received(1).GetString("Modifier_Heat");
    }
}
