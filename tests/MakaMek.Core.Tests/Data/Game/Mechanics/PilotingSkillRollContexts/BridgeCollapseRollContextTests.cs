using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Data.Game.Mechanics.PilotingSkillRollContexts;
using Sanet.MakaMek.Localization;
using Shouldly;
using Xunit;

namespace Sanet.MakaMek.Core.Tests.Data.Game.Mechanics.PilotingSkillRollContexts;

public class BridgeCollapseRollContextTests
{
    private readonly ILocalizationService _localizationService = new FakeLocalizationService();

    [Fact]
    public void Constructor_WhenCalled_SetsBridgeHeight()
    {
        const int bridgeHeight = 3;

        var sut = new BridgeCollapseRollContext(bridgeHeight);

        sut.BridgeHeight.ShouldBe(bridgeHeight);
    }

    [Fact]
    public void Constructor_WhenCalled_SetsRollTypeToBridgeCollapse()
    {
        var sut = new BridgeCollapseRollContext(5);

        sut.RollType.ShouldBe(PilotingSkillRollType.BridgeCollapse);
    }

    [Fact]
    public void Render_WhenCalled_ReturnsLocalizedStringWithHeight()
    {
        var sut = new BridgeCollapseRollContext(3);

        var result = sut.Render(_localizationService);

        result.ShouldBe("Bridge Collapse (Height: 3)");
    }

    [Theory]
    [InlineData(1, "Bridge Collapse (Height: 1)")]
    [InlineData(2, "Bridge Collapse (Height: 2)")]
    [InlineData(5, "Bridge Collapse (Height: 5)")]
    [InlineData(10, "Bridge Collapse (Height: 10)")]
    public void Render_WithDifferentHeights_ReturnsCorrectLocalizedString(int bridgeHeight, string expected)
    {
        var sut = new BridgeCollapseRollContext(bridgeHeight);

        var result = sut.Render(_localizationService);

        result.ShouldBe(expected);
    }
}
