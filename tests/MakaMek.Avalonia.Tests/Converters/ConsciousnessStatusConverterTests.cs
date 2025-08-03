using System.Globalization;
using NSubstitute;
using Sanet.MakaMek.Avalonia.Converters;
using Sanet.MakaMek.Core.Services.Localization;
using Shouldly;

namespace MakaMek.Avalonia.Tests.Converters;

public class ConsciousnessStatusConverterTests
{
    private readonly ILocalizationService _localizationService;
    private readonly ConsciousnessStatusConverter _sut;

    public ConsciousnessStatusConverterTests()
    {
        _localizationService = Substitute.For<ILocalizationService>();
        _sut = new ConsciousnessStatusConverter();
        
        // Initialize the static field for testing
        ConsciousnessStatusConverter.Initialize(_localizationService);
    }

    [Fact]
    public void Convert_ShouldReturnLocalizedConsciousString_ForConsciousTrue()
    {
        // Arrange
        const string expectedText = "CONSCIOUS";
        _localizationService.GetString("Pilot_Status_Conscious").Returns(expectedText);

        // Act
        var result = _sut.Convert(true, typeof(string), null, CultureInfo.InvariantCulture);

        // Assert
        result.ShouldBeOfType<string>();
        result.ShouldBe(expectedText);
        _localizationService.Received(1).GetString("Pilot_Status_Conscious");
    }

    [Fact]
    public void Convert_ShouldReturnLocalizedUnconsciousString_ForConsciousFalse()
    {
        // Arrange
        const string expectedText = "UNCONSCIOUS";
        _localizationService.GetString("Pilot_Status_Unconscious").Returns(expectedText);

        // Act
        var result = _sut.Convert(false, typeof(string), null, CultureInfo.InvariantCulture);

        // Assert
        result.ShouldBeOfType<string>();
        result.ShouldBe(expectedText);
        _localizationService.Received(1).GetString("Pilot_Status_Unconscious");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not a boolean")]
    [InlineData(123)]
    [InlineData(45.67)]
    public void Convert_ShouldReturnLocalizedUnknownString_ForInvalidInput(object? invalidInput)
    {
        // Arrange
        const string expectedText = "UNKNOWN";
        _localizationService.GetString("Pilot_Status_Unknown").Returns(expectedText);

        // Act
        var result = _sut.Convert(invalidInput, typeof(string), null, CultureInfo.InvariantCulture);

        // Assert
        result.ShouldBeOfType<string>();
        result.ShouldBe(expectedText);
        _localizationService.Received(1).GetString("Pilot_Status_Unknown");
    }

    [Fact]
    public void Convert_ShouldReturnUnknownString_WithoutInitialization()
    {
        // Arrange
        ConsciousnessStatusConverter.Initialize(null!);
        const string expectedText = "UNKNOWN";
        _localizationService.GetString("Pilot_Status_Unknown").Returns(expectedText);

        // Act
        var result = _sut.Convert(true, typeof(string), null, CultureInfo.InvariantCulture);

        // Assert
        result.ShouldBeOfType<string>();
        result.ShouldBe(expectedText);
    }

    [Fact]
    public void ConvertBack_ShouldThrowNotImplementedException()
    {
        // Act & Assert
        Should.Throw<NotImplementedException>(() =>
            _sut.ConvertBack("CONSCIOUS", typeof(bool), null, CultureInfo.InvariantCulture));
    }
}
