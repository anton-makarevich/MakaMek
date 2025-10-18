using AsyncAwaitBestPractices.MVVM;
using NSubstitute;
using Sanet.MakaMek.Core.Services;
using Sanet.MakaMek.Core.Services.Localization;
using Sanet.MakaMek.Presentation.ViewModels;
using Shouldly;

namespace Sanet.MakaMek.Presentation.Tests.ViewModels;

public class AboutViewModelTests
{
    private readonly IExternalNavigationService _externalNavigationService;
    private readonly ILocalizationService _localizationService;
    private readonly AboutViewModel _sut;

    public AboutViewModelTests()
    {
        _externalNavigationService = Substitute.For<IExternalNavigationService>();
        _localizationService = new FakeLocalizationService();

        _sut = new AboutViewModel(_externalNavigationService, _localizationService);
    }

    [Fact]
    public void Constructor_ShouldInitializeCommands()
    {
        // Assert
        _sut.OpenGitHubCommand.ShouldNotBeNull();
        _sut.OpenMegaMekCommand.ShouldNotBeNull();
        _sut.OpenGameContentRulesCommand.ShouldNotBeNull();
        _sut.OpenContactEmailCommand.ShouldNotBeNull();
    }

    [Fact]
    public void Constructor_ShouldInitializeVersion()
    {
        // Assert
        _sut.Version.ShouldNotBeNullOrEmpty();
        _sut.Version.ShouldStartWith("v");
    }

    [Fact]
    public async Task OpenGitHubCommand_ShouldCallExternalNavigationService()
    {
        // Act
        await ((IAsyncCommand)_sut.OpenGitHubCommand).ExecuteAsync();

        // Assert
        await _externalNavigationService.Received(1).OpenUrlAsync("https://github.com/anton-makarevich/MakaMek");
    }

    [Fact]
    public async Task OpenMegaMekCommand_ShouldCallExternalNavigationService()
    {
        // Act
        await ((IAsyncCommand)_sut.OpenMegaMekCommand).ExecuteAsync();

        // Assert
        await _externalNavigationService.Received(1).OpenUrlAsync("https://megamek.org");
    }

    [Fact]
    public async Task OpenGameContentRulesCommand_ShouldCallExternalNavigationService()
    {
        // Act
        await ((IAsyncCommand)_sut.OpenGameContentRulesCommand).ExecuteAsync();

        // Assert
        await _externalNavigationService.Received(1).OpenUrlAsync("https://www.xbox.com/en-US/developers/rules");
    }

    [Fact]
    public void GameDescription_ShouldReturnLocalizedString()
    {
        // Act
        var result = _sut.GameDescription;

        // Assert
        result.ShouldNotBeNullOrEmpty();
        result.ShouldContain("MakaMek");
        result.ShouldContain("BattleTech");
    }

    [Fact]
    public void MegaMekAttribution_ShouldReturnLocalizedString()
    {
        // Act
        var result = _sut.MegaMekAttribution;

        // Assert
        result.ShouldNotBeNullOrEmpty();
        result.ShouldContain("MegaMek Data Repository");
        result.ShouldContain("Creative Commons");
    }

    [Fact]
    public void FreeAndOpenSourceStatement_ShouldReturnLocalizedString()
    {
        // Act
        var result = _sut.FreeAndOpenSourceStatement;

        // Assert
        result.ShouldNotBeNullOrEmpty();
        result.ShouldContain("free");
        result.ShouldContain("open source");
    }
    
    [Fact]
    public void ContactStatement_ShouldReturnLocalizedString()
    {
        // Act
        var result = _sut.ContactStatement;

        // Assert
        result.ShouldNotBeNullOrEmpty();
        result.ShouldContain("contact");
    }

    [Fact]
    public void TrademarkNotice1_ShouldReturnLocalizedString()
    {
        // Act
        var result = _sut.TrademarkNotice1;

        // Assert
        result.ShouldNotBeNullOrEmpty();
        result.ShouldContain("MechWarrior");
        result.ShouldContain("BattleMech");
        result.ShouldContain("Topps");
    }

    [Fact]
    public void TrademarkNotice2_ShouldReturnLocalizedString()
    {
        // Act
        var result = _sut.TrademarkNotice2;

        // Assert
        result.ShouldNotBeNullOrEmpty();
        result.ShouldContain("Microsoft");
    }

    [Fact]
    public void GameContentRulesNotice_ShouldReturnLocalizedString()
    {
        // Act
        var result = _sut.GameContentRulesNotice;

        // Assert
        result.ShouldNotBeNullOrEmpty();
        result.ShouldContain("Game Content Usage Rules");
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenLocalizationServiceIsNull()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new AboutViewModel(_externalNavigationService, null!));
    }

    [Fact]
    public async Task OpenContactEmailCommand_ShouldCallExternalNavigationService()
    {
        // Act
        await ((IAsyncCommand)_sut.OpenContactEmailCommand).ExecuteAsync();

        // Assert
        await _externalNavigationService.Received(1).OpenEmailAsync(
            "anton.makarevich@gmail.com",
            Arg.Is<string>(s => s.Contains("MakaMek") && s.Contains("question")));
    }
}

