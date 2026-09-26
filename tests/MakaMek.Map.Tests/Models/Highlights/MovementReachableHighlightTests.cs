using Sanet.MakaMek.Localization;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Models.Highlights;
using Shouldly;

namespace Sanet.MakaMek.Map.Tests.Models.Highlights;

public class MovementReachableHighlightTests
{
    private readonly FakeLocalizationService _localization = new();

    [Fact]
    public void ShouldHaveCorrectRenderOrder()
    {
        // Arrange & Act
        var sut = new MovementReachableHighlight(MovementType.Walk);

        // Assert
        sut.RenderOrder.ShouldBe(0);
    }

    [Fact]
    public void ShouldHaveCorrectName()
    {
        // Arrange & Act
        var sut = new MovementReachableHighlight(MovementType.Walk);

        // Assert
        sut.Name.ShouldBe(nameof(MovementReachableHighlight));
    }

    [Theory]
    [InlineData(MovementType.Walk)]
    [InlineData(MovementType.Run)]
    [InlineData(MovementType.Jump)]
    [InlineData((MovementType)0)]
    public void Render_ShouldReturnLocalizedMovementType(MovementType type)
    {
        var sut = new MovementReachableHighlight(type);

        sut.Render(_localization).ShouldBe(type.ToString());
    }
}