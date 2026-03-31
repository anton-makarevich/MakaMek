using Sanet.MakaMek.Map.Models.Highlights;
using Shouldly;

namespace Sanet.MakaMek.Map.Tests.Models.Highlights;

public class AttackReachableHighlightTests
{
    [Fact]
    public void ShouldHaveCorrectRenderOrder()
    {
        // Arrange & Act
        var sut = new AttackReachableHighlight();

        // Assert
        sut.RenderOrder.ShouldBe(1);
    }

    [Fact]
    public void ShouldHaveCorrectName()
    {
        // Arrange & Act
        var sut = new AttackReachableHighlight();

        // Assert
        sut.Name.ShouldBe(nameof(AttackReachableHighlight));
    }
}