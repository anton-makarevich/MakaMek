using Microsoft.Extensions.Logging;
using NSubstitute;
using Sanet.MakaMek.Assets.Services;
using Sanet.MakaMek.Bots.Models;
using Sanet.MakaMek.Core.Models.Game.Factories;
using Sanet.MakaMek.Core.Utils;
using Sanet.MakaMek.Presentation.ViewModels.Setup;
using Sanet.MakaMek.Services;
using Shouldly;

namespace Sanet.MakaMek.Presentation.Tests.ViewModels.Setup;

public class GameSetupSessionTests
{
    private readonly IFileCachingService _cachingService = Substitute.For<IFileCachingService>();
    private readonly GameSetupSession _sut;

    public GameSetupSessionTests()
    {
        _sut = new GameSetupSession(
            Substitute.For<IUnitsLoader>(),
            Substitute.For<IDispatcherService>(),
            Substitute.For<IGameFactory>(),
            Substitute.For<IBotManager>(),
            new DefaultPlayerStore(_cachingService),
            new GameSetupDialogService(Substitute.For<IMechFactory>()),
            new LocalPlayerCommands(Substitute.For<IBotManager>()));
    }

    [Fact]
    public async Task AddDefaultPlayerAsync_RechecksAdmissionAfterLoadingTheCachedPlayer()
    {
        // Arrange - the default-player read stays pending while the lobby fills up
        var cacheRead = new TaskCompletionSource<byte[]?>();
        _cachingService.TryGetCachedFile(Arg.Any<string>()).Returns(cacheRead.Task);
        var canAddPlayer = true;

        // Act
        var addTask = _sut.AddDefaultPlayerAsync(
            (_, _) => throw new InvalidOperationException("the default player must not be created"),
            () => canAddPlayer);
        canAddPlayer = false;
        cacheRead.SetResult(null);

        // Assert - the stale pre-read admission must not add a fifth player
        (await addTask).ShouldBeFalse();
        _sut.Players.ShouldBeEmpty();
    }
}
