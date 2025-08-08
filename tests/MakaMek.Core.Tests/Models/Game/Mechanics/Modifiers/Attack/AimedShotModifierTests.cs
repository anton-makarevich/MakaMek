using NSubstitute;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Attack;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Services.Localization;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Mechanics.Modifiers.Attack;

public class AimedShotModifierTests
{
    private readonly ILocalizationService _localizationService = Substitute.For<ILocalizationService>();

    [Theory]
    [InlineData(PartLocation.Head, 3)]
    [InlineData(PartLocation.CenterTorso, -4)]
    [InlineData(PartLocation.LeftArm, -4)]
    [InlineData(PartLocation.RightArm, -4)]
    [InlineData(PartLocation.LeftTorso, -4)]
    [InlineData(PartLocation.RightTorso, -4)]
    [InlineData(PartLocation.LeftLeg, -4)]
    [InlineData(PartLocation.RightLeg, -4)]
    public void Create_ShouldReturnCorrectModifierValue(PartLocation targetLocation, int expectedValue)
    {
        // Act
        var result = AimedShotModifier.Create(targetLocation);

        // Assert
        result.Value.ShouldBe(expectedValue);
        result.TargetLocation.ShouldBe(targetLocation);
    }

    [Fact]
    public void Create_WithHeadTarget_ShouldReturnPositiveModifier()
    {
        // Act
        var result = AimedShotModifier.Create(PartLocation.Head);

        // Assert
        result.Value.ShouldBe(3);
        result.TargetLocation.ShouldBe(PartLocation.Head);
    }

    [Theory]
    [InlineData(PartLocation.CenterTorso)]
    [InlineData(PartLocation.LeftArm)]
    [InlineData(PartLocation.RightArm)]
    [InlineData(PartLocation.LeftTorso)]
    [InlineData(PartLocation.RightTorso)]
    [InlineData(PartLocation.LeftLeg)]
    [InlineData(PartLocation.RightLeg)]
    public void Create_WithBodyPartTarget_ShouldReturnNegativeModifier(PartLocation targetLocation)
    {
        // Act
        var result = AimedShotModifier.Create(targetLocation);

        // Assert
        result.Value.ShouldBe(-4);
        result.TargetLocation.ShouldBe(targetLocation);
    }

    [Fact]
    public void Render_WithHeadTarget_ShouldUseHeadTemplate()
    {
        // Arrange
        var sut = AimedShotModifier.Create(PartLocation.Head);
        _localizationService.GetString("MechPart_Head").Returns("Head");
        _localizationService.GetString("Modifier_AimedShotHead").Returns("Aimed Shot ({0}): +{1}");

        // Act
        var result = sut.Render(_localizationService);

        // Assert
        result.ShouldBe("Aimed Shot (Head): +3");
        _localizationService.Received(1).GetString("MechPart_Head");
        _localizationService.Received(1).GetString("Modifier_AimedShotHead");
    }

    [Fact]
    public void Render_WithBodyPartTarget_ShouldUseBodyPartTemplate()
    {
        // Arrange
        var sut = AimedShotModifier.Create(PartLocation.CenterTorso);
        _localizationService.GetString("MechPart_CenterTorso").Returns("Center Torso");
        _localizationService.GetString("Modifier_AimedShotBodyPart").Returns("Aimed Shot ({0}): {1}");

        // Act
        var result = sut.Render(_localizationService);

        // Assert
        result.ShouldBe("Aimed Shot (Center Torso): -4");
        _localizationService.Received(1).GetString("MechPart_CenterTorso");
        _localizationService.Received(1).GetString("Modifier_AimedShotBodyPart");
    }

    [Theory]
    [InlineData(PartLocation.LeftArm, "Left Arm")]
    [InlineData(PartLocation.RightLeg, "Right Leg")]
    [InlineData(PartLocation.LeftTorso, "Left Torso")]
    public void Render_WithDifferentBodyParts_ShouldFormatCorrectly(PartLocation targetLocation, string partName)
    {
        // Arrange
        var sut = AimedShotModifier.Create(targetLocation);
        _localizationService.GetString($"MechPart_{targetLocation}").Returns(partName);
        _localizationService.GetString("Modifier_AimedShotBodyPart").Returns("Aimed Shot ({0}): {1}");

        // Act
        var result = sut.Render(_localizationService);

        // Assert
        result.ShouldBe($"Aimed Shot ({partName}): -4");
        _localizationService.Received(1).GetString($"MechPart_{targetLocation}");
        _localizationService.Received(1).GetString("Modifier_AimedShotBodyPart");
    }

    [Fact]
    public void AimedShotModifier_ShouldHaveCorrectProperties()
    {
        // Arrange & Act
        var sut = new AimedShotModifier
        {
            Value = -4,
            TargetLocation = PartLocation.LeftArm
        };

        // Assert
        sut.Value.ShouldBe(-4);
        sut.TargetLocation.ShouldBe(PartLocation.LeftArm);
    }
}
