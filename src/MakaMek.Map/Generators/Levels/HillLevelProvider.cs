using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Map.Generators.Levels;

/// <summary>
/// Generates organic hill clusters using BFS flood-fill (via <see cref="PatchGenerator"/>).
/// Hexes nearer to a patch center receive higher elevation; edges taper to level 1.
/// All levels are pre-computed in the constructor.
/// </summary>
public class HillLevelProvider : ILevelProvider
{
    private readonly Dictionary<HexCoordinates, int> _levels;

    /// <summary>
    /// Initialises the provider and pre-computes all hex levels.
    /// </summary>
    /// <param name="width">Map width (number of columns).</param>
    /// <param name="height">Map height (number of rows).</param>
    /// <param name="hillCoverage">Fraction of hexes to raise (0.0–1.0).</param>
    /// <param name="maxElevation">Highest possible level at a patch centre.</param>
    /// <param name="random">Optional <see cref="Random"/> for reproducible results.</param>
    public HillLevelProvider(
        int width,
        int height,
        double hillCoverage,
        int maxElevation,
        Random? random = null)
    {
        _levels = new Dictionary<HexCoordinates, int>();

        if (hillCoverage <= 0 || maxElevation <= 0)
            return;

        if (hillCoverage >= 1.0)
        {
            // Full coverage: every hex gets maxElevation
            for (var q = 1; q < width + 1; q++)
            for (var r = 1; r < height + 1; r++)
                _levels[new HexCoordinates(q, r)] = maxElevation;
            return;
        }

        var patchGen = new PatchGenerator(width, height, random);
        var patches = patchGen.GeneratePatches(hillCoverage);

        foreach (var (hex, distanceFromCenter) in patches)
        {
            // Elevation tapers from maxElevation at center (dist=0) down by 1 per step,
            // but never below 1 (the hex is in a hill cluster).
            var level = Math.Max(1, maxElevation - distanceFromCenter);
            _levels[hex] = level;
        }
    }

    /// <inheritdoc/>
    public int GetLevel(HexCoordinates coordinates)
    {
        return _levels.GetValueOrDefault(coordinates, 0);
    }
}

