using NSubstitute;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Attack;
using Sanet.MakaMek.Core.Services.Localization;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Mechanics.Modifiers.Attack;

public class ProneAttackerModifierTests
{
    private readonly ILocalizationService _localizationService = Substitute.For<ILocalizationService>();

    [Fact]
    public void Render_ShouldFormatCorrectly()
    {
        // Arrange
        var sut = new ProneAttackerModifier
        {
            Value = 2
        };
        _localizationService.GetString("Modifier_ProneFiring").Returns("Prone Firing: +{0}");

        // Act
        var result = sut.Render(_localizationService);

        // Assert
        result.ShouldBe("Prone Firing: +2");
        _localizationService.Received(1).GetString("Modifier_ProneFiring");
    }
}
