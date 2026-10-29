using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using NSubstitute;
using Sanet.MakaMek.Avalonia.Controls;
using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Presentation.ViewModels.Wrappers;
using Sanet.MakaMek.Services;
using Shouldly;

namespace MakaMek.Avalonia.Tests.Controls;

/// <summary>
/// The card takes its image service from a declared property, so it can be exercised with a
/// substitute instead of the live app. These cover the binding order, the failure modes and the
/// stale-result rule.
/// </summary>
public class UnitItemControlTests
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

        var control = new UnitItemControl { DataContext = CreateUnit(Model) };
        control.ImageService = service;

        SourceOf(control).ShouldBeSameAs(bitmap);
        service.Received(1).GetImage("units/mechs", Model);
    });

    [Fact]
    public Task ServiceFirst_ThenDataContext_RequestsTheUpperCasedModel() => Dispatch(() =>
    {
        var bitmap = CreateBitmap();
        var service = Substitute.For<IImageService<Bitmap>>();
        service.GetImage("units/mechs", Model).Returns(Task.FromResult<Bitmap?>(bitmap));

        var control = new UnitItemControl { ImageService = service };
        control.DataContext = CreateUnit(Model);

        SourceOf(control).ShouldBeSameAs(bitmap);
        service.Received(1).GetImage("units/mechs", Model);
    });

    [Fact]
    public Task NonBitmapImageService_IsRejected() => Dispatch(() =>
    {
        var control = new UnitItemControl { DataContext = CreateUnit(Model) };

        Should.Throw<InvalidOperationException>(() => control.ImageService = Substitute.For<IImageService>())
            .Message.ShouldContain(nameof(UnitItemControl));
    });

    [Fact]
    public async Task ResultArrivingAfterTheCardChangedHands_IsDiscarded() => DispatchAsync(async () =>
    {
        var firstImage = CreateBitmap();
        var secondImage = CreateBitmap();
        var firstRequest = new TaskCompletionSource<Bitmap?>();

        var service = Substitute.For<IImageService<Bitmap>>();
        service.GetImage("units/mechs", Model).Returns(firstRequest.Task);
        service.GetImage("units/mechs", OtherModel).Returns(Task.FromResult<Bitmap?>(secondImage));

        var control = new UnitItemControl { DataContext = CreateUnit(Model) };
        control.ImageService = service;

        control.DataContext = CreateUnit(OtherModel);
        SourceOf(control).ShouldBeSameAs(secondImage);

        firstRequest.SetResult(firstImage);
        await Settle();

        SourceOf(control).ShouldBeSameAs(secondImage, "the image of the previous unit must not win");
    });

    [Fact]
    public Task NullResult_ClearsTheImage() => Dispatch(() =>
    {
        var service = Substitute.For<IImageService<Bitmap>>();
        service.GetImage("units/mechs", Model).Returns(Task.FromResult<Bitmap?>(CreateBitmap()));
        service.GetImage("units/mechs", OtherModel).Returns(Task.FromResult<Bitmap?>(null));

        var control = new UnitItemControl { DataContext = CreateUnit(Model) };
        control.ImageService = service;
        SourceOf(control).ShouldNotBeNull();

        control.DataContext = CreateUnit(OtherModel);

        SourceOf(control).ShouldBeNull();
        TileTintOf(control).OpacityMask.ShouldBeNull();
    });

    [Fact]
    public async Task PlayerTintChange_ReloadsOncePerControl_NotOncePerConstructedCard() => DispatchAsync(async () =>
    {
        var bitmap = CreateBitmap();
        var service = Substitute.For<IImageService<Bitmap>>();
        service.GetImage("units/mechs", Model).Returns(Task.FromResult<Bitmap?>(bitmap));

        // A second card is what used to make every tint change reload twice: each constructor
        // registered another class handler for the whole type.
        var changed = new UnitItemControl { DataContext = CreateUnit(Model), ImageService = service };
        _ = new UnitItemControl { DataContext = CreateUnit(Model), ImageService = service };
        await Settle();

        service.ClearReceivedCalls();
        changed.PlayerTint = "#FF0000";
        await Settle();

        await service.Received(1).GetImage("units/mechs", Model);
        TileTintOf(changed).Background.ShouldBeOfType<SolidColorBrush>()
            .Color.ShouldBe(Color.Parse("#FF0000"));
    });

    private static IImage? SourceOf(UnitItemControl control) =>
        control.FindControl<Image>("UnitImage")!.Source;

    private static Border TileTintOf(UnitItemControl control) =>
        control.FindControl<Border>("TintBorder")!;

    private static Bitmap CreateBitmap() =>
        new(new MemoryStream(Convert.FromBase64String(PngBase64)));

    private static UnitViewModel CreateUnit(string model) => new(new UnitData
    {
        Id = Guid.NewGuid(),
        Chassis = "Atlas",
        Model = model,
        Mass = 100,
        EngineRating = 300,
        EngineType = "Fusion",
        ArmorValues = [],
        Equipment = [],
        AdditionalAttributes = new Dictionary<string, string>(),
        Quirks = new Dictionary<string, string>()
    });

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
        HeadlessUnitTestSession.GetOrStartForAssembly(typeof(UnitItemControlTests).Assembly)
            .Dispatch(action, CancellationToken.None);

    private static void DispatchAsync(Func<Task> action)
    {
        HeadlessUnitTestSession.GetOrStartForAssembly(typeof(UnitItemControlTests).Assembly)
            .Dispatch(async () =>
            {
                await action();
                return true;
            }, CancellationToken.None);
    }
}
