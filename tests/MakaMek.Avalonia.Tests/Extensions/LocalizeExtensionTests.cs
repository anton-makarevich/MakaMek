using NSubstitute;
using Sanet.MakaMek.Avalonia.Extensions;
using Sanet.MakaMek.Localization;
using Shouldly;
using System.Reflection;

namespace MakaMek.Avalonia.Tests.Extensions;

public class LocalizeExtensionTests
{
    private readonly ILocalizationService _localizationService = Substitute.For<ILocalizationService>();
    private readonly LocalizeExtension _sut = new();

    public LocalizeExtensionTests()
    {
        // Reset static field before each test to ensure test isolation
        typeof(LocalizeExtension)
            .GetField("_localizationService", BindingFlags.NonPublic | BindingFlags.Static)
            ?.SetValue(null, null);
    }

    [Fact]
    public void Constructor_WithNoParameters_SetsEmptyKey()
    {
        // Act
        var extension = new LocalizeExtension();

        // Assert
        extension.Key.ShouldBe(string.Empty);
    }

    [Fact]
    public void Constructor_WithKeyParameter_SetsKey()
    {
        // Arrange
        const string expectedKey = "Test_Key";

        // Act
        var extension = new LocalizeExtension(expectedKey);

        // Assert
        extension.Key.ShouldBe(expectedKey);
    }

    [Fact]
    public void ProvideValue_WhenNotInitialized_ReturnsKey()
    {
        // Arrange
        _sut.Key = "Test_Key";

        // Act
        var result = _sut.ProvideValue(Substitute.For<IServiceProvider>());

        // Assert
        result.ShouldBe("Test_Key");
    }

    [Fact]
    public void ProvideValue_WhenInitialized_ReturnsLocalizedString()
    {
        // Arrange
        const string key = "Test_Key";
        const string localizedText = "Localized Text";
        _sut.Key = key;
        LocalizeExtension.Initialize(_localizationService);
        _localizationService.GetString(key).Returns(localizedText);

        // Act
        var result = _sut.ProvideValue(Substitute.For<IServiceProvider>());

        // Assert
        result.ShouldBe(localizedText);
        _localizationService.Received(1).GetString(key);
    }

    [Fact]
    public void ProvideValue_WithEmptyKey_ReturnsEmptyString()
    {
        // Arrange
        _sut.Key = string.Empty;
        LocalizeExtension.Initialize(_localizationService);
        _localizationService.GetString(string.Empty).Returns(string.Empty);

        // Act
        var result = _sut.ProvideValue(Substitute.For<IServiceProvider>());

        // Assert
        result.ShouldBe(string.Empty);
    }

    [Fact]
    public void Initialize_SetsStaticLocalizationService()
    {
        // Arrange
        var newLocalizationService = Substitute.For<ILocalizationService>();
        const string key = "Another_Key";
        const string localizedText = "Another Text";
        _sut.Key = key;
        newLocalizationService.GetString(key).Returns(localizedText);

        // Act
        LocalizeExtension.Initialize(newLocalizationService);
        var result = _sut.ProvideValue(Substitute.For<IServiceProvider>());

        // Assert
        result.ShouldBe(localizedText);
        newLocalizationService.Received(1).GetString(key);
    }

    [Fact]
    public void ProvideValue_AfterReInitialize_UsesNewService()
    {
        // Arrange
        const string key = "Test_Key";
        const string newText = "New Text";
        _sut.Key = key;
        
        var newService = Substitute.For<ILocalizationService>();
        newService.GetString(key).Returns(newText);
        
        // First initialize with original service
        LocalizeExtension.Initialize(_localizationService);
        // Then re-initialize with new service
        LocalizeExtension.Initialize(newService);

        // Act
        var result = _sut.ProvideValue(Substitute.For<IServiceProvider>());

        // Assert
        result.ShouldBe(newText);
        _localizationService.DidNotReceive().GetString(Arg.Any<string>());
        newService.Received(1).GetString(key);
    }
}
