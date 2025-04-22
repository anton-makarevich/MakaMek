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
        _converter.Convert(new[] { 5 }, typeof(string), null, _culture).ShouldBe("5");
    }

    [Fact]
    public void Convert_ConsecutiveSlots_ReturnsRange()
    {
        _converter.Convert(new[] { 2, 3, 4 }, typeof(string), null, _culture).ShouldBe("2-4");
        _converter.Convert(new[] { 4, 2, 3 }, typeof(string), null, _culture).ShouldBe("2-4");
    }

    [Fact]
    public void Convert_NonConsecutiveSlots_ReturnsCommaSeparated()
    {
        _converter.Convert(new[] { 1, 3, 5 }, typeof(string), null, _culture).ShouldBe("1,3,5");
        _converter.Convert(new[] { 7, 2, 5 }, typeof(string), null, _culture).ShouldBe("2,5,7");
    }

    [Fact]
    public void ConvertBack_Throws()
    {
        Should.Throw<NotSupportedException>(() => _converter.ConvertBack("1-3", typeof(int[]), null, _culture));
    }
}
