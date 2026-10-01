using System.Globalization;
using Avalonia.Data;
using Sanet.MakaMek.Avalonia.Converters;
using Shouldly;

namespace MakaMek.Avalonia.Tests.Converters;

public class UnitIntegrityPercentConverterTests
{
    private readonly UnitIntegrityPercentConverter _sut = new();

    private string Convert(params object?[] values) =>
        (string)_sut.Convert(values, typeof(string), null, CultureInfo.InvariantCulture);

    [Theory]
    [InlineData(0, 10, "0%")]
    [InlineData(5, 10, "50%")]
    [InlineData(10, 10, "100%")]
    [InlineData(1, 3, "33%")]
    [InlineData(2, 3, "67%")]
    public void Convert_RoundsThePairToAWholePercent(int current, int maximum, string expected)
        => Convert(current, maximum).ShouldBe(expected);

    [Fact]
    public void Convert_ClampsAbove100()
        => Convert(12, 10).ShouldBe("100%");

    [Fact]
    public void Convert_ClampsBelowZero()
        => Convert(-5, 10).ShouldBe("0%");

    [Theory]
    [InlineData(5, 0)]
    [InlineData(5, -1)]
    public void Convert_ReturnsZero_WhenThereIsNoMaximumToDivideBy(int current, int maximum)
        => Convert(current, maximum).ShouldBe("0%");

    [Fact]
    public void Convert_ReturnsZero_WhenABindingHasNotProducedBothValues()
        => Convert(5).ShouldBe("0%");

    [Theory]
    [InlineData(null, 10)]
    [InlineData(5, null)]
    [InlineData("5", "10")]
    [InlineData(5d, 10d)]
    public void Convert_ReturnsZero_ForAnythingThatIsNotAPairOfInts(object? current, object? maximum)
        => Convert(current, maximum).ShouldBe("0%");

    [Fact]
    public void ConvertBack_LeavesTheSourceAlone()
        => _sut.ConvertBack(["50%"], typeof(int), null, CultureInfo.InvariantCulture)
            .ShouldBe(BindingOperations.DoNothing);
}
