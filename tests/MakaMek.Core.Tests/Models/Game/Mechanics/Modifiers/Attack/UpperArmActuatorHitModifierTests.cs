using NSubstitute;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Attack;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Services.Localization;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Mechanics.Modifiers.Attack;

public class UpperArmActuatorHitModifierTests
{
    private readonly ILocalizationService _localizationService = Substitute.For<ILocalizationService>();

    [Fact]
    public void Render_ShouldFormatCorrectly()
    {
        // Arrange
        var sut = new UpperArmActuatorHitModifier
        {
            Value = 1,
            ArmLocation = PartLocation.LeftArm
        };
        _localizationService.GetString("MechPart_LeftArm").Returns("Left Arm");
        _localizationService.GetString("Modifier_UpperArmActuatorHit")
            .Returns("{0} Upper Arm Actuator Destroyed (+{1} to hit)");

        // Act
        var result = sut.Render(_localizationService);

        // Assert
        result.ShouldBe("Left Arm Upper Arm Actuator Destroyed (+1 to hit)");
        _localizationService.Received(1).GetString("MechPart_LeftArm");
        _localizationService.Received(1).GetString("Modifier_UpperArmActuatorHit");
    }

    [Theory]
    [InlineData(PartLocation.LeftArm)]
    [InlineData(PartLocation.RightArm)]
    public void UpperArmActuatorHitModifier_ShouldHaveCorrectProperties(PartLocation location)
    {
        // Arrange & Act
        var sut = new UpperArmActuatorHitModifier
        {
            Value = 1,
            ArmLocation = location
        };

        // Assert
        sut.Value.ShouldBe(1);
        sut.ArmLocation.ShouldBe(location);
    }
}
