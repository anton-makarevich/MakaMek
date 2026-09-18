using Sanet.MakaMek.Localization;
using Sanet.MakaMek.Map.Models.Highlights;
using Shouldly;

namespace Sanet.MakaMek.Map.Tests.Models.Highlights;

public class AttackReachableHighlightTests
{
    private readonly FakeLocalizationService _localization = new();

    [Fact]
    public void ShouldHaveCorrectRenderOrder()
    {
        // Arrange & Act
        var sut = new AttackReachableHighlight([]);

        // Assert
        sut.RenderOrder.ShouldBe(1);
    }

    [Fact]
    public void ShouldHaveCorrectName()
    {
        // Arrange & Act
        var sut = new AttackReachableHighlight([]);

        // Assert
        sut.Name.ShouldBe(nameof(AttackReachableHighlight));
    }

    [Fact]
    public void ShouldHaveCorrectBoundaryOutlineColor()
    {
        // Arrange & Act
        var sut = new AttackReachableHighlight([]);

        // Assert
        sut.BoundaryOutlineColor.ShouldBe("#FFB347");
    }

    [Fact]
    public void Render_ShouldJoinWeaponNamesWithComma()
    {
        var sut = new AttackReachableHighlight(["PPC", "ML"]);

        sut.Render(_localization).ShouldBe("PPC, ML");
    }

    [Theory]
    [InlineData(AttackRangeBand.Short, "#66E3FF")]
    [InlineData(AttackRangeBand.Medium, "#FFB347")]
    [InlineData(AttackRangeBand.Long, "#FF8C69")]
    [InlineData(AttackRangeBand.Mixed, "#C084FC")]
    public void BoundaryOutlineColor_ShouldIdentifyRangeBand(
        AttackRangeBand rangeBand,
        string expectedColor)
    {
        var sut = new AttackReachableHighlight([], rangeBand);

        sut.BoundaryOutlineColor.ShouldBe(expectedColor);
    }

    [Fact]
    public void Render_ShouldPreferTacticalText_WhenProvided()
    {
        var sut = new AttackReachableHighlight(["PPC"], TacticalText: "PPC: 58% / 4.1");

        sut.Render(_localization).ShouldBe("PPC: 58% / 4.1");
    }
}
