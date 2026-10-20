using NSubstitute;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Models.Terrains;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Map;

public class LineOfSightResultExtensionsTests
{
    private readonly IRulesProvider _rules = Substitute.For<IRulesProvider>();

    private static LineOfSightResult CreateLosResult(params LineOfSightHexInfo[] hexPath) => new()
    {
        From = new HexCoordinates(1, 1),
        To = new HexCoordinates(3, 1),
        AttackerLosLevel = 0,
        TargetLosLevel = 0,
        HasLineOfSight = true,
        HexPath = hexPath,
        TotalInterveningFactor = hexPath.Sum(h => h.InterveningFactor)
    };

    private static LineOfSightHexInfo CreateHexInfo(Hex hex, int interveningFactor = 1) => new()
    {
        Hex = hex,
        InterpolatedLosHeight = 0,
        InterveningFactor = interveningFactor
    };

    private static Hex CreateHex(HexCoordinates coordinates, params Terrain[] terrains)
    {
        var hex = new Hex(coordinates);
        foreach (var terrain in terrains)
            hex.AddTerrain(terrain);

        return hex;
    }

    [Fact]
    public void GetTerrainToHitModifiers_ReturnsEmpty_WhenHexPathIsEmpty()
    {
        // Arrange
        var sut = CreateLosResult();

        // Act
        var result = sut.GetTerrainToHitModifiers(_rules);

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public void GetTerrainToHitModifiers_ReturnsEmpty_WhenHexDoesNotIntervene()
    {
        // Arrange
        var hex = CreateHex(new HexCoordinates(2, 1), new LightWoodsTerrain());
        var sut = CreateLosResult(CreateHexInfo(hex, interveningFactor: 0));
        _rules.GetTerrainToHitModifier(MakaMekTerrains.LightWoods).Returns(1);

        // Act
        var result = sut.GetTerrainToHitModifiers(_rules);

        // Assert
        result.ShouldBeEmpty();
        _rules.DidNotReceive().GetTerrainToHitModifier(Arg.Any<MakaMekTerrains>());
    }

    [Fact]
    public void GetTerrainToHitModifiers_ReturnsModifier_WhenRulesProvideNonZeroValue()
    {
        // Arrange
        var hex = CreateHex(new HexCoordinates(2, 1), new LightWoodsTerrain());
        var sut = CreateLosResult(CreateHexInfo(hex));
        _rules.GetTerrainToHitModifier(MakaMekTerrains.LightWoods).Returns(1);

        // Act
        var result = sut.GetTerrainToHitModifiers(_rules);

        // Assert
        var modifier = result.ShouldHaveSingleItem();
        modifier.Value.ShouldBe(1);
        modifier.TerrainId.ShouldBe(MakaMekTerrains.LightWoods);
        modifier.Location.ShouldBe(new HexCoordinates(2, 1));
    }

    [Fact]
    public void GetTerrainToHitModifiers_SkipsTerrain_WhenRulesProvideZeroValue()
    {
        // Arrange
        var hex = CreateHex(new HexCoordinates(2, 1), new LightWoodsTerrain());
        var sut = CreateLosResult(CreateHexInfo(hex));
        _rules.GetTerrainToHitModifier(MakaMekTerrains.LightWoods).Returns(0);

        // Act
        var result = sut.GetTerrainToHitModifiers(_rules);

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public void GetTerrainToHitModifiers_IncludesNegativeModifierValue()
    {
        // Arrange
        var hex = CreateHex(new HexCoordinates(2, 1), new LightWoodsTerrain());
        var sut = CreateLosResult(CreateHexInfo(hex));
        _rules.GetTerrainToHitModifier(MakaMekTerrains.LightWoods).Returns(-1);

        // Act
        var result = sut.GetTerrainToHitModifiers(_rules);

        // Assert
        result.ShouldHaveSingleItem().Value.ShouldBe(-1);
    }

    [Fact]
    public void GetTerrainToHitModifiers_ReturnsModifierForEachTerrainInHex()
    {
        // Arrange
        var hex = CreateHex(new HexCoordinates(2, 1), new LightWoodsTerrain(), new HeavyWoodsTerrain());
        var sut = CreateLosResult(CreateHexInfo(hex));
        _rules.GetTerrainToHitModifier(MakaMekTerrains.LightWoods).Returns(1);
        _rules.GetTerrainToHitModifier(MakaMekTerrains.HeavyWoods).Returns(2);

        // Act
        var result = sut.GetTerrainToHitModifiers(_rules);

        // Assert
        result.Count.ShouldBe(2);
        result.ShouldContain(m => m.TerrainId == MakaMekTerrains.LightWoods && m.Value == 1);
        result.ShouldContain(m => m.TerrainId == MakaMekTerrains.HeavyWoods && m.Value == 2);
        result.ShouldAllBe(m => m.Location == new HexCoordinates(2, 1));
    }

    [Fact]
    public void GetTerrainToHitModifiers_ReturnsModifierForEachInterveningHex()
    {
        // Arrange
        var firstHex = CreateHex(new HexCoordinates(2, 1), new LightWoodsTerrain());
        var secondHex = CreateHex(new HexCoordinates(3, 1), new HeavyWoodsTerrain());
        var sut = CreateLosResult(CreateHexInfo(firstHex), CreateHexInfo(secondHex));
        _rules.GetTerrainToHitModifier(MakaMekTerrains.LightWoods).Returns(1);
        _rules.GetTerrainToHitModifier(MakaMekTerrains.HeavyWoods).Returns(2);

        // Act
        var result = sut.GetTerrainToHitModifiers(_rules);

        // Assert
        result.Count.ShouldBe(2);
        result.ShouldContain(m => m.TerrainId == MakaMekTerrains.LightWoods && m.Location == new HexCoordinates(2, 1));
        result.ShouldContain(m => m.TerrainId == MakaMekTerrains.HeavyWoods && m.Location == new HexCoordinates(3, 1));
    }

    [Fact]
    public void GetTerrainToHitModifiers_ReturnsEmpty_WhenInterveningHexHasNoTerrain()
    {
        // Arrange
        var hex = CreateHex(new HexCoordinates(2, 1));
        var sut = CreateLosResult(CreateHexInfo(hex));

        // Act
        var result = sut.GetTerrainToHitModifiers(_rules);

        // Assert
        result.ShouldBeEmpty();
        _rules.DidNotReceive().GetTerrainToHitModifier(Arg.Any<MakaMekTerrains>());
    }
}
