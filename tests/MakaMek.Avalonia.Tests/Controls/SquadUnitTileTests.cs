using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using NSubstitute;
using Sanet.MakaMek.Avalonia.Controls;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Services;
using Shouldly;

namespace MakaMek.Avalonia.Tests.Controls;

/// <summary>
/// The tile takes its image service from a declared property, so it can be exercised with a
/// substitute instead of the live app. These cover the binding order, the failure modes and the
/// stale-result rule.
/// </summary>
public class SquadUnitTileTests
{
    private const string Model = "AS7-D";
    private const string OtherModel = "AWS-8Q";

    private const string PngBase64 =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==";

    [Fact]
    public Task DataContextFirst_ThenService_RequestsTheUpperCasedModel() => Dispatch(() =>
    {
        var bitmap = CreateBitmap();
        var service = Substitute.For<IImageService<Bitmap>>();
        service.GetImage("units/mechs", Model).Returns(Task.FromResult<Bitmap?>(bitmap));

        var tile = new SquadUnitTile { DataContext = CreateUnit(Model) };
        tile.ImageService = service;

        SourceOf(tile).ShouldBeSameAs(bitmap);
        service.Received(1).GetImage("units/mechs", Model);
    });

    [Fact]
    public Task ServiceFirst_ThenDataContext_RequestsTheUpperCasedModel() => Dispatch(() =>
    {
        var bitmap = CreateBitmap();
        var service = Substitute.For<IImageService<Bitmap>>();
        service.GetImage("units/mechs", Model).Returns(Task.FromResult<Bitmap?>(bitmap));

        var tile = new SquadUnitTile { ImageService = service };
        tile.DataContext = CreateUnit(Model);

        SourceOf(tile).ShouldBeSameAs(bitmap);
        service.Received(1).GetImage("units/mechs", Model);
    });

    [Fact]
    public Task NonBitmapImageService_IsRejected() => Dispatch(() =>
    {
        var tile = new SquadUnitTile { DataContext = CreateUnit(Model) };

        Should.Throw<InvalidOperationException>(() => tile.ImageService = Substitute.For<IImageService>())
            .Message.ShouldContain(nameof(SquadUnitTile));
    });

    [Fact]
    public Task ResultArrivingAfterTheTileChangedHands_IsDiscarded() => DispatchAsync(async () =>
    {
        var firstImage = CreateBitmap();
        var secondImage = CreateBitmap();
        var firstRequest = new TaskCompletionSource<Bitmap?>();

        var service = Substitute.For<IImageService<Bitmap>>();
        service.GetImage("units/mechs", Model).Returns(firstRequest.Task);
        service.GetImage("units/mechs", OtherModel).Returns(Task.FromResult<Bitmap?>(secondImage));

        var tile = new SquadUnitTile { DataContext = CreateUnit(Model) };
        tile.ImageService = service;

        tile.DataContext = CreateUnit(OtherModel);
        SourceOf(tile).ShouldBeSameAs(secondImage);

        firstRequest.SetResult(firstImage);
        await Settle();

        SourceOf(tile).ShouldBeSameAs(secondImage, "the image of the previous unit must not win");
    });

    [Fact]
    public Task NullResult_ClearsTheImageAndTint() => Dispatch(() =>
    {
        var service = Substitute.For<IImageService<Bitmap>>();
        service.GetImage("units/mechs", Model).Returns(Task.FromResult<Bitmap?>(CreateBitmap()));
        service.GetImage("units/mechs", OtherModel).Returns(Task.FromResult<Bitmap?>(null));

        var tile = new SquadUnitTile { DataContext = CreateUnit(Model) };
        tile.ImageService = service;
        SourceOf(tile).ShouldNotBeNull();

        tile.DataContext = CreateUnit(OtherModel);

        SourceOf(tile).ShouldBeNull();
        tile.FindControl<Border>("UnitTint")!.OpacityMask.ShouldBeNull();
    });

    private static IImage? SourceOf(SquadUnitTile tile) =>
        tile.FindControl<Image>("UnitImage")!.Source;

    private static Bitmap CreateBitmap() =>
        new(new MemoryStream(Convert.FromBase64String(PngBase64)));

    private static IUnit CreateUnit(string model)
    {
        var unit = Substitute.For<IUnit>();
        unit.Model.Returns(model);
        return unit;
    }

    /// <summary>Lets the fire-and-forget reload finish on the UI thread.</summary>
    private static async Task Settle()
    {
        for (var i = 0; i < 5; i++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(1);
        }
    }

    private static Task Dispatch(Action action) =>
        HeadlessUnitTestSession.GetOrStartForAssembly(typeof(SquadUnitTileTests).Assembly)
            .Dispatch(action, CancellationToken.None);

    private static Task DispatchAsync(Func<Task> action) =>
        HeadlessUnitTestSession.GetOrStartForAssembly(typeof(SquadUnitTileTests).Assembly)
            .Dispatch(async () =>
            {
                await action();
                return true;
            }, CancellationToken.None);
}
