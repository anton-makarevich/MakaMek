using NSubstitute;
using Sanet.MakaMek.Avalonia.Converters;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers;
using Sanet.MakaMek.Core.Services.Localization;
using Shouldly;
using System.Globalization;

namespace MakaMek.Avalonia.Tests.Converters;

public class ModifierToTextConverterTests
{
    private record TestRollModifier : RollModifier
    {
        public override string Render(ILocalizationService localizationService)
        {
            return $"Test Modifier: {Value}";
        }
    }

    private readonly ILocalizationService _localizationService = Substitute.For<ILocalizationService>();
    private readonly ModifierToTextConverter _sut = new();
    private readonly TestRollModifier _testModifier;

    public ModifierToTextConverterTests()
    {
        _testModifier = new TestRollModifier
        {
            Value = 1
        };
        
        // Initialize the static field for testing
        ModifierToTextConverter.Initialize(_localizationService);
    }

    [Fact]
    public void Convert_WithNullModifier_ReturnsEmptyString()
    {
        // Arrange
        RollModifier? modifier = null;

        // Act
        var result = _sut.Convert(modifier, typeof(string), null, CultureInfo.InvariantCulture);

        // Assert
        result.ShouldBeOfType<string>();
        result.ShouldBe(string.Empty);
        _localizationService.DidNotReceive().GetString(Arg.Any<string>());
    }

    [Fact]
    public void Convert_WithRollModifier_ReturnsRenderedText()
    {
        // Arrange
        const string expectedText = "Test Modifier: 1";

        // Act
        var result = _sut.Convert(_testModifier, typeof(string), null, CultureInfo.InvariantCulture);

        // Assert
        result.ShouldBeOfType<string>();
        result.ShouldBe(expectedText);
    }

    [Fact]
    public void Convert_WithNonRollModifier_ReturnsEmptyString()
    {
        // Arrange
        var notARollModifier = new object();

        // Act
        var result = _sut.Convert(notARollModifier, typeof(string), null, CultureInfo.InvariantCulture);

        // Assert
        result.ShouldBeOfType<string>();
        result.ShouldBe(string.Empty);
    }

    [Fact]
    public void ConvertBack_ThrowsNotSupportedException()
    {
        // Arrange
        object? value = null;

        // Act & Assert
        Assert.Throws<NotSupportedException>(() => 
            _sut.ConvertBack(value, typeof(object), null, CultureInfo.InvariantCulture));
    }
}
