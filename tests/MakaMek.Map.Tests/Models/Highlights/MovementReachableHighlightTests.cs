using Sanet.MakaMek.Map.Models.Highlights;
using Shouldly;

namespace Sanet.MakaMek.Map.Tests.Models.Highlights;

public class MovementReachableHighlightTests
{
    [Fact]
    public void ShouldHaveCorrectRenderOrder()
    {
        // Arrange & Act
        var sut = new MovementReachableHighlight();

        // Assert
        sut.RenderOrder.ShouldBe(0);
    }

    [Fact]
    public void ShouldHaveCorrectName()
    {
        // Arrange & Act
        var sut = new MovementReachableHighlight();

        // Assert
        sut.Name.ShouldBe(nameof(MovementReachableHighlight));
    }
}