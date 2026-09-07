using AsyncAwaitBestPractices.MVVM;
using NSubstitute;
using Sanet.MakaMek.Assets.Configuration;
using Sanet.MakaMek.Localization;
using Sanet.MakaMek.Presentation.ViewModels;
using Sanet.MVVM.Core.Services;
using Shouldly;

namespace Sanet.MakaMek.Presentation.Tests.ViewModels;

public class AddProviderViewModelTests
{
    private readonly ILocalizationService _localizationService = new FakeLocalizationService();
    private readonly INavigationService _navigationService = Substitute.For<INavigationService>();

    private AddProviderViewModel CreateSut()
    {
        var sut = new AddProviderViewModel(
            [ProviderType.Bucket, ProviderType.GitHub, ProviderType.Filesystem],
            [AssetType.Units, AssetType.Hexes],
            _localizationService);
        sut.SetNavigationService(_navigationService);
        return sut;
    }

    [Fact]
    public void Constructor_ShouldDefaultToBucketAndUnits()
    {
        // Arrange & Act
        var sut = CreateSut();

        // Assert
        sut.SelectedProviderType.ShouldBe(ProviderType.Bucket);
        sut.SelectedAssetType.ShouldBe(AssetType.Units);
    }

    [Fact]
    public void GetResultAsync_ShouldReturnIncompleteTask()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var resultTask = sut.GetResultAsync();

        // Assert
        resultTask.IsCompleted.ShouldBeFalse();
    }

    [Fact]
    public async Task Confirm_WhenUrlOrPathEmpty_ShouldSetValidationMessageAndKeepDialogOpen()
    {
        // Arrange
        var sut = CreateSut();
        sut.UrlOrPath = "   ";

        // Act
        await ((IAsyncCommand)sut.ConfirmCommand).ExecuteAsync();

        // Assert
        sut.ValidationMessage.ShouldBe("URL or path is required.");
        sut.HasValidationMessage.ShouldBeTrue();
        sut.GetResultAsync().IsCompleted.ShouldBeFalse();
    }

    [Fact]
    public async Task Confirm_WhenUrlOrPathValid_ShouldReturnTrimmedResult()
    {
        // Arrange
        var sut = CreateSut();
        sut.SelectedProviderType = ProviderType.Filesystem;
        sut.SelectedAssetType = AssetType.Hexes;
        sut.UrlOrPath = " /data/hexes ";

        // Act
        await ((IAsyncCommand)sut.ConfirmCommand).ExecuteAsync();

        // Assert
        var result = await sut.GetResultAsync();
        result.ShouldNotBeNull();
        result.ProviderType.ShouldBe(ProviderType.Filesystem);
        result.AssetType.ShouldBe(AssetType.Hexes);
        result.UrlOrPath.ShouldBe("/data/hexes");
    }

    [Fact]
    public async Task UrlOrPath_WhenChanged_ShouldClearValidationMessage()
    {
        // Arrange
        var sut = CreateSut();
        sut.UrlOrPath = "   ";
        await ((IAsyncCommand)sut.ConfirmCommand).ExecuteAsync();
        sut.HasValidationMessage.ShouldBeTrue();

        // Act
        sut.UrlOrPath = "/data/units";

        // Assert
        sut.ValidationMessage.ShouldBeEmpty();
        sut.HasValidationMessage.ShouldBeFalse();
    }

    [Fact]
    public async Task Cancel_ShouldReturnNullResult()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        await ((IAsyncCommand)sut.CancelCommand).ExecuteAsync();

        // Assert
        var result = await sut.GetResultAsync();
        result.ShouldBeNull();
    }
}
