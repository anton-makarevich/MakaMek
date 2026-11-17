using Microsoft.Extensions.Logging;
using NSubstitute;
using Sanet.MakaMek.Core.Services;
using Sanet.MakaMek.Map.Factories;
using Sanet.MakaMek.Map.Generators;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Models.Terrains;
using Sanet.MakaMek.Presentation.ViewModels.Setup;
using Shouldly;

namespace Sanet.MakaMek.Presentation.Tests.ViewModels.Setup;

public class LobbyMapPreviewTests
{
    private readonly IMapPreviewRenderer _previewRenderer = Substitute.For<IMapPreviewRenderer>();
    private readonly LobbyMapPreview _sut;

    public LobbyMapPreviewTests()
    {
        _sut = new LobbyMapPreview(_previewRenderer, Substitute.For<ILogger>());
    }

    [Fact]
    public async Task BeginUpdate_WithNullMap_ClearsTheMapAndDisposesTheRenderedImage()
    {
        // Arrange - a rendered preview for the previous map
        var map = CreateMap();
        var previousImage = Substitute.For<IDisposable>();
        _previewRenderer.GeneratePreview(map, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<object?>(previousImage));
        _sut.BeginUpdate(map);
        await _sut.RenderAsync();
        _sut.Image.ShouldBe(previousImage);
        _sut.HasMap.ShouldBeTrue();

        // Act - the map is withdrawn
        _sut.BeginUpdate(null);

        // Assert - no stale image or map survives, and a render must not be started
        _sut.Map.ShouldBeNull();
        _sut.HasMap.ShouldBeFalse();
        _sut.Image.ShouldBeNull();
        previousImage.Received(1).Dispose();

        await _sut.RenderAsync();
        await _previewRenderer.Received(1)
            .GeneratePreview(map, Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RenderAsync_WhenMapWasNeverApplied_DoesNotRender()
    {
        // Act
        await _sut.RenderAsync();

        // Assert
        _sut.Image.ShouldBeNull();
        await _previewRenderer.DidNotReceive()
            .GeneratePreview(Arg.Any<BattleMap>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    private static BattleMap CreateMap() =>
        new BattleMapFactory().GenerateMap(2, 2, new SingleTerrainGenerator(2, 2, new ClearTerrain()));
}
