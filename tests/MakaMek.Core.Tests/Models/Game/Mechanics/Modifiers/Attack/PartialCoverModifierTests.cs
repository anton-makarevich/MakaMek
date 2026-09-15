using NSubstitute;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Attack;
using Sanet.MakaMek.Localization;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Mechanics.Modifiers.Attack;

public class PartialCoverModifierTests
{
    private readonly PartialCoverModifier _sut = new()
    {
        Value = 1
    };
    private readonly ILocalizationService _localizationService = Substitute.For<ILocalizationService>();

    [Fact]
    public void Render_WithValidValue_ShouldFormatCorrectly()
    {
        // Arrange
        const string localizationPattern = "Partial Cover: +{0}";
        _localizationService.GetString("Modifier_PartialCover").Returns(localizationPattern);

        // Act
        var result = _sut.Render(_localizationService);

        // Assert
        result.ShouldBe("Partial Cover: +1");
    }
}
