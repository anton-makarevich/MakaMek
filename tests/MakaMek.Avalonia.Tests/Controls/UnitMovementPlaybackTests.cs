using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using MonacoLabs.Presto;
using NSubstitute;
using Sanet.Localization;
using Sanet.MakaMek.Assets.Services;
using Sanet.MakaMek.Avalonia.Controls;
using Sanet.MakaMek.Avalonia.Services;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Map.Data;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Presentation.ViewModels;
using Sanet.MakaMek.Services;
using Shouldly;
using System.Reactive.Concurrency;

namespace MakaMek.Avalonia.Tests.Controls;

/// <summary>
/// Playback is motion, so the assertions are about its contract: it starts at the path start rather
/// than the already-applied destination, it always takes five seconds, frames follow travelled
/// distance, reduced motion snaps to the end, and cancellation returns the control to the model.
/// </summary>
public class UnitMovementPlaybackTests
{
    private const string AtlasPngBase64 =
        "iVBORw0KGgoAAAANSUhEUgAAAQAAAABACAYAAAD1Xam+AAAA4UlEQVR4nO3UgQnAMADDsBR2eD/fYG9YguQEn+2+Z5uZLbfnfyBJACBMACBMACBMACBMACBMACBMACBMACBMACBMACBMACBMACBMACBMACBMACBMACBMACBMACBMACBMACBMACBMACBMACBMACBMACBMACBMACBMACBMACBMACBMACBMACBMACBMACBMACBMACBMACBMACBMACBMACBMACBMACBMACBMACBMACBMACBMACBMACBMACBMACBMACBMACBMACBMACBMACBMACBMACBMACBMACBMACBMACBMACBMACDsA3ELA7uBKX0+AAAAAElFTkSuQmCC";

    private const string Model = "AS7-D";

    [Fact]
    public Task Playback_StartsAtThePathStart_AndArrivesAfterFiveSeconds() => Dispatch(() =>
    {
        using var engine = StartEngine();
        using var harness = new Harness();

        harness.Control.OnMoveCommand(Move(0, 0, 2, 0));
        Canvas.GetLeft(harness.Control).ShouldBe(0, 0.001);

        engine.Advance(2500);
        Canvas.GetLeft(harness.Control).ShouldBe(75, 1, "half the time is half the two-hex path");

        engine.Advance(2500);
        Canvas.GetLeft(harness.Control).ShouldBe(150, 0.001);
    });

    [Fact]
    public Task PathLengthDoesNotChangeTheFiveSecondDuration() => Dispatch(() =>
    {
        using var engine = StartEngine();
        using var shortPath = new Harness();
        using var longPath = new Harness();
        shortPath.SetAuthoritativePosition(1, 0);
        longPath.SetAuthoritativePosition(4, 0);

        shortPath.Control.OnMoveCommand(Move(0, 0, 1, 0));
        longPath.Control.OnMoveCommand(Move(0, 0, 4, 0));

        Canvas.GetLeft(shortPath.Control).ShouldBe(0, 0.001);
        Canvas.GetLeft(longPath.Control).ShouldBe(0, 0.001);

        engine.Advance(1000);
        Canvas.GetLeft(shortPath.Control).ShouldBe(15, 2, "at 1 s the short path is 20% along");
        Canvas.GetLeft(longPath.Control).ShouldBe(60, 2, "at 1 s the long path is 20% along");

        engine.Advance(3800);
        Canvas.GetLeft(shortPath.Control).ShouldBeLessThan(75, "the short path is still moving before 5 s");
        Canvas.GetLeft(longPath.Control).ShouldBeLessThan(300, "the long path is still moving before 5 s");

        engine.Advance(200);
        Canvas.GetLeft(shortPath.Control).ShouldBe(75, 0.001);
        Canvas.GetLeft(longPath.Control).ShouldBe(300, 0.001);
    });

    [Fact]
    public Task FramesFollowTravelledDistance() => Dispatch(() =>
    {
        using var engine = StartEngine();
        using var harness = new Harness();
        var frames = harness.SpriteSet.Clips["Walk"].Frames;
        var image = ImageOf(harness.Control);

        harness.Control.OnMoveCommand(Move(0, 0, 2, 0));
        image.Source.ShouldBeSameAs(frames[0]);

        // Half the path is half the stride (75 px of 100 px per cycle) -> frame 7 of 10.
        engine.Advance(2500);
        image.Source.ShouldBeSameAs(frames[7]);
    });

