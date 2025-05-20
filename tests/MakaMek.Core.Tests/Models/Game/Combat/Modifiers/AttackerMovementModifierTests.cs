using NSubstitute;
using Sanet.MakaMek.Core.Models.Game.Combat.Modifiers;
using Sanet.MakaMek.Core.Models.Game.Combat.Modifiers.Attack;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Services.Localization;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Combat.Modifiers;

public class AttackerMovementModifierTests
{
    private readonly ILocalizationService _localizationService = Substitute.For<ILocalizationService>();
    private readonly AttackerMovementModifier _sut;

    public AttackerMovementModifierTests()
    {
        _sut = new AttackerMovementModifier
        {
            Value = 2,
            MovementType = MovementType.Run
        };
    }

    [Fact]
    public void Format_ShouldFormatCorrectly()
    {
        // Arrange
        _localizationService.GetString("Modifier_AttackerMovement").Returns("Attacker Movement ({0}): +{1}");

        // Act
        var result = _sut.Format(_localizationService);

        // Assert
        result.ShouldBe("Attacker Movement (Run): +2");
        _localizationService.Received(1).GetString("Modifier_AttackerMovement");
    }
}
