using Sanet.MakaMek.Localization;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Models.Highlights;
using Shouldly;

namespace Sanet.MakaMek.Map.Tests.Models.Highlights;

public class LosBlockingHighlightTests
{
    private readonly FakeLocalizationService _localization = new();

    [Fact]
    public void ShouldHaveCorrectRenderOrder()
    {
        // Arrange & Act
        var sut = new LosBlockingHighlight(LineOfSightBlockReason.InterveningTerrain);

        // Assert
        sut.RenderOrder.ShouldBe(2);
    }

    [Fact]
    public void ShouldHaveCorrectName()
    {
        // Arrange & Act
        var sut = new LosBlockingHighlight(LineOfSightBlockReason.InterveningTerrain);

        // Assert
        sut.Name.ShouldBe(nameof(LosBlockingHighlight));
    }

    [Fact]
    public void ShouldAcceptReasonInConstructor()
    {
        // Arrange & Act
        var sut = new LosBlockingHighlight(LineOfSightBlockReason.Elevation);

        // Assert
        sut.Reason.ShouldBe(LineOfSightBlockReason.Elevation);
    }

    [Fact]
    public void BlockingHex_ShouldStoreCoordinates_WhenProvided()
    {
        // Arrange & Act
        var coords = new HexCoordinates(3, 4);
        var sut = new LosBlockingHighlight(LineOfSightBlockReason.InterveningTerrain, coords);

        // Assert
        sut.BlockingHex.ShouldBe(coords);
    }

    [Fact]
    public void BlockingHex_ShouldBeNull_WhenNotProvided()
    {
        // Arrange & Act
        var sut = new LosBlockingHighlight(LineOfSightBlockReason.InterveningTerrain);

        // Assert
        sut.BlockingHex.ShouldBeNull();
    }

    [Fact]
    public void Render_InvalidCoordinates_ReturnsLocalizedMessage()
    {
        var sut = new LosBlockingHighlight(LineOfSightBlockReason.InvalidCoordinates);

        sut.Render(_localization).ShouldBe("Invalid coordinates");
    }

    [Fact]
    public void Render_Elevation_FormatsBlockingHex()
    {
        var hex = new HexCoordinates(3, 4);
        var sut = new LosBlockingHighlight(LineOfSightBlockReason.Elevation, hex);

        sut.Render(_localization).ShouldBe("Elevation at 0304");
    }

    [Fact]
    public void Render_InterveningTerrain_FormatsBlockingHex()
    {
        var hex = new HexCoordinates(1, 2);
        var sut = new LosBlockingHighlight(LineOfSightBlockReason.InterveningTerrain, hex);

        sut.Render(_localization).ShouldBe("Terrain at 0102");
    }

    [Fact]
    public void Render_InterveningTerrain_WithoutBlockingHex_FallsBackToInvalidCoordinatesMessage()
    {
        var sut = new LosBlockingHighlight(LineOfSightBlockReason.InterveningTerrain);

        sut.Render(_localization).ShouldBe("Invalid coordinates");
    }
}
