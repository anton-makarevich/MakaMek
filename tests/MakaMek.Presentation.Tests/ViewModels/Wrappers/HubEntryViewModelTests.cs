using AsyncAwaitBestPractices.MVVM;
using Sanet.Transport.SignalR.Client.Relay;
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
    public async Task RefreshStatusAsync_WhenProbesOverlap_OlderResultDoesNotOverrideNewer()
    {
        var firstTcs = new TaskCompletionSource<HubStatus>();
        var secondTcs = new TaskCompletionSource<HubStatus>();
        var probeIndex = 0;
        var sut = new HubEntryViewModel(
            DemoHub,
            checkStatus: (_, _) => probeIndex++ == 0 ? firstTcs.Task : secondTcs.Task);

        // Start two overlapping probes; the second one is the current refresh
        var firstProbe = sut.RefreshStatusAsync();
        var secondProbe = sut.RefreshStatusAsync();

        // The newer probe completes first; its result must win
        secondTcs.SetResult(HubStatus.Online);
        await secondProbe;
        sut.Status.ShouldBe(HubStatus.Online);
        sut.IsCheckingStatus.ShouldBeFalse();

        // The older probe completes afterwards; its result must be ignored
        firstTcs.SetResult(HubStatus.Offline);
        await firstProbe;
        sut.Status.ShouldBe(HubStatus.Online);
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

    [Fact]
    public async Task RefreshStatusAsync_WhenCancelled_SetsUnknownAndStopsChecking()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var sut = new HubEntryViewModel(
            DemoHub,
            checkStatus: (_, token) => Task.FromException<HubStatus>(new OperationCanceledException(token)));

        await sut.RefreshStatusAsync(cts.Token);

        sut.Status.ShouldBe(HubStatus.Unknown);
        sut.IsCheckingStatus.ShouldBeFalse();
    }

    [Fact]
    public async Task RefreshStatusAsync_WhenOlderProbeCancelled_DoesNotOverrideNewerResult()
    {
        using var firstCts = new CancellationTokenSource();
        var firstTcs = new TaskCompletionSource<HubStatus>();
        var secondTcs = new TaskCompletionSource<HubStatus>();
        var probeIndex = 0;
        var sut = new HubEntryViewModel(
            DemoHub,
            checkStatus: (_, token) =>
            {
                if (probeIndex++ == 0)
                {
                    token.Register(() => firstTcs.TrySetException(new OperationCanceledException(token)));
                    return firstTcs.Task;
                }
                return secondTcs.Task;
            });

        // Start two overlapping probes; the second one is the current refresh
        var firstProbe = sut.RefreshStatusAsync(firstCts.Token);
        var secondProbe = sut.RefreshStatusAsync();

        secondTcs.SetResult(HubStatus.Online);
        await secondProbe;
        sut.Status.ShouldBe(HubStatus.Online);

        // Cancelling the stale first probe must not overwrite the newer result
        firstCts.Cancel();
        await firstProbe;
        sut.Status.ShouldBe(HubStatus.Online);
        sut.IsCheckingStatus.ShouldBeFalse();
    }
}
