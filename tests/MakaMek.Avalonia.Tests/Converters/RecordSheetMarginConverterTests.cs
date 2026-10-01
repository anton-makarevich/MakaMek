using System.Globalization;
using Avalonia;
using Avalonia.Data;
using Sanet.MakaMek.Avalonia.Converters;
using Shouldly;

namespace MakaMek.Avalonia.Tests.Converters;

public class RecordSheetMarginConverterTests
{
    private readonly RecordSheetMarginConverter _sut = new();

    private Thickness Convert(bool compact, bool barShowing, object? barHeight, double turnStatus = 50) =>
        (Thickness)_sut.Convert([compact, barShowing, barHeight, turnStatus], typeof(Thickness), null,
            CultureInfo.InvariantCulture);

    [Fact]
    public void Convert_ClearsTheSquadBar_OnACompactLayout()
    {
        // 88 used to be hardcoded here, against a bar that needs 118, so the drawer was drawn
        // across the top of the cards.
        Convert(compact: true, barShowing: true, 108d).Bottom.ShouldBe(118);
    }

    [Fact]
    public void Convert_FollowsTheBarHeight_SoAChangeCannotDriftOutOfStep()
    {
        Convert(true, true, 140d).Bottom.ShouldBeGreaterThan(Convert(true, true, 108d).Bottom);
    }

    [Theory]
    [InlineData(false, 108d)]
    [InlineData(true, 0d)]
    [InlineData(true, null)]
    public void Convert_TakesTheWholeEdge_WhenThereIsNoBarToClear(bool barShowing, object? barHeight)
    {
        Convert(true, barShowing, barHeight).Bottom.ShouldBe(88);
    }

    [Fact]
    public void Convert_IgnoresTheSquadBar_OnADesktopLayout()
    {
        // The drawer is pinned top right there, so the bottom edge is not shared.
        Convert(compact: false, barShowing: true, 108d).ShouldBe(new Thickness(0, 60, 88, 80));
    }

    /// <summary>
    /// The turn status bar is 50 points with the turn and phase on it and taller once the guidance
    /// labels fill in, so a constant top inset put the drawer inside it.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Convert_ClearsTheTurnStatusBar(bool compact)
    {
        Convert(compact, barShowing: true, 108d, turnStatus: 75).Top.ShouldBe(85);
    }

    [Fact]
    public void Convert_IsUnset_WithoutEveryValue()
    {
        _sut.Convert([true, true, 108d], typeof(Thickness), null, CultureInfo.InvariantCulture)
            .ShouldBe(AvaloniaProperty.UnsetValue);
    }

    [Fact]
    public void ConvertBack_DoesNothing()
    {
        _sut.ConvertBack([new Thickness(1)], typeof(bool), null, CultureInfo.InvariantCulture)
            .ShouldBe(BindingOperations.DoNothing);
    }
}
