using System.Globalization;
using Sanet.MakaMek.Avalonia.Converters;
using Shouldly;

namespace MakaMek.Avalonia.Tests.Converters;

public class SlotsRangeConverterTests
{
    private readonly SlotsRangeConverter _converter = new();
    private readonly CultureInfo _culture = CultureInfo.InvariantCulture;

    [Fact]
    public void Convert_NullOrEmpty_ReturnsDash()
    {
        _converter.Convert(null, typeof(string), null, _culture).ShouldBe("-");
        _converter.Convert(Array.Empty<int>(), typeof(string), null, _culture).ShouldBe("-");
    }

    [Fact]
    public void Convert_SingleSlot_ReturnsSlot()
    {
        _converter.Convert((int[]) [4], typeof(string), null, _culture).ShouldBe("5");
    }

    [Fact]
    public void Convert_ConsecutiveSlots_ReturnsRange()
    {
        _converter.Convert((int[]) [1, 2, 3], typeof(string), null, _culture).ShouldBe("2-4");
        _converter.Convert((int[]) [3, 2, 1], typeof(string), null, _culture).ShouldBe("2-4");
    }

    [Fact]
    public void Convert_NonConsecutiveSlots_ReturnsCommaSeparated()
    {
        _converter.Convert((int[]) [0, 2, 4], typeof(string), null, _culture).ShouldBe("1,3,5");
        _converter.Convert((int[]) [6, 1, 4], typeof(string), null, _culture).ShouldBe("2,5,7");
    }

    [Fact]
    public void Convert_ZeroBasedSlots_AreDisplayedAsOneBased()
    {
        _converter.Convert((int[]) [0], typeof(string), null, _culture).ShouldBe("1");
        _converter.Convert((int[]) [0, 1, 2], typeof(string), null, _culture).ShouldBe("1-3");
        _converter.Convert((int[]) [1, 2, 3], typeof(string), null, _culture).ShouldBe("2-4");
    }

    [Fact]
    public void Convert_MultipleRanges_AreDisplayedCorrectly()
    {
        _converter.Convert((int[]) [0, 1, 2, 7, 8, 9], typeof(string), null, _culture).ShouldBe("1-3,8-10");
        _converter.Convert((int[]) [0, 2, 4, 5, 6], typeof(string), null, _culture).ShouldBe("1,3,5-7");
    }

    [Fact]
    public void ConvertBack_Throws()
    {
        Should.Throw<NotSupportedException>(() => _converter.ConvertBack("1-3", typeof(int[]), null, _culture));
    }
}
