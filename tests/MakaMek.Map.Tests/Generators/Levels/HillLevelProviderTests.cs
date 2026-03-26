using Sanet.MakaMek.Map.Generators.Levels;
using Sanet.MakaMek.Map.Models;
using Shouldly;

namespace Sanet.MakaMek.Map.Tests.Generators.Levels;

public class HillLevelProviderTests
{
    private const int Width = 10;
    private const int Height = 10;

    [Fact]
    public void ZeroCoverage_ProducesAllFlatLevels()
    {
        var sut = new HillLevelProvider(Width, Height, hillCoverage: 0, maxElevation: 3);

        for (var q = 1; q <= Width; q++)
        for (var r = 1; r <= Height; r++)
            sut.GetLevel(new HexCoordinates(q, r)).ShouldBe(0);
    }

    [Fact]
    public void FullCoverage_ProducesNonZeroLevelsEverywhere()
    {
        var sut = new HillLevelProvider(Width, Height, hillCoverage: 1.0, maxElevation: 3, random: new Random(42));

        var allNonZero = true;
        for (var q = 1; q <= Width; q++)
        for (var r = 1; r <= Height; r++)
        {
            if (sut.GetLevel(new HexCoordinates(q, r)) == 0)
            {
                allNonZero = false;
                break;
            }
        }

        allNonZero.ShouldBeTrue();
    }

    [Fact]
    public void LevelNeverExceedsMaxElevation()
    {
        const int maxElevation = 3;
        var sut = new HillLevelProvider(Width, Height, hillCoverage: 0.5, maxElevation: maxElevation, random: new Random(42));

        for (var q = 1; q <= Width; q++)
        for (var r = 1; r <= Height; r++)
            sut.GetLevel(new HexCoordinates(q, r)).ShouldBeLessThanOrEqualTo(maxElevation);
    }

    [Fact]
    public void AdjacentHexesFormClusters()
    {
        // Generate with decent coverage, so we're likely to get hill patches
        var sut = new HillLevelProvider(Width, Height, hillCoverage: 0.4, maxElevation: 3, random: new Random(42));

        var hillHexes = new List<HexCoordinates>();
        for (var q = 1; q <= Width; q++)
        for (var r = 1; r <= Height; r++)
        {
            var coords = new HexCoordinates(q, r);
            if (sut.GetLevel(coords) > 0)
                hillHexes.Add(coords);
        }

        hillHexes.Count.ShouldBeGreaterThan(0);

        // Verify that at least some hill hexes have hill neighbors (cluster property)
        var clusteredCount = hillHexes.Count(hex =>
            hex.GetAllNeighbours().Any(n => hillHexes.Contains(n)));

        clusteredCount.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void SameSeed_ProducesIdenticalResults()
    {
        const int seed = 12345;
        var provider1 = new HillLevelProvider(Width, Height, hillCoverage: 0.3, maxElevation: 3, random: new Random(seed));
        var provider2 = new HillLevelProvider(Width, Height, hillCoverage: 0.3, maxElevation: 3, random: new Random(seed));

        for (var q = 1; q <= Width; q++)
        for (var r = 1; r <= Height; r++)
        {
            var coords = new HexCoordinates(q, r);
            provider1.GetLevel(coords).ShouldBe(provider2.GetLevel(coords));
        }
    }
}

