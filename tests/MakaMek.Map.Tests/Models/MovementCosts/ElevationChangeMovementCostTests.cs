using NSubstitute;
using Sanet.MakaMek.Localization;
using Sanet.MakaMek.Map.Models.MovementCosts;
using Shouldly;

namespace Sanet.MakaMek.Map.Tests.Models.MovementCosts;

public class ElevationChangeMovementCostTests
{
    [Fact]
    public void Render_ReturnsFormattedString()
    {
        var localization = Substitute.For<ILocalizationService>();
        localization.GetString("MovementCost_ElevationChange").Returns("elevation change ({0:+#;-#;0}), {1} MP");
        var sut = new ElevationChangeMovementCost { ElevationDelta = 2, Value = 1 };

        var result = sut.Render(localization);

        result.ShouldBe("elevation change (+2), 1 MP");
    }

    [Fact]
    public void Render_WithNegativeDelta_ReturnsFormattedString()
    {
        var localization = Substitute.For<ILocalizationService>();
        localization.GetString("MovementCost_ElevationChange").Returns("elevation change ({0:+#;-#;0}), {1} MP");
        var cost = new ElevationChangeMovementCost { ElevationDelta = -1, Value = 1 };

        var result = cost.Render(localization);

        result.ShouldBe("elevation change (-1), 1 MP");
    }
}
