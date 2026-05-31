using NSubstitute;
using Sanet.MakaMek.Localization;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Models.MovementCosts;
using Sanet.MakaMek.Map.Models.Terrains;
using Shouldly;

namespace Sanet.MakaMek.Map.Tests.Models;

public class MovementCostRenderTests
{
    [Fact]
    public void TerrainMovementCost_Render_ReturnsFormattedString()
    {
        var localization = Substitute.For<ILocalizationService>();
        localization.GetString("MovementCost_Terrain").Returns("entered {0}, {1} MP");
        var cost = new TerrainMovementCost { TerrainId = MakaMekTerrains.LightWoods, Value = 2 };

        var result = cost.Render(localization);

        result.ShouldBe("entered LightWoods, 2 MP");
    }

    [Fact]
    public void RotationMovementCost_Render_ReturnsFormattedString()
    {
        var localization = Substitute.For<ILocalizationService>();
        localization.GetString("MovementCost_Rotation").Returns("rotated {0} side(s), {1} MP");
        var cost = new RotationMovementCost { FromFacing = HexDirection.Top, ToFacing = HexDirection.TopRight, Value = 1 };

        var result = cost.Render(localization);

        result.ShouldBe("rotated 1 side(s), 1 MP");
    }

    [Fact]
    public void RotationMovementCost_Render_WithThreeSideRotation_ReturnsCorrectSides()
    {
        var localization = Substitute.For<ILocalizationService>();
        localization.GetString("MovementCost_Rotation").Returns("rotated {0} side(s), {1} MP");
        var cost = new RotationMovementCost { FromFacing = HexDirection.Top, ToFacing = HexDirection.Bottom, Value = 3 };

        var result = cost.Render(localization);

        result.ShouldBe("rotated 3 side(s), 3 MP");
    }

    [Fact]
    public void RotationMovementCost_Render_WithWrappingRotation_ReturnsMinSides()
    {
        var localization = Substitute.For<ILocalizationService>();
        localization.GetString("MovementCost_Rotation").Returns("rotated {0} side(s), {1} MP");
        var cost = new RotationMovementCost { FromFacing = HexDirection.Top, ToFacing = HexDirection.BottomLeft, Value = 2 };

        var result = cost.Render(localization);

        result.ShouldBe("rotated 2 side(s), 2 MP");
    }

    [Fact]
    public void ElevationChangeMovementCost_Render_ReturnsFormattedString()
    {
        var localization = Substitute.For<ILocalizationService>();
        localization.GetString("MovementCost_ElevationChange").Returns("elevation change ({0:+#;-#;0}), {1} MP");
        var cost = new ElevationChangeMovementCost { ElevationDelta = 2, Value = 1 };

        var result = cost.Render(localization);

        result.ShouldBe("elevation change (+2), 1 MP");
    }

    [Fact]
    public void ElevationChangeMovementCost_Render_WithNegativeDelta_ReturnsFormattedString()
    {
        var localization = Substitute.For<ILocalizationService>();
        localization.GetString("MovementCost_ElevationChange").Returns("elevation change ({0:+#;-#;0}), {1} MP");
        var cost = new ElevationChangeMovementCost { ElevationDelta = -1, Value = 1 };

        var result = cost.Render(localization);

        result.ShouldBe("elevation change (-1), 1 MP");
    }

    [Fact]
    public void JumpMovementCost_Render_ReturnsFormattedString()
    {
        var localization = Substitute.For<ILocalizationService>();
        localization.GetString("MovementCost_Jump").Returns("jump, {0} MP");
        var cost = new JumpMovementCost { Value = 5 };

        var result = cost.Render(localization);

        result.ShouldBe("jump, 5 MP");
    }

    [Fact]
    public void StandUpAttemptMovementCost_Render_ReturnsFormattedString()
    {
        var localization = Substitute.For<ILocalizationService>();
        localization.GetString("MovementCost_StandUpAttempt").Returns("stand up attempt, {0} MP");
        var cost = new StandUpAttemptMovementCost { Value = 2 };

        var result = cost.Render(localization);

        result.ShouldBe("stand up attempt, 2 MP");
    }
}
