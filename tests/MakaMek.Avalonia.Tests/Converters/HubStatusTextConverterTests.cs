using System.Globalization;
using NSubstitute;
using Sanet.MakaMek.Avalonia.Converters;
using Sanet.Transport.SignalR.Client.Relay;
using Sanet.MakaMek.Localization;
using Shouldly;

namespace MakaMek.Avalonia.Tests.Converters;

public class HubStatusTextConverterTests : IDisposable
{
    private readonly ILocalizationService _localizationService;
    private readonly HubStatusTextConverter _sut;

    public HubStatusTextConverterTests()
    {
        _localizationService = Substitute.For<ILocalizationService>();
        HubStatusTextConverter.Initialize(_localizationService);
        _sut = new HubStatusTextConverter();
    }

    [Fact]
    public void Convert_Online_ReturnsLocalizedString()
    {
        // Arrange
        const string expectedText = "Online";
        _localizationService.GetString("Hub_Status_Online").Returns(expectedText);

        // Act
        var result = _sut.Convert(HubStatus.Online, typeof(string), null, CultureInfo.InvariantCulture);

        // Assert
        result.ShouldBeOfType<string>();
        result.ShouldBe(expectedText);
        _localizationService.Received(1).GetString("Hub_Status_Online");
    }

    [Fact]
    public void Convert_Online_ReturnsDefaultWhenServiceNotInitialized()
    {
        // Arrange
        HubStatusTextConverter.Initialize(null!);
        var sut = new HubStatusTextConverter();

        // Act
        var result = sut.Convert(HubStatus.Online, typeof(string), null, CultureInfo.InvariantCulture);

        // Assert
        result.ShouldBeOfType<string>();
        result.ShouldBe("Online");
    }

    [Fact]
    public void Convert_Offline_ReturnsLocalizedString()
    {
        // Arrange
        const string expectedText = "Offline";
        _localizationService.GetString("Hub_Status_Offline").Returns(expectedText);

        // Act
        var result = _sut.Convert(HubStatus.Offline, typeof(string), null, CultureInfo.InvariantCulture);

        // Assert
        result.ShouldBeOfType<string>();
        result.ShouldBe(expectedText);
        _localizationService.Received(1).GetString("Hub_Status_Offline");
    }

    [Fact]
    public void Convert_Offline_ReturnsDefaultWhenServiceNotInitialized()
    {
        // Arrange
        HubStatusTextConverter.Initialize(null!);
        var sut = new HubStatusTextConverter();

        // Act
        var result = sut.Convert(HubStatus.Offline, typeof(string), null, CultureInfo.InvariantCulture);

        // Assert
        result.ShouldBeOfType<string>();
        result.ShouldBe("Offline");
    }

    [Fact]
    public void Convert_Checking_ReturnsLocalizedString()
    {
        // Arrange
        const string expectedText = "Checking...";
        _localizationService.GetString("Hub_Status_Checking").Returns(expectedText);

        // Act
        var result = _sut.Convert(HubStatus.Checking, typeof(string), null, CultureInfo.InvariantCulture);

        // Assert
        result.ShouldBeOfType<string>();
        result.ShouldBe(expectedText);
        _localizationService.Received(1).GetString("Hub_Status_Checking");
    }

    [Fact]
    public void Convert_Checking_ReturnsDefaultWhenServiceNotInitialized()
    {
        // Arrange
        HubStatusTextConverter.Initialize(null!);
        var sut = new HubStatusTextConverter();

        // Act
        var result = sut.Convert(HubStatus.Checking, typeof(string), null, CultureInfo.InvariantCulture);

        // Assert
        result.ShouldBeOfType<string>();
        result.ShouldBe("Checking...");
    }

    [Fact]
    public void Convert_Unknown_ReturnsLocalizedString()
    {
        // Arrange
        const string expectedText = "Unknown";
        _localizationService.GetString("Hub_Status_Unknown").Returns(expectedText);

        // Act
        var result = _sut.Convert(HubStatus.Unknown, typeof(string), null, CultureInfo.InvariantCulture);

        // Assert
        result.ShouldBeOfType<string>();
        result.ShouldBe(expectedText);
        _localizationService.Received(1).GetString("Hub_Status_Unknown");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not a status")]
    [InlineData(123)]
    [InlineData(true)]
    public void Convert_InvalidInput_ReturnsUnknownString(object? invalidInput)
    {
        // Arrange
        const string expectedText = "Unknown";
        _localizationService.GetString("Hub_Status_Unknown").Returns(expectedText);

        // Act
        var result = _sut.Convert(invalidInput, typeof(string), null, CultureInfo.InvariantCulture);

        // Assert
        result.ShouldBeOfType<string>();
        result.ShouldBe(expectedText);
        _localizationService.Received(1).GetString("Hub_Status_Unknown");
    }

    [Fact]
    public void Convert_InvalidInput_ReturnsDefaultWhenServiceNotInitialized()
    {
        // Arrange
        HubStatusTextConverter.Initialize(null!);
        var sut = new HubStatusTextConverter();

        // Act
        var result = sut.Convert(null, typeof(string), null, CultureInfo.InvariantCulture);

        // Assert
        result.ShouldBeOfType<string>();
        result.ShouldBe("Unknown");
    }

    [Fact]
    public void ConvertBack_ThrowsNotImplementedException()
    {
        // Act & Assert
        Should.Throw<NotImplementedException>(() =>
            _sut.ConvertBack("Online", typeof(HubStatus), null, CultureInfo.InvariantCulture));
    }

    public void Dispose()
    {
        HubStatusTextConverter.Initialize(null!);
    }
}
