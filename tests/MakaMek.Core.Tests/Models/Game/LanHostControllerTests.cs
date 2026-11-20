using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Services.Transport;
using Sanet.Transport;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Game;

public class LanHostControllerTests
{
    private readonly ICommandPublisher _commandPublisher = Substitute.For<ICommandPublisher>();
    private readonly ICommandTransportAdapter _transportAdapter = Substitute.For<ICommandTransportAdapter>();
    private readonly INetworkHostService _networkHostService = Substitute.For<INetworkHostService>();

    public LanHostControllerTests()
    {
        _commandPublisher.Adapter.Returns(_transportAdapter);
        _networkHostService.CanStart.Returns(true);
        _networkHostService.IsRunning.Returns(false);
    }

    private LanHostController CreateSut() =>
        new(_commandPublisher, _networkHostService, NullLogger.Instance);

    [Fact]
    public async Task StartAsync_WhenCancelledAfterHostStarts_StopsHostAndDoesNotAddPublisher()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        _networkHostService.Start().Returns(_ =>
        {
            _networkHostService.IsRunning.Returns(true);
            cts.Cancel();
            return Task.CompletedTask;
        });
        _networkHostService.Publisher.Returns(Substitute.For<ITransportPublisher>());
        var sut = CreateSut();

        // Act & Assert
        await Should.ThrowAsync<OperationCanceledException>(() => sut.StartAsync(cts.Token));

        await _networkHostService.Received(1).StopAsync();
        _transportAdapter.DidNotReceive().AddPublisher(Arg.Any<ITransportPublisher>());
    }

    [Fact]
    public async Task StartAsync_WhenCancelledAfterPublisherRegistered_RemovesPublisherAndThrows()
    {
        // Arrange
        var publisher = Substitute.For<ITransportPublisher>();
        using var cts = new CancellationTokenSource();
        _networkHostService.Publisher.Returns(_ =>
        {
            // Cancellation arrives after the host started but before the publisher was installed.
            cts.Cancel();
            return publisher;
        });
        var sut = CreateSut();

        // Act & Assert
        await Should.ThrowAsync<OperationCanceledException>(() => sut.StartAsync(cts.Token));

        _transportAdapter.Received(1).AddPublisher(publisher);
        _transportAdapter.Received(1).RemovePublisher(publisher);
    }
}
