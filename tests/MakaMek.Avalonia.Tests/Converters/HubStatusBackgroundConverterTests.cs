using System.Globalization;
using Avalonia.Media;
using NSubstitute;
using Sanet.MakaMek.Avalonia.Converters;
using Sanet.MakaMek.Avalonia.Controls.Services;
using Sanet.Transport.SignalR.Client.Relay;
using Shouldly;

namespace MakaMek.Avalonia.Tests.Converters;

public class HubStatusBackgroundConverterTests : IDisposable
{
    private readonly IAvaloniaResourcesLocator _resourcesLocator;
    private readonly HubStatusBackgroundConverter _sut;

    public HubStatusBackgroundConverterTests()
    {
        _resourcesLocator = Substitute.For<IAvaloniaResourcesLocator>();
        HubStatusBackgroundConverter.Initialize(_resourcesLocator);
        _sut = new HubStatusBackgroundConverter();
    }

    [Fact]
    public void Convert_Online_ReturnsSuccessBrush()
    {
        // Arrange
        var successBrush = new SolidColorBrush(Colors.LightGreen);
        _resourcesLocator.TryFindResource("SuccessBrush").Returns(successBrush);

        // Act
        var result = _sut.Convert(HubStatus.Online, typeof(IBrush), null, CultureInfo.InvariantCulture) as SolidColorBrush;

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBe(successBrush);
    }

    [Fact]
    public void Convert_Online_ReturnsDefaultWhenResourceNotFound()
    {
        // Arrange
        _resourcesLocator.TryFindResource("SuccessBrush").Returns(null!);

        // Act
        var result = _sut.Convert(HubStatus.Online, typeof(IBrush), null, CultureInfo.InvariantCulture) as SolidColorBrush;

        // Assert
        result.ShouldNotBeNull();
        result.Color.ShouldBe(Colors.Green);
    }

    [Fact]
    public void Convert_Online_ReturnsDefaultWhenLocatorNotInitialized()
    {
        // Arrange
        HubStatusBackgroundConverter.Initialize(null!);
        var sut = new HubStatusBackgroundConverter();

        // Act
        var result = sut.Convert(HubStatus.Online, typeof(IBrush), null, CultureInfo.InvariantCulture) as SolidColorBrush;

        // Assert
        result.ShouldNotBeNull();
        result.Color.ShouldBe(Colors.Green);
    }

    [Fact]
    public void Convert_Offline_ReturnsErrorBrush()
    {
        // Arrange
        var errorBrush = new SolidColorBrush(Colors.IndianRed);
        _resourcesLocator.TryFindResource("ErrorBrush").Returns(errorBrush);

        // Act
        var result = _sut.Convert(HubStatus.Offline, typeof(IBrush), null, CultureInfo.InvariantCulture) as SolidColorBrush;

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBe(errorBrush);
    }

    [Fact]
    public void Convert_Offline_ReturnsDefaultWhenResourceNotFound()
    {
        // Arrange
        _resourcesLocator.TryFindResource("ErrorBrush").Returns(null!);

        // Act
        var result = _sut.Convert(HubStatus.Offline, typeof(IBrush), null, CultureInfo.InvariantCulture) as SolidColorBrush;

        // Assert
        result.ShouldNotBeNull();
        result.Color.ShouldBe(Colors.Red);
    }

    [Fact]
    public void Convert_Offline_ReturnsDefaultWhenLocatorNotInitialized()
    {
        // Arrange
        HubStatusBackgroundConverter.Initialize(null!);
        var sut = new HubStatusBackgroundConverter();

        // Act
        var result = sut.Convert(HubStatus.Offline, typeof(IBrush), null, CultureInfo.InvariantCulture) as SolidColorBrush;

        // Assert
        result.ShouldNotBeNull();
        result.Color.ShouldBe(Colors.Red);
    }

    [Fact]
    public void Convert_Checking_ReturnsInfoBrush()
    {
        // Arrange
        var infoBrush = new SolidColorBrush(Colors.SkyBlue);
        _resourcesLocator.TryFindResource("InfoBrush").Returns(infoBrush);

        // Act
        var result = _sut.Convert(HubStatus.Checking, typeof(IBrush), null, CultureInfo.InvariantCulture) as SolidColorBrush;

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBe(infoBrush);
    }

    [Fact]
    public void Convert_Checking_ReturnsDefaultWhenResourceNotFound()
    {
        // Arrange
        _resourcesLocator.TryFindResource("InfoBrush").Returns(null!);

        // Act
        var result = _sut.Convert(HubStatus.Checking, typeof(IBrush), null, CultureInfo.InvariantCulture) as SolidColorBrush;

        // Assert
        result.ShouldNotBeNull();
        result.Color.ShouldBe(Colors.DodgerBlue);
    }

    [Fact]
    public void Convert_Unknown_ReturnsOverlayTransparentBrush()
    {
        // Arrange
        var overlayBrush = new SolidColorBrush(Colors.DimGray);
        _resourcesLocator.TryFindResource("OverlayTransparentBrush").Returns(overlayBrush);

        // Act
        var result = _sut.Convert(HubStatus.Unknown, typeof(IBrush), null, CultureInfo.InvariantCulture) as SolidColorBrush;

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBe(overlayBrush);
    }

    [Fact]
    public void Convert_Unknown_ReturnsDefaultWhenResourceNotFound()
    {
        // Arrange
        _resourcesLocator.TryFindResource("OverlayTransparentBrush").Returns(null!);

        // Act
        var result = _sut.Convert(HubStatus.Unknown, typeof(IBrush), null, CultureInfo.InvariantCulture) as SolidColorBrush;

        // Assert
        result.ShouldNotBeNull();
        result.Color.ShouldBe(Colors.Gray);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not a status")]
    [InlineData(123)]
    [InlineData(true)]
    public void Convert_InvalidInput_ReturnsDefaultGray(object? invalidInput)
    {
        // Arrange
        _resourcesLocator.TryFindResource("OverlayTransparentBrush").Returns(null!);

        // Act
        var result = _sut.Convert(invalidInput, typeof(IBrush), null, CultureInfo.InvariantCulture) as SolidColorBrush;

        // Assert
        result.ShouldNotBeNull();
        result.Color.ShouldBe(Colors.Gray);
    }

    [Fact]
    public void Convert_InvalidTargetType_ReturnsTransparent()
    {
        // Act
        var result = _sut.Convert(HubStatus.Online, typeof(string), null, CultureInfo.InvariantCulture) as SolidColorBrush;

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
        HubStatusBackgroundConverter.Initialize(null!);
    }
}
