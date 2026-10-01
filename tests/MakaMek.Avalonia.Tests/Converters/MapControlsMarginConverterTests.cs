using System.Globalization;
using Avalonia;
using Avalonia.Data;
using Sanet.MakaMek.Avalonia.Converters;
using Shouldly;

namespace MakaMek.Avalonia.Tests.Converters;

public class MapControlsMarginConverterTests
{
    private readonly MapControlsMarginConverter _sut = new();

    private Thickness Convert(bool compact, object? turnStatusHeight) =>
        (Thickness)_sut.Convert([compact, turnStatusHeight], typeof(Thickness), null,
            CultureInfo.InvariantCulture);

    /// <summary>
    /// The compact layout puts the column directly under the turn status bar, and that bar is 50
    /// points with the turn and phase on it and taller once the guidance labels fill in.
    /// </summary>
    [Fact]
    public void Convert_ClearsTheTurnStatusBar_OnACompactLayout()
    {
        Convert(compact: true, 75d).Top.ShouldBe(85);
    }

    [Fact]
    public void Convert_FollowsTheBarHeight()
    {
        Convert(true, 75d).Top.ShouldBeGreaterThan(Convert(true, 50d).Top);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0d)]
    [InlineData(double.NaN)]
    public void Convert_TreatsAnUnusableHeightAsNoBar(object? height)
    {
        Convert(true, height).Top.ShouldBe(10);
    }

    [Fact]
    public void Convert_PinsBottomRight_OnADesktopLayout()
    {
        // The column grows from the bottom edge there, so the bar above it is irrelevant.
        Convert(compact: false, 75d).ShouldBe(new Thickness(0, 0, 12, 20));
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
