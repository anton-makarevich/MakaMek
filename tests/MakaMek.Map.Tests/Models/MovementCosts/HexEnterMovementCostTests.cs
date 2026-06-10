using NSubstitute;
using Sanet.MakaMek.Localization;
using Sanet.MakaMek.Map.Models.MovementCosts;
using Shouldly;

namespace Sanet.MakaMek.Map.Tests.Models.MovementCosts;

public class HexEnterMovementCostTests
{
    [Fact]
    public void Value_Returns_SetValue()
    {
        var sut = new HexEnterMovementCost { Value = 1 };

        sut.Value.ShouldBe(1);
    }

    [Fact]
    public void Render_ReturnsFormattedString()
    {
        var localization = Substitute.For<ILocalizationService>();
        localization.GetString("MovementCost_HexEnter").Returns("entered hex, {0} MP");
        var sut = new HexEnterMovementCost { Value = 1 };

        var result = sut.Render(localization);

        result.ShouldBe("entered hex, 1 MP");
    }
}
