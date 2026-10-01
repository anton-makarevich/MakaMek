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
    public void Render_ShouldJoinWeaponNamesWithComma()
    {
        var sut = new AttackReachableHighlight(["PPC", "ML"]);

        sut.Render(_localization).ShouldBe("PPC, ML");
    }

    /// <summary>
    /// The band is all the model carries. What each one looks like is the renderer's business,
    /// resolved from Colors.axaml, which is where #1520 moved highlight colours.
    /// </summary>
    [Theory]
    [InlineData(AttackRangeBand.Short)]
    [InlineData(AttackRangeBand.Medium)]
    [InlineData(AttackRangeBand.Long)]
    [InlineData(AttackRangeBand.Mixed)]
    public void ShouldCarryTheRangeBandItWasGiven(AttackRangeBand rangeBand)
    {
        new AttackReachableHighlight([], rangeBand).RangeBand.ShouldBe(rangeBand);
    }

    [Fact]
    public void ShouldDefaultToTheMediumBand()
    {
        // Medium keeps the existing attack colour, so an unbanded highlight looks unchanged.
        new AttackReachableHighlight([]).RangeBand.ShouldBe(AttackRangeBand.Medium);
    }

    [Fact]
    public void Render_ShouldPreferTacticalText_WhenProvided()
    {
        var sut = new AttackReachableHighlight(["PPC"], TacticalText: "PPC: 58% / 4.1");

        sut.Render(_localization).ShouldBe("PPC: 58% / 4.1");
    }
}
