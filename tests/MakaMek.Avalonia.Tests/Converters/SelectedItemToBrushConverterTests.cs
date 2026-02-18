using System.Globalization;
using Avalonia.Media;
using Sanet.MakaMek.Avalonia.Converters;
using Shouldly;
using NSubstitute;
using Sanet.MakaMek.Avalonia.Services;

namespace MakaMek.Avalonia.Tests.Converters;

public class SelectedItemToBrushConverterTests : IDisposable
{
    private readonly IAvaloniaResourcesLocator _resourcesLocator;
    private readonly ISelectedItemToBrushConverter _sut;

    public SelectedItemToBrushConverterTests()
    {
        _resourcesLocator = Substitute.For<IAvaloniaResourcesLocator>();
        ISelectedItemToBrushConverter.Initialize(_resourcesLocator);
        _sut = new ISelectedItemToBrushConverter();
    }

    [Fact]
    public void Convert_True_ReturnsPrimaryBrush()
    {
        // Arrange
        var primaryBrush = new SolidColorBrush(Colors.Blue);
        _resourcesLocator.TryFindResource("PrimaryBrush").Returns(primaryBrush);

        // Act
        var result = _sut.Convert(true, typeof(IBrush), null, CultureInfo.InvariantCulture);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeSameAs(primaryBrush);
    }

    [Fact]
    public void Convert_False_ReturnsTransparent()
    {
        // Act
        var result = _sut.Convert(false, typeof(IBrush), null, CultureInfo.InvariantCulture);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeSameAs(Brushes.Transparent);
    }

    [Fact]
    public void Convert_True_ReturnsFallbackColorWhenResourceNotFound()
    {
        // Arrange
        _resourcesLocator.TryFindResource("PrimaryBrush").Returns(null);

        // Act
        var result = _sut.Convert(true, typeof(IBrush), null, CultureInfo.InvariantCulture) as SolidColorBrush;

        // Assert
        result.ShouldNotBeNull();
        result.Color.ShouldBe(Color.Parse("#6B8E23")); // Fallback color
    }

    [Fact]
    public void Convert_True_ReturnsFallbackColorWhenLocatorNotInitialized()
    {
        // Arrange
        ISelectedItemToBrushConverter.Initialize(null!);
        var sut = new ISelectedItemToBrushConverter();

        // Act
        var result = sut.Convert(true, typeof(IBrush), null, CultureInfo.InvariantCulture) as SolidColorBrush;

        // Assert
        result.ShouldNotBeNull();
        result.Color.ShouldBe(Color.Parse("#6B8E23")); // Fallback color
    }

    [Fact]
    public void Convert_NullValue_ReturnsTransparent()
    {
        // Act
        var result = _sut.Convert(null, typeof(IBrush), null, CultureInfo.InvariantCulture);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeSameAs(Brushes.Transparent);
    }

    [Fact]
    public void Convert_NonBooleanValue_ReturnsTransparent()
    {
        // Act
        var result = _sut.Convert("not a boolean", typeof(IBrush), null, CultureInfo.InvariantCulture);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeSameAs(Brushes.Transparent);
    }

    [Fact]
    public void Convert_InvalidTargetType_ReturnsTransparent()
    {
        // Act
        var result = _sut.Convert(true, typeof(string), null, CultureInfo.InvariantCulture);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeSameAs(Brushes.Transparent);
    }

    [Fact]
    public void ConvertBack_ThrowsNotSupportedException()
    {
        // Act & Assert
        Should.Throw<NotSupportedException>(() =>
            _sut.ConvertBack(null, typeof(object), null, CultureInfo.InvariantCulture));
    }

    public void Dispose()
    {
        ISelectedItemToBrushConverter.Initialize(null!);
    }
}
