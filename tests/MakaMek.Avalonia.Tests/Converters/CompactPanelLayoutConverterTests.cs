using System.Globalization;
using Avalonia;
using Avalonia.Layout;
using Sanet.MakaMek.Avalonia.Converters;
using Shouldly;

namespace MakaMek.Avalonia.Tests.Converters;

public class CompactPanelLayoutConverterTests
{
    [Fact]
    public void Convert_CompactMode_UsesBottomSheetValues()
    {
        var converter = new CompactPanelLayoutConverter();

        converter.Convert(true, typeof(object), "horizontal", CultureInfo.InvariantCulture).ShouldBe(HorizontalAlignment.Stretch);
        converter.Convert(true, typeof(object), "vertical", CultureInfo.InvariantCulture).ShouldBe(VerticalAlignment.Bottom);
        converter.Convert(true, typeof(object), "margin", CultureInfo.InvariantCulture).ShouldBe(new Thickness(8, 56, 8, 88));
        converter.Convert(true, typeof(object), "maxHeight", CultureInfo.InvariantCulture).ShouldBe(500d);
        converter.Convert(true, typeof(object), "controlsVertical", CultureInfo.InvariantCulture).ShouldBe(VerticalAlignment.Top);
        converter.Convert(true, typeof(object), "controlsMargin", CultureInfo.InvariantCulture).ShouldBe(new Thickness(8, 60, 8, 0));
        converter.Convert(true, typeof(object), "squadMargin", CultureInfo.InvariantCulture).ShouldBe(new Thickness(12, 0, 12, 10));
        converter.Convert(true, typeof(object), "actionMargin", CultureInfo.InvariantCulture).ShouldBe(new Thickness(0, 0, 0, 96));
    }

    [Fact]
    public void Convert_DesktopMode_PreservesRightDrawerValues()
    {
        var converter = new CompactPanelLayoutConverter();

        converter.Convert(false, typeof(object), "horizontal", CultureInfo.InvariantCulture).ShouldBe(HorizontalAlignment.Right);
        converter.Convert(false, typeof(object), "vertical", CultureInfo.InvariantCulture).ShouldBe(VerticalAlignment.Top);
        converter.Convert(false, typeof(object), "margin", CultureInfo.InvariantCulture).ShouldBe(new Thickness(0, 60, 88, 80));
        converter.Convert(false, typeof(object), "maxWidth", CultureInfo.InvariantCulture).ShouldBe(420d);
        converter.Convert(false, typeof(object), "controlsVertical", CultureInfo.InvariantCulture).ShouldBe(VerticalAlignment.Bottom);
        converter.Convert(false, typeof(object), "controlsMargin", CultureInfo.InvariantCulture).ShouldBe(new Thickness(0, 0, 12, 20));
        converter.Convert(false, typeof(object), "squadMargin", CultureInfo.InvariantCulture).ShouldBe(new Thickness(12, 0, 0, 10));
        converter.Convert(false, typeof(object), "actionMargin", CultureInfo.InvariantCulture).ShouldBe(new Thickness(0, 0, 0, 20));
    }
}
