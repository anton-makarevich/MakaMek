using NSubstitute;
using Sanet.MakaMek.Localization;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Models.MovementCosts;
using Shouldly;

namespace Sanet.MakaMek.Map.Tests.Models.MovementCosts;

public class RotationMovementCostTests
{
    [Fact]
    public void Render_ReturnsFormattedString()
    {
        var localization = Substitute.For<ILocalizationService>();
        localization.GetString("MovementCost_Rotation").Returns("rotated {0} side(s), {1} MP");
        var cost = new RotationMovementCost { FromFacing = HexDirection.Top, ToFacing = HexDirection.TopRight, Value = 1 };

        var result = cost.Render(localization);

        result.ShouldBe("rotated 1 side(s), 1 MP");
    }

    [Fact]
    public void Render_WithThreeSideRotation_ReturnsCorrectSides()
    {
        var localization = Substitute.For<ILocalizationService>();
        localization.GetString("MovementCost_Rotation").Returns("rotated {0} side(s), {1} MP");
        var sut = new RotationMovementCost { FromFacing = HexDirection.Top, ToFacing = HexDirection.Bottom, Value = 3 };

        var result = sut.Render(localization);

        result.ShouldBe("rotated 3 side(s), 3 MP");
    }

    [Fact]
    public void Render_WithWrappingRotation_ReturnsMinSides()
    {
        var localization = Substitute.For<ILocalizationService>();
        localization.GetString("MovementCost_Rotation").Returns("rotated {0} side(s), {1} MP");
        var sut = new RotationMovementCost { FromFacing = HexDirection.Top, ToFacing = HexDirection.BottomLeft, Value = 2 };

        var result = sut.Render(localization);

        result.ShouldBe("rotated 2 side(s), 2 MP");
    }
}
