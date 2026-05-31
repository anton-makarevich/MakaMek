using Sanet.MakaMek.Map.Models;
using Shouldly;

namespace Sanet.MakaMek.Map.Tests.Models;

public class SegmentEventTests
{
    [Fact]
    public void Constructor_SetsType()
    {
        var sut = new SegmentEvent(SegmentEventType.Fall);

        sut.Type.ShouldBe(SegmentEventType.Fall);
    }

    [Fact]
    public void Constructor_WithStandupAttempt_SetsCorrectType()
    {
        var sut = new SegmentEvent(SegmentEventType.StandupAttempt);

        sut.Type.ShouldBe(SegmentEventType.StandupAttempt);
    }

    [Fact]
    public void Equals_SameValues_ReturnsTrue()
    {
        var a = new SegmentEvent(SegmentEventType.Fall);
        var b = new SegmentEvent(SegmentEventType.Fall);

        a.Equals(b).ShouldBeTrue();
    }

    [Fact]
    public void Equals_DifferentType_ReturnsFalse()
    {
        var a = new SegmentEvent(SegmentEventType.Fall);
        var b = new SegmentEvent(SegmentEventType.StandupAttempt);

        a.Equals(b).ShouldBeFalse();
    }

    [Fact]
    public void OperatorEquals_SameValues_ReturnsTrue()
    {
        var a = new SegmentEvent(SegmentEventType.Fall);
        var b = new SegmentEvent(SegmentEventType.Fall);

        (a == b).ShouldBeTrue();
    }

    [Fact]
    public void OperatorNotEquals_DifferentValues_ReturnsTrue()
    {
        var a = new SegmentEvent(SegmentEventType.Fall);
        var b = new SegmentEvent(SegmentEventType.StandupAttempt);

        (a != b).ShouldBeTrue();
    }

    [Fact]
    public void ToString_ReturnsFormattedString()
    {
        var sut = new SegmentEvent(SegmentEventType.Fall);

        var result = sut.ToString();

        result.ShouldContain("Fall");
    }
}
