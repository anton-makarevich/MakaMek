using NSubstitute;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.PilotingSkill;
using Sanet.MakaMek.Localization;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Mechanics.Modifiers.PilotingSkill;

public class SkiddingModifierTests
{
    private readonly ILocalizationService _localizationService = Substitute.For<ILocalizationService>();

    [Fact]
    public void Render_ShouldFormatCorrectly()
    {
        var sut = new SkiddingModifier
        {
            Value = 2,
            HexesMoved = 5
        };
        _localizationService.GetString("Modifier_SkidDistance").Returns("Skid ({0} hexes moved): {1}");

        var result = sut.Render(_localizationService);

        result.ShouldBe("Skid (5 hexes moved): +2");
        _localizationService.Received(1).GetString("Modifier_SkidDistance");
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(5, 5)]
    [InlineData(10, 10)]
    [InlineData(25, 25)]
    public void HexesMoved_ShouldBeSetCorrectly(int hexesMoved, int expectedHexesMoved)
    {
        var sut = new SkiddingModifier
        {
            Value = 1,
            HexesMoved = hexesMoved
        };

        sut.HexesMoved.ShouldBe(expectedHexesMoved);
    }

    [Theory]
    [InlineData(-1, -1)]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(6, 6)]
    public void Value_ShouldBeSetCorrectly(int value, int expectedValue)
    {
        var sut = new SkiddingModifier
        {
            Value = value,
            HexesMoved = 5
        };

        sut.Value.ShouldBe(expectedValue);
    }

    [Theory]
    [InlineData(3, 0)]
    [InlineData(5, 1)]
    [InlineData(8, 2)]
    [InlineData(11, 4)]
    public void Render_WithDifferentHexesMovedAndValues_ShouldFormatCorrectly(int hexesMoved, int value)
    {
        var sut = new SkiddingModifier
        {
            Value = value,
            HexesMoved = hexesMoved
        };
        _localizationService.GetString("Modifier_SkidDistance").Returns("Skid ({0} hexes moved): {1}");

        var result = sut.Render(_localizationService);

        result.ShouldBe($"Skid ({hexesMoved} hexes moved): +{value}");
    }

    [Fact]
    public void Render_WithNegativeValue_ShouldFormatCorrectly()
    {
        var sut = new SkiddingModifier
        {
            Value = -1,
            HexesMoved = 1
        };
        _localizationService.GetString("Modifier_SkidDistance").Returns("Skid ({0} hexes moved): {1}");

        var result = sut.Render(_localizationService);

        result.ShouldBe("Skid (1 hexes moved): -1");
    }
}
