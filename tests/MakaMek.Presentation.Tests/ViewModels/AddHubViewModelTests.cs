using AsyncAwaitBestPractices.MVVM;
using NSubstitute;
using Sanet.MakaMek.Localization;
using Sanet.MakaMek.Presentation.ViewModels;
using Sanet.MVVM.Core.Services;
using Shouldly;

namespace Sanet.MakaMek.Presentation.Tests.ViewModels;

public class AddHubViewModelTests
{
    private readonly ILocalizationService _localizationService = new FakeLocalizationService();
    private readonly INavigationService _navigationService = Substitute.For<INavigationService>();

    private AddHubViewModel CreateSut()
    {
        var sut = new AddHubViewModel(_localizationService);
        sut.SetNavigationService(_navigationService);
        return sut;
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
    public async Task Confirm_WhenBaseUrlEmpty_ShouldSetValidationMessageAndKeepDialogOpen()
    {
        // Arrange
        var sut = CreateSut();
        sut.BaseUrl = "   ";

        // Act
        await ((IAsyncCommand)sut.ConfirmCommand).ExecuteAsync();

        // Assert
        sut.ValidationMessage.ShouldBe("Hub URL is required.");
        sut.HasValidationMessage.ShouldBeTrue();
        sut.GetResultAsync().IsCompleted.ShouldBeFalse();
    }

    [Fact]
    public async Task Confirm_WhenBaseUrlValid_ShouldReturnTrimmedResult()
    {
        // Arrange
        var sut = CreateSut();
        sut.Name = "  My Hub  ";
        sut.BaseUrl = " http://my-hub.example ";
        sut.ApiKey = "secret";

        // Act
        await ((IAsyncCommand)sut.ConfirmCommand).ExecuteAsync();

        // Assert
        var result = await sut.GetResultAsync();
        result.ShouldNotBeNull();
        result.Name.ShouldBe("My Hub");
        result.BaseUrl.ShouldBe("http://my-hub.example");
        result.ApiKey.ShouldBe("secret");
    }

    [Fact]
    public async Task BaseUrl_WhenChanged_ShouldClearValidationMessage()
    {
        // Arrange
        var sut = CreateSut();
        sut.BaseUrl = "   ";
        await ((IAsyncCommand)sut.ConfirmCommand).ExecuteAsync();
        sut.HasValidationMessage.ShouldBeTrue();

        // Act
        sut.BaseUrl = "http://my-hub.example";

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
