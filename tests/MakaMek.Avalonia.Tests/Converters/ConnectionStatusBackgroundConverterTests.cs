using System.Globalization;
using Avalonia.Media;
using NSubstitute;
using Sanet.MakaMek.Avalonia.Converters;
using Sanet.MakaMek.Avalonia.Controls.Services;
using Sanet.MakaMek.Core.Services.Transport;
using Shouldly;

namespace MakaMek.Avalonia.Tests.Converters;

public class ConnectionStatusBackgroundConverterTests : IDisposable
{
    private readonly IAvaloniaResourcesLocator _resourcesLocator;
    private readonly ConnectionStatusBackgroundConverter _sut;

    public ConnectionStatusBackgroundConverterTests()
    {
        _resourcesLocator = Substitute.For<IAvaloniaResourcesLocator>();
        ConnectionStatusBackgroundConverter.Initialize(_resourcesLocator);
        _sut = new ConnectionStatusBackgroundConverter();
    }

    [Theory]
    [InlineData(ConnectionStatus.Connected, "SuccessBrush")]
    [InlineData(ConnectionStatus.Connecting, "InfoBrush")]
    [InlineData(ConnectionStatus.Reconnecting, "InfoBrush")]
    [InlineData(ConnectionStatus.Disconnected, "ErrorBrush")]
    [InlineData(ConnectionStatus.Closed, "ErrorBrush")]
    public void Convert_ReturnsResourceBrush(ConnectionStatus status, string resource)
    {
        // Arrange
        var brush = new SolidColorBrush(Colors.LightGreen);
        _resourcesLocator.TryFindResource(resource).Returns(brush);

        // Act
        var result = _sut.Convert(status, typeof(IBrush), null, CultureInfo.InvariantCulture) as SolidColorBrush;

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBe(brush);
    }

    [Fact]
    public void Convert_Connected_ReturnsDefaultWhenResourceNotFound()
    {
        // Arrange
        _resourcesLocator.TryFindResource("SuccessBrush").Returns(null!);

        // Act
        var result = _sut.Convert(ConnectionStatus.Connected, typeof(IBrush), null, CultureInfo.InvariantCulture) as SolidColorBrush;

        // Assert
        result.ShouldNotBeNull();
        result.Color.ShouldBe(Colors.Green);
    }

    [Theory]
    [InlineData(ConnectionStatus.Connecting)]
    [InlineData(ConnectionStatus.Reconnecting)]
    public void Convert_ReturnsDefaultWhenResourceNotFound(ConnectionStatus status)
    {
        // Arrange
        _resourcesLocator.TryFindResource("InfoBrush").Returns(null!);

        // Act
        var result = _sut.Convert(status, typeof(IBrush), null, CultureInfo.InvariantCulture) as SolidColorBrush;

        // Assert
        result.ShouldNotBeNull();
        result.Color.ShouldBe(Colors.DodgerBlue);
    }

    [Theory]
    [InlineData(ConnectionStatus.Disconnected)]
    [InlineData(ConnectionStatus.Closed)]
    public void Convert_ReturnsErrorDefaultWhenResourceNotFound(ConnectionStatus status)
    {
        // Arrange
        _resourcesLocator.TryFindResource("ErrorBrush").Returns(null!);

        // Act
        var result = _sut.Convert(status, typeof(IBrush), null, CultureInfo.InvariantCulture) as SolidColorBrush;

        // Assert
        result.ShouldNotBeNull();
        result.Color.ShouldBe(Colors.Red);
    }

    [Fact]
    public void Convert_ReturnsDefaultWhenLocatorNotInitialized()
    {
        // Arrange
        ConnectionStatusBackgroundConverter.Initialize(null!);
        var sut = new ConnectionStatusBackgroundConverter();

        // Act
        var result = sut.Convert(ConnectionStatus.Connected, typeof(IBrush), null, CultureInfo.InvariantCulture) as SolidColorBrush;

        // Assert
        result.ShouldNotBeNull();
        result.Color.ShouldBe(Colors.Green);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not a status")]
    [InlineData(123)]
    [InlineData(true)]
    public void Convert_InvalidInput_ReturnsDefaultRed(object? invalidInput)
    {
        // Arrange
        _resourcesLocator.TryFindResource("ErrorBrush").Returns(null!);

        // Act
        var result = _sut.Convert(invalidInput, typeof(IBrush), null, CultureInfo.InvariantCulture) as SolidColorBrush;

        // Assert
        result.ShouldNotBeNull();
        result.Color.ShouldBe(Colors.Red);
    }

    [Fact]
    public void Convert_InvalidTargetType_ReturnsTransparent()
    {
        // Act
        var result = _sut.Convert(ConnectionStatus.Connected, typeof(string), null, CultureInfo.InvariantCulture) as SolidColorBrush;

        // Assert
        result.ShouldNotBeNull();
        result.Color.ShouldBe(Colors.Transparent);
    }

    [Fact]
    public void ConvertBack_ThrowsNotImplementedException()
    {
        // Act & Assert
        Should.Throw<NotImplementedException>(() =>
            _sut.ConvertBack(null, typeof(object), null, CultureInfo.InvariantCulture));
    }

    public void Dispose()
    {
        ConnectionStatusBackgroundConverter.Initialize(null!);
    }
}