using NSubstitute;
using Sanet.MakaMek.Localization;
using Sanet.MakaMek.Map.Models.MovementCosts;
using Shouldly;

namespace Sanet.MakaMek.Map.Tests.Models.MovementCosts;

public class JumpMovementCostTests
{
    [Fact]
    public void Render_ReturnsFormattedString()
    {
        var localization = Substitute.For<ILocalizationService>();
        localization.GetString("MovementCost_Jump").Returns("jump, {0} MP");
        var sut = new JumpMovementCost { Value = 5 };

        var result = sut.Render(localization);

        result.ShouldBe("jump, 5 MP");
    }
}
