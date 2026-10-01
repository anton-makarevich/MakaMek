using System.Globalization;
using Avalonia;
using Avalonia.Data;
using Sanet.MakaMek.Avalonia.Converters;
using Shouldly;

namespace MakaMek.Avalonia.Tests.Converters;

public class RecordSheetMarginConverterTests
{
    private readonly RecordSheetMarginConverter _sut = new();

    private Thickness Convert(bool compact, bool barShowing, object? barHeight) =>
        (Thickness)_sut.Convert([compact, barShowing, barHeight], typeof(Thickness), null,
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
    public void Convert_IgnoresTheBar_OnADesktopLayout()
    {
        // The drawer is pinned top right there, so the bottom edge is not shared.
        Convert(compact: false, barShowing: true, 108d).ShouldBe(new Thickness(0, 60, 88, 80));
    }

    [Fact]
    public void Convert_IsUnset_WithoutEveryValue()
    {
        _sut.Convert([true, true], typeof(Thickness), null, CultureInfo.InvariantCulture)
            .ShouldBe(AvaloniaProperty.UnsetValue);
    }

    [Fact]
    public void ConvertBack_DoesNothing()
    {
        _sut.ConvertBack([new Thickness(1)], typeof(bool), null, CultureInfo.InvariantCulture)
            .ShouldBe(BindingOperations.DoNothing);
    }
}
