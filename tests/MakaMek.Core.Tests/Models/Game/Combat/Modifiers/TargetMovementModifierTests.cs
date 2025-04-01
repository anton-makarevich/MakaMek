using NSubstitute;
using Sanet.MakaMek.Core.Models.Game.Combat.Modifiers;
using Sanet.MakaMek.Core.Services.Localization;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Combat.Modifiers;

public class TargetMovementModifierTests
{
    private readonly ILocalizationService _localizationService = Substitute.For<ILocalizationService>();

    [Fact]
    public void Format_ShouldFormatCorrectly()
    {
        // Arrange
        var modifier = new TargetMovementModifier
        {
            Value = 3,
            HexesMoved = 5
        };
        _localizationService.GetString("Modifier_TargetMovement").Returns("Target Movement ({0} hexes): +{1}");

        // Act
        var result = modifier.Format(_localizationService);

        // Assert
        result.ShouldBe("Target Movement (5 hexes): +3");
        _localizationService.Received(1).GetString("Modifier_TargetMovement");
    }
}