    [Fact]
    public Task ReducedMotion_SnapsToTheDestination() => Dispatch(() =>
    {
        using var engine = StartEngine();
        using var harness = new Harness();
        Presto.Defaults.ReducedMotion = true;
        try
        {
            harness.Control.OnMoveCommand(Move(0, 0, 2, 0));

            Canvas.GetLeft(harness.Control).ShouldBe(harness.AuthoritativePosition.Coordinates.H, 0.001);
            ImageSourceOf(harness).ShouldBeSameAs(harness.StaticImage);

            engine.Advance(100);
            Canvas.GetLeft(harness.Control).ShouldBe(harness.AuthoritativePosition.Coordinates.H, 0.001);
        }
        finally
        {
            Presto.Defaults.ReducedMotion = false;
        }
    });

    [Fact]
    public Task DisposingTheControl_ReconcilesWithTheAuthoritativePosition() => Dispatch(() =>
    {
        using var engine = StartEngine();
        var harness = new Harness();

        harness.Control.OnMoveCommand(Move(0, 0, 2, 0));
        engine.Advance(1000);
        Canvas.GetLeft(harness.Control).ShouldBeLessThan(150);

        harness.Control.Dispose();

        Canvas.GetLeft(harness.Control).ShouldBe(harness.AuthoritativePosition.Coordinates.H, 0.001);
        ImageSourceOf(harness).ShouldBeSameAs(harness.StaticImage);
    });

    [Fact]
    public Task AnInterruptionCancelsPlaybackAndReconcilesTheModel() => Dispatch(() =>
    {
        using var engine = StartEngine();
        using var harness = new Harness();

        harness.Control.OnMoveCommand(Move(0, 0, 2, 0));
        engine.Advance(1000);
        Canvas.GetLeft(harness.Control).ShouldBeLessThan(150);

        harness.ViewModel.DetachHandlers();

        Canvas.GetLeft(harness.Control).ShouldBe(harness.AuthoritativePosition.Coordinates.H, 0.001);
        ImageSourceOf(harness).ShouldBeSameAs(harness.StaticImage);
    });

    [Fact]
    public Task QueuedCommands_PlayInOrder() => Dispatch(() =>
    {
        using var engine = StartEngine();
        using var harness = new Harness();

        harness.SetAuthoritativePosition(1, 0);
        harness.Control.OnMoveCommand(Move(0, 0, 1, 0));
        harness.SetAuthoritativePosition(2, 0);
        harness.Control.OnMoveCommand(Move(1, 0, 2, 0));

        Canvas.GetLeft(harness.Control).ShouldBe(0, 0.001);

        engine.Advance(5000);
        Canvas.GetLeft(harness.Control).ShouldBe(75, 1, "the second command starts where the first ended");

        engine.Advance(5000);
        Canvas.GetLeft(harness.Control).ShouldBe(150, 0.001);
    });

    [Fact]
    public Task Jump_TranslatesButShowsTheStaticImage() => Dispatch(() =>
    {
        using var engine = StartEngine();
        using var harness = new Harness();

        harness.Control.OnMoveCommand(Move(0, 0, 2, 0, MovementType.Jump));
        var image = ImageOf(harness.Control);

        engine.Advance(2500);
        Canvas.GetLeft(harness.Control).ShouldBe(75, 1);
        image.Source.ShouldBeSameAs(harness.StaticImage);
    });

    [Fact]
    public void FrameRectangles_FollowTheSheetCoordinateFormula()
    {
        const int padding = 2;
        const int width = 16;
        const int height = 18;

        UnitSpriteService.GetFrameRect(padding, width, height, row: 0, column: 0)
            .ShouldBe(new PixelRect(2, 2, width, height));
        UnitSpriteService.GetFrameRect(padding, width, height, row: 1, column: 3)
            .ShouldBe(new PixelRect(2 + 3 * (width + padding), 2 + 1 * (height + padding), width, height));
    }

    private static MoveUnitCommand Move(int fromQ, int fromR, int toQ, int toR,
        MovementType movementType = MovementType.Walk) =>
        new()
        {
            GameOriginId = Guid.NewGuid(),
            UnitId = Guid.Empty,
            PlayerId = Guid.Empty,
            MovementType = movementType,
            IsCompleted = true,
            MovementPath =
            [
                new PathSegmentData
                {
                    From = new HexPositionData
                    {
                        Coordinates = new HexCoordinateData(fromQ, fromR),
                        Facing = (int)HexDirection.Top
                    },
                    To = new HexPositionData
                    {
                        Coordinates = new HexCoordinateData(toQ, toR),
                        Facing = (int)HexDirection.Top
                    },
                    Costs = []
                }
            ]
        };

