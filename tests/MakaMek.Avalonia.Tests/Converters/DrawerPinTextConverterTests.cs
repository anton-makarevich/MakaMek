using System.Globalization;
using Sanet.MakaMek.Avalonia.Converters;
using Shouldly;

namespace MakaMek.Avalonia.Tests.Converters;

public class DrawerPinTextConverterTests
{
    [Theory]
    [InlineData(false, "Pin drawer")]
    [InlineData(true, "Unpin drawer")]
    public void Convert_ReturnsStateAppropriateLabel(bool pinned, string expected)
        => new DrawerPinTextConverter().Convert(pinned, typeof(string), null, CultureInfo.InvariantCulture).ShouldBe(expected);
}
