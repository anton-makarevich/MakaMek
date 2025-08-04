using System.Globalization;
using Avalonia.Media;
using NSubstitute;
using Sanet.MakaMek.Avalonia.Converters;
using Sanet.MakaMek.Avalonia.Services;
using Shouldly;

namespace MakaMek.Avalonia.Tests.Converters;

public class ConsciousnessColorConverterTests
{
    private readonly IAvaloniaResourcesLocator _resourcesLocator;
    private readonly ConsciousnessColorConverter _sut;

    public ConsciousnessColorConverterTests()
    {
        _resourcesLocator = Substitute.For<IAvaloniaResourcesLocator>();
        ConsciousnessColorConverter.Initialize(_resourcesLocator);
        _sut = new ConsciousnessColorConverter();
    }

    [Fact]
    public void Convert_ShouldReturnSuccessColor_ForConsciousTrue()
    {
        // Arrange
        var expectedColor = Colors.Green;
        _resourcesLocator.TryFindResource("SuccessColor").Returns(expectedColor);

        // Act
        var result = _sut.Convert(true, typeof(Color), null, CultureInfo.InvariantCulture);

        // Assert
        result.ShouldBe(expectedColor);
    }

    [Fact]
    public void Convert_ShouldReturnErrorColor_ForConsciousFalse()
    {
        // Arrange
        var expectedColor = Colors.Red;
        _resourcesLocator.TryFindResource("ErrorColor").Returns(expectedColor);

        // Act
        var result = _sut.Convert(false, typeof(Color), null, CultureInfo.InvariantCulture);

        // Assert
        result.ShouldBe(expectedColor);
    }

    [Fact]
    public void Convert_ShouldReturnDefault_ForConsciousTrue_WhenResourceNotFound()
    {
        // Arrange
        _resourcesLocator.TryFindResource("SuccessColor").Returns(null);

        // Act
        var result = _sut.Convert(true, typeof(Color), null, CultureInfo.InvariantCulture);

        // Assert
        result.ShouldBe(Colors.Green);
    }
    
    [Fact]
    public void Convert_ShouldReturnDefault_ForConsciousTrue_WhenLocatorNotInitialized()
    {
        // Arrange
        ConsciousnessColorConverter.Initialize(null!);

        // Act
        var result = _sut.Convert(true, typeof(Color), null, CultureInfo.InvariantCulture);

        // Assert
        result.ShouldBe(Colors.Green);
    }

    [Fact]
    public void Convert_ShouldReturnDefault_ForConsciousFalse_WhenResourceNotFound()
    {
        // Arrange
        _resourcesLocator.TryFindResource("ErrorColor").Returns(null);

        // Act
        var result = _sut.Convert(false, typeof(Color), null, CultureInfo.InvariantCulture);

        // Assert
        result.ShouldBe(Colors.Red);
    }
    
    [Fact]
    public void Convert_ShouldReturnDefault_ForConsciousFalse_WhenLocatorNotInitialized()
    {
        // Arrange
        ConsciousnessColorConverter.Initialize(null!);

        // Act
        var result = _sut.Convert(false, typeof(Color), null, CultureInfo.InvariantCulture);

        // Assert
        result.ShouldBe(Colors.Red);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not a boolean")]
    [InlineData(123)]
    [InlineData(45.67)]
    public void Convert_ShouldReturnsWarningColor_ForInvalidInput(object? invalidInput)
    {
        // Arrange
        var expectedColor = Colors.Orange;
        _resourcesLocator.TryFindResource("WarningColor").Returns(expectedColor);

        // Act
        var result = _sut.Convert(invalidInput, typeof(Color), null, CultureInfo.InvariantCulture);

        // Assert
        result.ShouldBe(expectedColor);
    }
    
    [Fact]
    public void Convert_ShouldReturnDefault_ForInvalidInput_WhenLocatorNotInitialized()
    {
        // Arrange
        ConsciousnessColorConverter.Initialize(null!);

        // Act
        var result = _sut.Convert("invalid", typeof(Color), null, CultureInfo.InvariantCulture);

        // Assert
        result.ShouldBe(Colors.Gray);
    }

    [Fact]
    public void Convert_ShouldReturnDefault_ForInvalidInput_WhenResourceNotFound()
    {
        // Arrange
        _resourcesLocator.TryFindResource("WarningColor").Returns(null);

        // Act
        var result = _sut.Convert("invalid", typeof(Color), null, CultureInfo.InvariantCulture);

        // Assert
        result.ShouldBe(Colors.Gray);
    }

    [Fact]
    public void ConvertBack_ShouldThrowNotImplementedException()
    {
        // Act & Assert
        Should.Throw<NotImplementedException>(() =>
            _sut.ConvertBack(Colors.Green, typeof(bool), null, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Constructor_ShouldNotThrow_WithDefaultResourcesLocator()
    {
        // Act & Assert
        Should.NotThrow(() => new ConsciousnessColorConverter());
    }
}
