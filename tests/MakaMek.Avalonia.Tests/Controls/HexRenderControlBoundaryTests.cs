using System.Reactive.Concurrency;
using Avalonia.Headless;
using Avalonia.Media;
using NSubstitute;
using Sanet.MakaMek.Assets.Services;
using Sanet.MakaMek.Avalonia.Controls;
using Sanet.MakaMek.Avalonia.Controls.Services;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Models.Highlights;
using Sanet.MakaMek.Presentation.ViewModels;
using Shouldly;

namespace MakaMek.Avalonia.Tests.Controls;

/// <summary>
/// Boundary outlines carry the highlight type rather than a colour, so the renderer is the only
/// place a colour is chosen. These tests pin that the colour comes from the themed resources.
/// </summary>
public class HexRenderControlBoundaryTests
{
    private readonly SolidColorBrush _movementStroke = new(Colors.Lime);
    private readonly SolidColorBrush _attackStroke = new(Colors.Fuchsia);
    private readonly SolidColorBrush _losStroke = new(Colors.Navy);
    private readonly SolidColorBrush _whiteStroke = new(Colors.White);

    private static Task Dispatch(Action action)
    {
        var session = HeadlessUnitTestSession.GetOrStartForAssembly(
            typeof(HexRenderControlBoundaryTests).Assembly);
        return session.Dispatch(action, CancellationToken.None);
    }

    private HexRenderControl CreateSut()
    {
        var resources = Substitute.For<IAvaloniaResourcesLocator>();
        resources.TryFindResource("MovementReachableStrokeBrush").Returns(_movementStroke);
        resources.TryFindResource("AttackReachableStrokeBrush").Returns(_attackStroke);
        resources.TryFindResource("LosBlockingStrokeBrush").Returns(_losStroke);
        resources.TryFindResource("WhiteHighlightBrush").Returns(_whiteStroke);

        return new HexRenderControl(
            Substitute.For<ITerrainAssetService>(),
            null,
            Scheduler.CurrentThread,
            resources);
    }

    [Fact]
    public Task SetBoundaryOutlines_ShouldTakeStrokeFromThemedResource_PerHighlightType() => Dispatch(() =>
    {
        // Arrange
        var sut = CreateSut();
        var movement = new HighlightBoundaryOutline(
            0b000001, new MovementReachableHighlight(MovementType.Walk), 2);
        var attack = new HighlightBoundaryOutline(
            0b000001, new AttackReachableHighlight(["PPC"]), 2);
        var los = new HighlightBoundaryOutline(
            0b000001, new LosBlockingHighlight(LineOfSightBlockReason.InterveningTerrain), 2);

        // Act
        sut.SetBoundaryOutlines(new Dictionary<HexCoordinates, HighlightBoundaryOutline>
        {
            [new HexCoordinates(1, 1)] = movement,
            [new HexCoordinates(2, 1)] = attack,
            [new HexCoordinates(3, 1)] = los
        });

        // Assert
        sut.BoundaryPenFor(movement).Brush.ShouldBeSameAs(_movementStroke);
        sut.BoundaryPenFor(attack).Brush.ShouldBeSameAs(_attackStroke);
        sut.BoundaryPenFor(los).Brush.ShouldBeSameAs(_losStroke);
    });

    [Fact]
    public Task SetBoundaryOutlines_ShouldKeepThicknessPerOutline() => Dispatch(() =>
    {
        // Arrange
        var sut = CreateSut();
        var thin = new HighlightBoundaryOutline(
            0b000001, new AttackReachableHighlight(["PPC"]), 1);
        var thick = new HighlightBoundaryOutline(
            0b000010, new AttackReachableHighlight(["PPC"]), 4);

        // Act
        sut.SetBoundaryOutlines(new Dictionary<HexCoordinates, HighlightBoundaryOutline>
        {
            [new HexCoordinates(1, 1)] = thin,
            [new HexCoordinates(2, 1)] = thick
        });

        // Assert - same highlight type, two pens, because thickness is part of the key
        sut.BoundaryPenFor(thin).Thickness.ShouldBe(1);
        sut.BoundaryPenFor(thick).Thickness.ShouldBe(4);
        sut.BoundaryPenFor(thin).Brush.ShouldBeSameAs(_attackStroke);
        sut.BoundaryPenFor(thick).Brush.ShouldBeSameAs(_attackStroke);
    });

    [Fact]
    public Task SetBoundaryOutlines_ShouldFallBackToWhite_ForAnUnknownHighlightType() => Dispatch(() =>
    {
        // Arrange
        var sut = CreateSut();
        var unknown = new HighlightBoundaryOutline(0b000001, new UnknownHighlight(), 2);

        // Act
        sut.SetBoundaryOutlines(new Dictionary<HexCoordinates, HighlightBoundaryOutline>
        {
            [new HexCoordinates(1, 1)] = unknown
        });

        // Assert
        sut.BoundaryPenFor(unknown).Brush.ShouldBeSameAs(_whiteStroke);
    });

    [Fact]
    public Task SetBoundaryOutlines_ShouldDropPreviousPens_WhenOutlinesAreReplaced() => Dispatch(() =>
    {
        // Arrange
        var sut = CreateSut();
        var attack = new HighlightBoundaryOutline(
            0b000001, new AttackReachableHighlight(["PPC"]), 2);
        sut.SetBoundaryOutlines(new Dictionary<HexCoordinates, HighlightBoundaryOutline>
        {
            [new HexCoordinates(1, 1)] = attack
        });

        // Act
        sut.SetBoundaryOutlines(null);

        // Assert
        Should.Throw<KeyNotFoundException>(() => sut.BoundaryPenFor(attack));
    });

    private sealed record UnknownHighlight : IHexHighlightType
    {
        public int RenderOrder => 42;
        public string Name => nameof(UnknownHighlight);
        public string Render(Sanet.MakaMek.Localization.ILocalizationService localizationService) => Name;
    }
}
