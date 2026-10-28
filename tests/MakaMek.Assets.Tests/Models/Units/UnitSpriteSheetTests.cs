using Sanet.MakaMek.Assets.Models.Units;
using Shouldly;

namespace Sanet.MakaMek.Assets.Tests.Models.Units;

public class UnitSpriteSheetTests
{
    private static UnitSpriteSheet CreateSheet(
        int cellWidth = 16,
        int cellHeight = 18,
        int padding = 2,
        Dictionary<string, UnitSpriteAnimation>? animations = null) =>
        new()
        {
            CellWidth = cellWidth,
            CellHeight = cellHeight,
            Padding = padding,
            Animations = animations ?? new Dictionary<string, UnitSpriteAnimation>
            {
                ["Walk"] = new() { Row = 0, Frames = 2, Fps = 12, Loop = true, DistancePerCycle = 21.5 }
            }
        };

    [Fact]
    public void IsValid_IsTrue_ForACompleteSheet()
    {
        CreateSheet().IsValid.ShouldBeTrue();
    }

    [Fact]
    public void IsValid_DefaultsPivotToCellCentre()
    {
        new UnitSpriteSheet().Pivot.ShouldBe(new[] { 0.5, 0.5 });
    }

    [Theory]
    [InlineData(0, 18, 2)]
    [InlineData(-1, 18, 2)]
    [InlineData(16, 0, 2)]
    [InlineData(16, -1, 2)]
    [InlineData(16, 18, -1)]
    public void IsValid_IsFalse_WhenCellDimensionsOrPaddingAreInvalid(
        int cellWidth, int cellHeight, int padding)
    {
        CreateSheet(cellWidth, cellHeight, padding).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void IsValid_IsFalse_WhenThereAreNoAnimations()
    {
        CreateSheet(animations: []).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void IsValid_IsFalse_WhenEveryAnimationHasNoFrames()
    {
        CreateSheet(animations: new Dictionary<string, UnitSpriteAnimation>
        {
            ["Walk"] = new() { Row = 0, Frames = 0 },
            ["Run"] = new() { Row = 1, Frames = 0 }
        }).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void IsValid_IsTrue_WhenAtLeastOneAnimationHasFrames()
    {
        CreateSheet(animations: new Dictionary<string, UnitSpriteAnimation>
        {
            ["Idle"] = new() { Row = 0, Frames = 0 },
            ["Walk"] = new() { Row = 1, Frames = 4 }
        }).IsValid.ShouldBeTrue();
    }
}
