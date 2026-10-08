using System.Globalization;
using Avalonia;
using Avalonia.Data;
using Sanet.MakaMek.Avalonia.Converters;
using Shouldly;

namespace MakaMek.Avalonia.Tests.Converters;

public class UtilityPanelMarginConverterTests
{
    private readonly UtilityPanelMarginConverter _sut = new();

    private Thickness Convert(bool barShowing, object? barHeight) =>
        (Thickness)_sut.Convert([barShowing, barHeight], typeof(Thickness), null,
            CultureInfo.InvariantCulture);

    [Fact]
    public void Convert_LiftsThePanelClearOfTheSquadBar()
    {
        // GamePanel draws against the bottom right corner, which is where the bar already is.
        Convert(barShowing: true, 108d).Bottom.ShouldBe(118);
    }

    [Fact]
    public void Convert_FollowsTheBarHeight()
    {
        Convert(true, 140d).Bottom.ShouldBeGreaterThan(Convert(true, 108d).Bottom);
    }

    [Theory]
    [InlineData(false, 108d)]
    [InlineData(true, 0d)]
    [InlineData(true, null)]
    [InlineData(true, double.NaN)]
    public void Convert_AddsNothing_WhenThereIsNoBarToClear(bool barShowing, object? barHeight)
    {
        Convert(barShowing, barHeight).Bottom.ShouldBe(0);
    }

    [Fact]
    public void Convert_LeavesTheOtherEdgesToTheTemplate()
    {
        var margin = Convert(true, 108d);

        margin.Left.ShouldBe(0);
        margin.Top.ShouldBe(0);
        margin.Right.ShouldBe(0);
    }

    [Fact]
    public void Convert_IsUnset_WithoutEveryValue()
    {
        _sut.Convert([true], typeof(Thickness), null, CultureInfo.InvariantCulture)
            .ShouldBe(AvaloniaProperty.UnsetValue);
    }

    [Fact]
    public void ConvertBack_DoesNothing()
    {
        _sut.ConvertBack([new Thickness(1)], typeof(bool), null, CultureInfo.InvariantCulture)
            .ShouldBe(BindingOperations.DoNothing);
    }
}
