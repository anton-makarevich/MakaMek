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
}