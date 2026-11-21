using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Services.Transport;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Game;

public class RelayHostConnectorTests
{
    private readonly ICommandPublisher _commandPublisher = Substitute.For<ICommandPublisher>();

    private RelayHostConnector CreateSut() =>
        new(_commandPublisher, null, null, null, null, NullLogger.Instance);

    [Fact]
    public void Dispose_WhenCalledTwice_CompletesFallbackStatusOnceAndDoesNotThrow()
    {
        // Arrange - no forwarder, so the connector's own fallback subject backs the status stream
        var sut = CreateSut();
        var completions = 0;
        sut.ConnectionStatus.Subscribe(_ => { }, _ => { }, () => completions++);

        // Act & Assert - the IDisposable contract requires repeated calls to be no-ops
        Should.NotThrow(sut.Dispose);
        Should.NotThrow(sut.Dispose);
        completions.ShouldBe(1);
    }
}