    private static Image ImageOf(UnitControl control) => control.Children.OfType<Image>().First();

    private static IImage? ImageSourceOf(Harness harness) => ImageOf(harness.Control).Source;

    private sealed class Harness : IDisposable
    {
        private HexPosition _position;

        public Harness()
        {
            var atlas = new Bitmap(new MemoryStream(Convert.FromBase64String(AtlasPngBase64)));
            var walkFrames = Enumerable.Range(0, 10)
                .Select(column => new CroppedBitmap(atlas,
                    UnitSpriteService.GetFrameRect(2, 16, 16, row: 0, column)))
                .ToList();

            SpriteSet = new UnitSpriteSet(atlas, 16, 16, new Dictionary<string, UnitSpriteClip>
            {
                ["Walk"] = new("Walk", walkFrames, DistancePerCycle: 100, Loop: true, Fps: 12)
            });

            StaticImage = new Bitmap(new MemoryStream(Convert.FromBase64String(AtlasPngBase64)));

            _position = new HexPosition(new HexCoordinates(2, 0), HexDirection.Top);
            var unit = Substitute.For<IUnit>();
            unit.Id.Returns(Guid.NewGuid());
            unit.Model.Returns(Model);
            unit.IsDeployed.Returns(true);
            unit.Position.Returns(_ => _position);
            unit.Notifications.Returns(Array.Empty<Sanet.MakaMek.Core.Events.UiEvent>());
            unit.Owner.Returns((Sanet.MakaMek.Core.Models.Game.Players.IPlayer?)null);
            unit.DequeueNotification().Returns((Sanet.MakaMek.Core.Events.UiEvent?)null);

            var imageService = Substitute.For<IImageService<Bitmap>>();
            imageService.GetImage("units/mechs", Model).Returns(Task.FromResult<Bitmap?>(StaticImage));

            var spriteService = Substitute.For<IUnitSpriteService>();
            spriteService.GetSpriteSet(Model).Returns(Task.FromResult<UnitSpriteSet?>(SpriteSet));

            var dispatcher = Substitute.For<IDispatcherService>();
            dispatcher.RunOnUIThread(Arg.InvokeDelegate<Action>());
            dispatcher.Scheduler.Returns(Scheduler.Immediate);

            var viewModel = new BattleMapViewModel(
                Substitute.For<IImageService>(),
                Substitute.For<ITerrainAssetService>(),
                Substitute.For<ILocalizationService>(),
                dispatcher,
                Substitute.For<IRulesProvider>(),
                Substitute.For<IPlatformService>());

            ViewModel = viewModel;
            Control = new UnitControl(unit, imageService, viewModel, spriteService);
            var canvas = new Canvas();
            canvas.Children.Add(Control);
        }

        public UnitSpriteSet SpriteSet { get; }

        public Bitmap StaticImage { get; }

        public UnitControl Control { get; }

        public BattleMapViewModel ViewModel { get; }

        public HexPosition AuthoritativePosition => _position;

        public void SetAuthoritativePosition(int q, int r) =>
            _position = new HexPosition(new HexCoordinates(q, r), HexDirection.Top);

        public void Dispose() => Control.Dispose();
    }

    private static TestEngine StartEngine()
    {
        PrestoAvalonia.EnsureInitialized();
        var frames = new ManualFrameSource();
        var previous = Presto.Engine.FrameSource;
        Presto.Engine.FrameSource = frames;
        return new TestEngine(frames, () => Presto.Engine.FrameSource = previous);
    }

    private sealed class TestEngine(ManualFrameSource frames, Action restore) : IDisposable
    {
        public void Advance(int milliseconds)
        {
            for (var elapsed = 0; elapsed < milliseconds; elapsed += 16)
            {
                frames.Advance(16);
            }
        }

        public void Dispose() => restore();
    }

    private static Task Dispatch(Action action)
    {
        var session = HeadlessUnitTestSession.GetOrStartForAssembly(
            typeof(UnitMovementPlaybackTests).Assembly);
        return session.Dispatch(action, CancellationToken.None);
    }
}
