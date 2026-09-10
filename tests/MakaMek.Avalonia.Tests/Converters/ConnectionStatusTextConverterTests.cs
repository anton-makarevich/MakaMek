using System.Globalization;
using NSubstitute;
using Sanet.MakaMek.Avalonia.Converters;
using Sanet.MakaMek.Core.Services.Transport;
using Sanet.MakaMek.Localization;
using Shouldly;

namespace MakaMek.Avalonia.Tests.Converters;

public class ConnectionStatusTextConverterTests : IDisposable
{
    private readonly ILocalizationService _localizationService;
    private readonly ConnectionStatusTextConverter _sut;

    public ConnectionStatusTextConverterTests()
    {
        _localizationService = Substitute.For<ILocalizationService>();
        ConnectionStatusTextConverter.Initialize(_localizationService);
        _sut = new ConnectionStatusTextConverter();
    }

    [Theory]
    [InlineData(ConnectionStatus.NotConnected, "Connection_Status_NotConnected")]
    [InlineData(ConnectionStatus.Connecting, "Connection_Status_Connecting")]
    [InlineData(ConnectionStatus.Connected, "Connection_Status_Connected")]
    [InlineData(ConnectionStatus.Reconnecting, "Connection_Status_Reconnecting")]
    [InlineData(ConnectionStatus.Disconnected, "Connection_Status_Disconnected")]
    [InlineData(ConnectionStatus.Closed, "Connection_Status_Closed")]
    public void Convert_ReturnsLocalizedString(ConnectionStatus status, string key)
    {
        // Arrange
        const string expectedText = "expected";
        _localizationService.GetString(key).Returns(expectedText);

        // Act
        var result = _sut.Convert(status, typeof(string), null, CultureInfo.InvariantCulture);

        // Assert
        result.ShouldBeOfType<string>();
        result.ShouldBe(expectedText);
        _localizationService.Received(1).GetString(key);
    }

    [Theory]
    [InlineData(ConnectionStatus.NotConnected, "Not connected")]
    [InlineData(ConnectionStatus.Connecting, "Connecting...")]
    [InlineData(ConnectionStatus.Connected, "Connected")]
    [InlineData(ConnectionStatus.Reconnecting, "Reconnecting...")]
    [InlineData(ConnectionStatus.Disconnected, "Disconnected")]
    [InlineData(ConnectionStatus.Closed, "Connection closed")]
    public void Convert_ReturnsDefaultWhenServiceNotInitialized(ConnectionStatus status, string defaultValue)
    {
        // Arrange
        ConnectionStatusTextConverter.Initialize(null!);
        var sut = new ConnectionStatusTextConverter();

        // Act
        var result = sut.Convert(status, typeof(string), null, CultureInfo.InvariantCulture);

        // Assert
        result.ShouldBeOfType<string>();
        result.ShouldBe(defaultValue);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not a status")]
    [InlineData(123)]
    [InlineData(true)]
    public void Convert_InvalidInput_ReturnsDisconnectedString(object? invalidInput)
    {
        // Arrange
        const string expectedText = "Disconnected";
        _localizationService.GetString("Connection_Status_Disconnected").Returns(expectedText);

        // Act
        var result = _sut.Convert(invalidInput, typeof(string), null, CultureInfo.InvariantCulture);

        // Assert
        result.ShouldBeOfType<string>();
        result.ShouldBe(expectedText);
        _localizationService.Received(1).GetString("Connection_Status_Disconnected");
    }

    [Fact]
    public void Convert_InvalidInput_ReturnsDefaultWhenServiceNotInitialized()
    {
        // Arrange
        ConnectionStatusTextConverter.Initialize(null!);
        var sut = new ConnectionStatusTextConverter();

        // Act
        var result = sut.Convert("not a status", typeof(string), null, CultureInfo.InvariantCulture);

        // Assert
        result.ShouldBeOfType<string>();
        result.ShouldBe("Disconnected");
    }

    [Fact]
    public void ConvertBack_ThrowsNotImplementedException()
    {
        // Act & Assert
        Should.Throw<NotImplementedException>(() =>
            _sut.ConvertBack("Connected", typeof(ConnectionStatus), null, CultureInfo.InvariantCulture));
    }

    public void Dispose()
    {
        ConnectionStatusTextConverter.Initialize(null!);
    }
}