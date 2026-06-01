using NSubstitute;
using Sanet.MakaMek.Localization;
using Sanet.MakaMek.Map.Models.MovementCosts;
using Shouldly;

namespace Sanet.MakaMek.Map.Tests.Models.MovementCosts;

public class StandUpAttemptMovementCostTests
{
    [Fact]
    public void Render_ReturnsFormattedString()
    {
        var localization = Substitute.For<ILocalizationService>();
        localization.GetString("MovementCost_StandUpAttempt").Returns("stand up attempt, {0} MP");
        var sut = new StandUpAttemptMovementCost { Value = 2 };

        var result = sut.Render(localization);

        result.ShouldBe("stand up attempt, 2 MP");
    }
}
