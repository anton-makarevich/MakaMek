using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Map.Generators;

/// <summary>
/// Shared utility for generating organic hex patches using BFS flood-fill.
/// Returns a dictionary mapping each hex in a patch to its distance from the patch center,
/// enabling callers to implement gradient effects (e.g., elevation tapering from center).
/// </summary>
public class PatchGenerator
{
    private readonly int _width;
    private readonly int _height;
    private readonly Random _random;

    public PatchGenerator(int width, int height, Random? random = null)
    {
        _width = width;
        _height = height;
        _random = random ?? new Random();
    }

    /// <summary>
    /// Generates hex patches via BFS flood-fill.
    /// </summary>
    /// <param name="coverage">Fraction of total hexes to cover (0.0–1.0).</param>
    /// <param name="minPatchSize">Minimum patch size; defaults to map-dimension-based value.</param>
    /// <param name="maxPatchSize">Maximum patch size; defaults to map-dimension-based value.</param>
    /// <returns>
    /// Dictionary mapping each hex coordinate in a patch to its distance from the nearest patch center.
    /// </returns>
    public Dictionary<HexCoordinates, int> GeneratePatches(
        double coverage,
        int? minPatchSize = null,
        int? maxPatchSize = null)
    {
        var result = new Dictionary<HexCoordinates, int>();
        var totalHexes = _width * _height;
        var targetHexes = (int)(totalHexes * coverage);

        if (targetHexes == 0)
            return result;

        minPatchSize ??= Math.Max(2, Math.Min(5, _width / 5));
        maxPatchSize ??= Math.Max(minPatchSize.Value + 2, Math.Min(9, _width / 3));

        var avgPatchSize = (minPatchSize.Value + maxPatchSize.Value) / 2;
        var patchCount = Math.Max(1, targetHexes / avgPatchSize);

        var safeMargin = Math.Min(2, Math.Min(_width, _height) / 10);

        for (var i = 0; i < patchCount; i++)
        {
            var center = new HexCoordinates(
                _random.Next(safeMargin, _width - safeMargin),
                _random.Next(safeMargin, _height - safeMargin));

            var patchSize = _random.Next(minPatchSize.Value, maxPatchSize.Value + 1);

            GeneratePatch(center, patchSize, result);
        }

        return result;
    }

    private void GeneratePatch(
        HexCoordinates center,
        int patchSize,
        Dictionary<HexCoordinates, int> result)
    {
        // Local tracking: patchHexes maps hex → distance from center for this patch
        var patchHexes = new Dictionary<HexCoordinates, int> { [center] = 0 };
        var walker = new PathWalker(_width, _height);

        walker.Flood(
            center,
            (current, candidate) =>
                TryAddToPatch(center, candidate, patchSize, patchHexes, patchHexes[current]),
            patchSize);

        MergeIntoResult(result, patchHexes);
    }

    /// <summary>
    /// Decides whether a candidate hex joins the patch: it must be new and pass the random
    /// probability that decreases with the distance from the patch center.
    /// </summary>
    private bool TryAddToPatch(
        HexCoordinates center,
        HexCoordinates candidate,
        int patchSize,
        Dictionary<HexCoordinates, int> patchHexes,
        int currentDistance)
    {
        if (patchHexes.ContainsKey(candidate)) return false;

        var distFromCenter = center.DistanceTo(candidate);
        var addProbability = 1.0 - (distFromCenter / (double)patchSize * 0.5);

        if (_random.NextDouble() >= addProbability) return false;

        patchHexes[candidate] = currentDistance + 1;
        return true;
    }

    /// <summary>
    /// Merges a patch into the overall result; use the minimum distance if a hex was already added.
    /// </summary>
    private static void MergeIntoResult(
        Dictionary<HexCoordinates, int> result,
        Dictionary<HexCoordinates, int> patchHexes)
    {
        foreach (var (hex, dist) in patchHexes)
        {
            if (!result.TryGetValue(hex, out var existing) || dist < existing)
                result[hex] = dist;
        }
    }
}

