using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Models.MovementCosts;
using Shouldly;

namespace Sanet.MakaMek.Map.Tests.Models;

public class SegmentEventTests
{
    [Fact]
    public void Constructor_SetsTypeAndCost()
    {
        var sut = new SegmentEvent(SegmentEventType.Fall, []);

        sut.Type.ShouldBe(SegmentEventType.Fall);
        sut.Cost.ShouldBe(0);
    }

    [Fact]
    public void Constructor_WithStandupAttempt_SetsCorrectType()
    {
        var sut = new SegmentEvent(SegmentEventType.StandupAttempt, [new StandUpAttemptMovementCost { Value = 3 }]);

        sut.Type.ShouldBe(SegmentEventType.StandupAttempt);
        sut.Cost.ShouldBe(3);
    }

    [Fact]
    public void Deconstruct_ReturnsTypeAndCost()
    {
        var sut = new SegmentEvent(SegmentEventType.Fall, []);

        var (type, costs) = sut;

        type.ShouldBe(SegmentEventType.Fall);
        costs.ShouldBeEmpty();
    }

    [Fact]
    public void Equals_SameValues_ReturnsTrue()
    {
        var a = new SegmentEvent(SegmentEventType.Fall, []);
        var b = new SegmentEvent(SegmentEventType.Fall, []);

        a.Equals(b).ShouldBeTrue();
    }

    [Fact]
    public void Equals_DifferentType_ReturnsFalse()
    {
        var a = new SegmentEvent(SegmentEventType.Fall, []);
        var b = new SegmentEvent(SegmentEventType.StandupAttempt, [new StandUpAttemptMovementCost { Value = 5 }]);

        a.Equals(b).ShouldBeFalse();
    }

    [Fact]
    public void Equals_DifferentCost_ReturnsFalse()
    {
        var a = new SegmentEvent(SegmentEventType.Fall, []);
        var b = new SegmentEvent(SegmentEventType.Fall, [new StandUpAttemptMovementCost { Value = 5 }]);

        a.Equals(b).ShouldBeFalse();
    }

    [Fact]
    public void OperatorEquals_SameValues_ReturnsTrue()
    {
        var a = new SegmentEvent(SegmentEventType.Fall, []);
        var b = new SegmentEvent(SegmentEventType.Fall, []);

        (a == b).ShouldBeTrue();
    }

    [Fact]
    public void OperatorNotEquals_DifferentValues_ReturnsTrue()
    {
        var a = new SegmentEvent(SegmentEventType.Fall, []);
        var b = new SegmentEvent(SegmentEventType.StandupAttempt, [new StandUpAttemptMovementCost { Value = 5 }]);

        (a != b).ShouldBeTrue();
    }

    [Fact]
    public void ToString_ReturnsFormattedString()
    {
        var sut = new SegmentEvent(SegmentEventType.Fall, []);

        var result = sut.ToString();

        result.ShouldContain("Fall");
    }
}
