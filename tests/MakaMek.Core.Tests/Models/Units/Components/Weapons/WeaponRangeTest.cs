using JetBrains.Annotations;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Units.Components.Weapons;

[TestSubject(typeof(WeaponRange))]
public class WeaponRangeTest
{
    private readonly WeaponRange _sut = new(MinimumRange: 3, ShortRange: 6, MediumRange: 12, LongRange: 18);

    [Theory]
    [InlineData(-1, RangeBracket.OutOfRange)]
    [InlineData(0, RangeBracket.OutOfRange)]
    [InlineData(1, RangeBracket.Minimum)]
    [InlineData(3, RangeBracket.Minimum)]
    [InlineData(4, RangeBracket.Short)]
    [InlineData(6, RangeBracket.Short)]
    [InlineData(7, RangeBracket.Medium)]
    [InlineData(12, RangeBracket.Medium)]
    [InlineData(13, RangeBracket.Long)]
    [InlineData(18, RangeBracket.Long)]
    [InlineData(19, RangeBracket.OutOfRange)]
    public void GetRangeBracket_WithDistance_ReturnsCorrectBracket(int distance, RangeBracket expected)
    {
        var result = _sut.GetRangeBracket(distance);

        result.ShouldBe(expected);
    }

    [Fact]
    public void GetRangeBracket_WithNoMinimumRange_ReturnsShortForDistanceOne()
    {
        var range = new WeaponRange(MinimumRange: 0, ShortRange: 6, MediumRange: 12, LongRange: 18);

        range.GetRangeBracket(1).ShouldBe(RangeBracket.Short);
    }

    [Fact]
    public void GetRangeBracket_WithMinimumRange_ReturnsMinimumForDistanceOne()
    {
        _sut.GetRangeBracket(1).ShouldBe(RangeBracket.Minimum);
    }

    [Fact]
    public void GetRangeBracket_WithZeroDistance_ReturnsOutOfRange()
    {
        _sut.GetRangeBracket(0).ShouldBe(RangeBracket.OutOfRange);
    }

    [Theory]
    [InlineData(RangeBracket.Minimum, 3)]
    [InlineData(RangeBracket.Short, 6)]
    [InlineData(RangeBracket.Medium, 12)]
    [InlineData(RangeBracket.Long, 18)]
    public void GetRangeValue_WithBracket_ReturnsCorrectValue(RangeBracket bracket, int expected)
    {
        var result = _sut.GetRangeValue(bracket);

        result.ShouldBe(expected);
    }

    [Fact]
    public void GetRangeValue_WithOutOfRange_ReturnsLongRangePlusOne()
    {
        var result = _sut.GetRangeValue(RangeBracket.OutOfRange);

        result.ShouldBe(_sut.LongRange + 1);
    }

    [Fact]
    public void GetRangeValue_WithUnknownBracket_ThrowsArgumentException()
    {
        const RangeBracket unknown = (RangeBracket)99;

        Should.Throw<ArgumentException>(() => _sut.GetRangeValue(unknown));
    }
}