using NSubstitute;
using Sanet.MakaMek.Core.Events;
using Sanet.MakaMek.Core.Services.Localization;
using Sanet.MakaMek.Core.ViewModels.Wrappers;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.ViewModels.Wrappers;

public class UiEventViewModelTests
{
    private readonly ILocalizationService _localizationService= Substitute.For<ILocalizationService>();

    [Fact]
    public void Constructor_SetsProperties_Correctly()
    {
        // Arrange
        var uiEvent = new UiEvent(UiEventType.ArmorDamage, "5");

        // Act
        var sut = new UiEventViewModel(uiEvent, _localizationService);

        // Assert
        sut.Type.ShouldBe(UiEventType.ArmorDamage);
    }

    [Theory]
    [InlineData(UiEventType.ArmorDamage)]
    [InlineData(UiEventType.StructureDamage)]
    [InlineData(UiEventType.UnitDestroyed)]
    [InlineData(UiEventType.Explosion)]
    [InlineData(UiEventType.CriticalHit)]
    [InlineData(UiEventType.ComponentDestroyed)]
    [InlineData(UiEventType.LocationDestroyed)]
    public void Type_ReturnsCorrectValue(UiEventType eventType)
    {
        // Arrange
        var uiEvent = new UiEvent(eventType);
        var sut = new UiEventViewModel(uiEvent, _localizationService);

        // Act & Assert
        sut.Type.ShouldBe(eventType);
    }

    [Fact]
    public void FormattedText_UsesLocalizationService_WithCorrectKey()
    {
        // Arrange
        const UiEventType eventType = UiEventType.ArmorDamage;
        var uiEvent = new UiEvent(eventType, "5");
        var expectedKey = $"Events_Unit_{eventType}";
        const string expectedTemplate = "Armor damage: {0}";
        
        _localizationService.GetString(expectedKey).Returns(expectedTemplate);
        
        var sut = new UiEventViewModel(uiEvent, _localizationService);

        // Act
        var result = sut.FormattedText;

        // Assert
        result.ShouldBe("Armor damage: 5");
        _localizationService.Received(1).GetString(expectedKey);
    }

    [Fact]
    public void FormattedText_HandlesMultipleParameters_Correctly()
    {
        // Arrange
        const UiEventType eventType = UiEventType.CriticalHit;
        var uiEvent = new UiEvent(eventType, "Left Arm", "Medium Laser");
        var expectedKey = $"Events_Unit_{eventType}";
        const string expectedTemplate = "Critical hit on {0}: {1}";
        
        _localizationService.GetString(expectedKey).Returns(expectedTemplate);
        
        var sut = new UiEventViewModel(uiEvent, _localizationService);

        // Act
        var result = sut.FormattedText;

        // Assert
        result.ShouldBe("Critical hit on Left Arm: Medium Laser");
        _localizationService.Received(1).GetString(expectedKey);
    }

    [Fact]
    public void FormattedText_HandlesNoParameters_Correctly()
    {
        // Arrange
        const UiEventType eventType = UiEventType.UnitDestroyed;
        var uiEvent = new UiEvent(eventType);
        var expectedKey = $"Events_Unit_{eventType}";
        const string expectedTemplate = "Unit destroyed!";
        
        _localizationService.GetString(expectedKey).Returns(expectedTemplate);
        
        var sut = new UiEventViewModel(uiEvent, _localizationService);

        // Act
        var result = sut.FormattedText;

        // Assert
        result.ShouldBe("Unit destroyed!");
        _localizationService.Received(1).GetString(expectedKey);
    }
}
