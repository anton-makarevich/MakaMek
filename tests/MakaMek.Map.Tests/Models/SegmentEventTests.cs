using Sanet.MakaMek.Map.Models;
using Shouldly;

namespace Sanet.MakaMek.Map.Tests.Models;

public class SegmentEventTests
{
    [Fact]
    public void Constructor_SetsTypeAndCost()
    {
        var sut = new SegmentEvent(SegmentEventType.Fall, 5);

        sut.Type.ShouldBe(SegmentEventType.Fall);
        sut.Cost.ShouldBe(5);
    }

    [Fact]
    public void Constructor_WithStandupAttempt_SetsCorrectType()
    {
        var sut = new SegmentEvent(SegmentEventType.StandupAttempt, 3);

        sut.Type.ShouldBe(SegmentEventType.StandupAttempt);
        sut.Cost.ShouldBe(3);
    }

    [Fact]
    public void Deconstruct_ReturnsTypeAndCost()
    {
        var sut = new SegmentEvent(SegmentEventType.Fall, 7);

        var (type, cost) = sut;

        type.ShouldBe(SegmentEventType.Fall);
        cost.ShouldBe(7);
    }

    [Fact]
    public void Equals_SameValues_ReturnsTrue()
    {
        var a = new SegmentEvent(SegmentEventType.Fall, 5);
        var b = new SegmentEvent(SegmentEventType.Fall, 5);

        a.Equals(b).ShouldBeTrue();
    }

    [Fact]
    public void Equals_DifferentType_ReturnsFalse()
    {
        var a = new SegmentEvent(SegmentEventType.Fall, 5);
        var b = new SegmentEvent(SegmentEventType.StandupAttempt, 5);

        a.Equals(b).ShouldBeFalse();
    }

    [Fact]
    public void Equals_DifferentCost_ReturnsFalse()
    {
        var a = new SegmentEvent(SegmentEventType.Fall, 5);
        var b = new SegmentEvent(SegmentEventType.Fall, 10);

        a.Equals(b).ShouldBeFalse();
    }

    [Fact]
    public void OperatorEquals_SameValues_ReturnsTrue()
    {
        var a = new SegmentEvent(SegmentEventType.Fall, 5);
        var b = new SegmentEvent(SegmentEventType.Fall, 5);

        (a == b).ShouldBeTrue();
    }

    [Fact]
    public void OperatorNotEquals_DifferentValues_ReturnsTrue()
    {
        var a = new SegmentEvent(SegmentEventType.Fall, 5);
        var b = new SegmentEvent(SegmentEventType.StandupAttempt, 5);

        (a != b).ShouldBeTrue();
    }

    [Fact]
    public void ToString_ReturnsFormattedString()
    {
        var sut = new SegmentEvent(SegmentEventType.Fall, 5);

        var result = sut.ToString();

        result.ShouldContain("Fall");
        result.ShouldContain("5");
    }
}
