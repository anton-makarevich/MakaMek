using System.Globalization;
using Avalonia;
using Avalonia.Data;
using Sanet.MakaMek.Avalonia.Converters;
using Shouldly;

namespace MakaMek.Avalonia.Tests.Converters;

public class SquadBarMarginConverterTests
{
    private readonly SquadBarMarginConverter _sut = new();

    private Thickness Convert(bool compact, object? drawerWidth) =>
        (Thickness)_sut.Convert([compact, drawerWidth], typeof(Thickness), null, CultureInfo.InvariantCulture);

    [Fact]
    public void Convert_TakesTheWholeWidth_WhenTheDrawerIsClosed()
    {
        // A collapsed drawer measures zero, so there is nothing to keep clear of.
        Convert(false, 0d).Right.ShouldBe(12);
    }

    [Fact]
    public void Convert_ReservesTheDrawerPlusAGutter_WhenItIsOpen()
    {
        Convert(false, 145d).Right.ShouldBe(157);
    }

    [Fact]
    public void Convert_FollowsTheDrawerWidth_SoALongerLabelCannotOverlap()
    {
        Convert(false, 220d).Right.ShouldBeGreaterThan(Convert(false, 145d).Right);
    }

    [Fact]
    public void Convert_ReservesNothing_WhenCompact()
    {
        // The compact layout puts the drawer at the top, leaving the bottom edge to the bar.
        Convert(true, 145d).Right.ShouldBe(12);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not a width")]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(-5d)]
    public void Convert_TreatsAnUnusableWidthAsNoDrawer(object? width)
    {
        Convert(false, width).Right.ShouldBe(12);
    }

    [Fact]
    public void Convert_KeepsTheEdgeAndBottomInsets()
    {
        var margin = Convert(false, 145d);

        margin.Left.ShouldBe(12);
        margin.Top.ShouldBe(0);
        margin.Bottom.ShouldBe(10);
    }

    [Fact]
    public void Convert_IsUnset_WithoutBothValues()
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
