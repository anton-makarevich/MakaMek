using AsyncAwaitBestPractices.MVVM;
using Sanet.MakaMek.Core.Services.Transport.Relay;
using Sanet.MakaMek.Presentation.ViewModels.Wrappers;
using Shouldly;

namespace Sanet.MakaMek.Presentation.Tests.ViewModels.Wrappers;

public class HubEntryViewModelTests
{
    private static HubConfigData DemoHub => new("demo", "Demo Hub", "http://demo.local", string.Empty, true);

    [Fact]
    public void Constructor_StatusDefaultsToUnknown()
    {
        var sut = new HubEntryViewModel(DemoHub);

        sut.Status.ShouldBe(HubStatus.Unknown);
        sut.IsCheckingStatus.ShouldBeFalse();
    }

    [Fact]
    public void Constructor_RefreshStatusCommand_ShouldBeInitialized()
    {
        var sut = new HubEntryViewModel(DemoHub);

        sut.RefreshStatusCommand.ShouldNotBeNull();
    }

    [Fact]
    public async Task RefreshStatusAsync_WithCheckStatus_TransitionsToCheckingThenResult()
    {
        var tcs = new TaskCompletionSource<HubStatus>();
        var sut = new HubEntryViewModel(
            DemoHub,
            checkStatus: (_, _) => tcs.Task);

        var task = sut.RefreshStatusAsync();

        sut.IsCheckingStatus.ShouldBeTrue();
        sut.Status.ShouldBe(HubStatus.Checking);

        tcs.SetResult(HubStatus.Online);
        await task;

        sut.Status.ShouldBe(HubStatus.Online);
        sut.IsCheckingStatus.ShouldBeFalse();
    }

    [Fact]
    public async Task RefreshStatusAsync_WhenCheckStatusReportsOffline_SetsOffline()
    {
        var sut = new HubEntryViewModel(
            DemoHub,
            checkStatus: (_, _) => Task.FromResult(HubStatus.Offline));

        await sut.RefreshStatusAsync();

        sut.Status.ShouldBe(HubStatus.Offline);
    }

    [Fact]
    public async Task RefreshStatusAsync_WhenCheckStatusThrows_SetsOffline()
    {
        var sut = new HubEntryViewModel(
            DemoHub,
            checkStatus: (_, _) => Task.FromException<HubStatus>(new Exception("probe failed")));

        await sut.RefreshStatusAsync();

        sut.Status.ShouldBe(HubStatus.Offline);
        sut.IsCheckingStatus.ShouldBeFalse();
    }

    [Fact]
    public async Task RefreshStatusAsync_WithoutCheckStatus_SetsUnknown()
    {
        var sut = new HubEntryViewModel(DemoHub);

        await sut.RefreshStatusAsync();

        sut.Status.ShouldBe(HubStatus.Unknown);
        sut.IsCheckingStatus.ShouldBeFalse();
    }

    [Fact]
    public async Task RefreshStatusCommand_WhenExecuted_InvokesCheckStatus()
    {
        var invoked = false;
        var sut = new HubEntryViewModel(
            DemoHub,
            checkStatus: (_, _) =>
            {
                invoked = true;
                return Task.FromResult(HubStatus.Online);
            });

        await ((IAsyncCommand)sut.RefreshStatusCommand).ExecuteAsync();

        invoked.ShouldBeTrue();
        sut.Status.ShouldBe(HubStatus.Online);
    }
}
