using System.Globalization;
using Avalonia.Media;
using Sanet.MakaMek.Avalonia.Converters;
using Sanet.MakaMek.Avalonia.Utils;
using Sanet.MakaMek.Core.Events;
using Shouldly;
using NSubstitute;

namespace MakaMek.Avalonia.Tests.Converters;

public class EventTypeToBackgroundConverterTests
{
    private readonly IAvaloniaResourcesLocator _resourcesLocator;
    private readonly EventTypeToBackgroundConverter _sut;

    public EventTypeToBackgroundConverterTests()
    {
        _resourcesLocator = Substitute.For<IAvaloniaResourcesLocator>();
        _sut = new EventTypeToBackgroundConverter(_resourcesLocator);
    }

    [Theory]
    [InlineData(UiEventType.ArmorDamage)]
    [InlineData(UiEventType.StructureDamage)]
    [InlineData(UiEventType.Explosion)]
    [InlineData(UiEventType.CriticalHit)]
    [InlineData(UiEventType.ComponentDestroyed)]
    [InlineData(UiEventType.LocationDestroyed)]
    [InlineData(UiEventType.UnitDestroyed)]
    public void Convert_ValidEventType_ReturnsBrush(UiEventType eventType)
    {
        // Act
        var result = _sut.Convert(eventType, typeof(IBrush), null, CultureInfo.InvariantCulture) as SolidColorBrush;

        // Assert
        result.ShouldNotBeNull();
    }

    [Fact]
    public void Convert_ArmorDamage_ReturnsMechArmorBrush()
    {
        // Arrange
        var resourceBrush = new SolidColorBrush(Colors.Blue);
        _resourcesLocator.TryFindResource("MechArmorBrush").Returns(resourceBrush);

        // Act
        var result = _sut.Convert(UiEventType.ArmorDamage, typeof(IBrush), null, CultureInfo.InvariantCulture) as SolidColorBrush;

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBe(resourceBrush);
    }

    [Fact]
    public void Convert_ArmorDamage_ReturnsDefaultWhenResourceNotFound()
    {
        // Arrange
        var expectedBrush = new SolidColorBrush(Colors.LightBlue);
        _resourcesLocator.TryFindResource("MechArmorBrush").Returns(null);

        // Act
        var result = _sut.Convert(UiEventType.ArmorDamage, typeof(IBrush), null, CultureInfo.InvariantCulture) as SolidColorBrush;

        // Assert
        result.ShouldNotBeNull();
        result.Color.ShouldBe(expectedBrush.Color);
    }

    [Fact]
    public void Convert_StructureDamage_ReturnsMechStructureBrush()
    {
        // Arrange
        var expectedBrush = new SolidColorBrush(Colors.Orange);
        var resourceBrush = new SolidColorBrush(Colors.DarkOrange);
        _resourcesLocator.TryFindResource("MechStructureBrush").Returns(resourceBrush);

        // Act
        var result = _sut.Convert(UiEventType.StructureDamage, typeof(IBrush), null, CultureInfo.InvariantCulture) as SolidColorBrush;

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBe(resourceBrush);
    }

    [Fact]
    public void Convert_StructureDamage_ReturnsDefaultWhenResourceNotFound()
    {
        // Arrange
        var expectedBrush = new SolidColorBrush(Colors.Orange);
        _resourcesLocator.TryFindResource("MechStructureBrush").Returns(null);

        // Act
        var result = _sut.Convert(UiEventType.StructureDamage, typeof(IBrush), null, CultureInfo.InvariantCulture) as SolidColorBrush;

        // Assert
        result.ShouldNotBeNull();
        result.Color.ShouldBe(expectedBrush.Color);
    }

    [Theory]
    [InlineData(UiEventType.Explosion)]
    [InlineData(UiEventType.CriticalHit)]
    [InlineData(UiEventType.ComponentDestroyed)]
    [InlineData(UiEventType.LocationDestroyed)]
    [InlineData(UiEventType.UnitDestroyed)]
    public void Convert_OtherEventTypes_ReturnsDestroyedColorBrush(UiEventType eventType)
    {
        // Arrange
        var expectedBrush = new SolidColorBrush(Colors.Red);
        var resourceBrush = new SolidColorBrush(Colors.DarkRed);
        _resourcesLocator.TryFindResource("DestroyedColor").Returns(resourceBrush);

        // Act
        var result = _sut.Convert(eventType, typeof(IBrush), null, CultureInfo.InvariantCulture) as SolidColorBrush;

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBe(resourceBrush);
    }

    [Theory]
    [InlineData(UiEventType.Explosion)]
    [InlineData(UiEventType.CriticalHit)]
    [InlineData(UiEventType.ComponentDestroyed)]
    [InlineData(UiEventType.LocationDestroyed)]
    [InlineData(UiEventType.UnitDestroyed)]
    public void Convert_OtherEventTypes_ReturnsDefaultWhenResourceNotFound(UiEventType eventType)
    {
        // Arrange
        var expectedBrush = new SolidColorBrush(Colors.Red);
        _resourcesLocator.TryFindResource("DestroyedColor").Returns(null);

        // Act
        var result = _sut.Convert(eventType, typeof(IBrush), null, CultureInfo.InvariantCulture) as SolidColorBrush;

        // Assert
        result.ShouldNotBeNull();
        result.Color.ShouldBe(expectedBrush.Color);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(123)]
    [InlineData(true)]
    public void Convert_InvalidInput_ReturnsNull(object? invalidInput)
    {
        // Act
        var result = _sut.Convert(invalidInput, typeof(IBrush), null, CultureInfo.InvariantCulture);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void ConvertBack_ThrowsNotImplementedException()
    {
        // Act & Assert
        Should.Throw<NotImplementedException>(() =>
            _sut.ConvertBack(null, typeof(UiEventType), null, CultureInfo.InvariantCulture));
    }
}
