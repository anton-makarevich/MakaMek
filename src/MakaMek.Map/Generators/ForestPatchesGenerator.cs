using Sanet.MakaMek.Core.Utils;
using Sanet.MakaMek.Map.Exceptions;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Models.Terrains;

namespace Sanet.MakaMek.Map.Generators;

public class ForestPatchesGenerator : ITerrainGenerator
{
    private readonly int _width;
    private readonly int _height;
    private readonly Random _random;
    private readonly HashSet<HexCoordinates> _forestHexes;
    private readonly double _lightWoodsProbability;

    public ForestPatchesGenerator(
        int width,
        int height,
        double forestCoverage = 0.2,
        double lightWoodsProbability = 0.6,
        int? minPatchSize = null,
        int? maxPatchSize = null,
        Random? random = null)
    {
        _width = width;
        _height = height;
        _lightWoodsProbability = lightWoodsProbability;
        _random = random ?? new Random();
        _forestHexes = new HashSet<HexCoordinates>();

        // Calculate patch sizes based on map dimensions
        var totalHexes = width * height;
        var targetForestHexes = (int)(totalHexes * forestCoverage);

        if (targetForestHexes == 0)
        {
            // No need to generate patches if forest coverage is 0
            return;
        }
        
        minPatchSize ??= Math.Max(2, Math.Min(5, width / 5));
        maxPatchSize ??= Math.Max(minPatchSize.Value + 2, Math.Min(9, width / 3));
        
        // Calculate the number of patches
        var avgPatchSize = (minPatchSize.Value + maxPatchSize.Value) / 2;
        var forestPatchCount = Math.Max(1, targetForestHexes / avgPatchSize);

        if (forestCoverage >= 1.0)
        {
            // For full coverage, add all hexes to forest
            for (var q = 1; q < width+1; q++)
            {
                for (var r = 1; r < height+1; r++)
                {
                    _forestHexes.Add(new HexCoordinates(q, r));
                }
            }
            return;
        }

        GenerateForestPatches(forestPatchCount, minPatchSize.Value, maxPatchSize.Value);
    }

    private void GenerateForestPatches(int patchCount, int minSize, int maxSize)
    {
        var safeMargin = Math.Min(2, Math.Min(_width, _height) / 10);
                
        for (var i = 0; i < patchCount; i++)
        {
            var patchCenter = new HexCoordinates(
                _random.Next(safeMargin, _width - safeMargin),
                _random.Next(safeMargin, _height - safeMargin));
            
            var patchSize = _random.Next(minSize, maxSize + 1);

            // Add the center and its neighbors up to patch size
            var hexesToAdd = new Queue<HexCoordinates>();
            hexesToAdd.Enqueue(patchCenter);
            var patchHexes = new HashSet<HexCoordinates> { patchCenter };

            while (hexesToAdd.Count > 0 && patchHexes.Count < patchSize)
            {
                var current = hexesToAdd.Dequeue();
                foreach (var neighbor in current.GetAdjacentCoordinates())
                {
                    // Skip if outside map bounds
                    if (neighbor.Q < 1 || neighbor.Q >= _width+1 ||
                        neighbor.R < 1 || neighbor.R >= _height+1)
                        continue;

                    if (patchHexes.Count >= patchSize) break;
                    
                    // Add a neighbor with decreasing probability based on distance from the center
                    var distanceFromCenter = patchCenter.DistanceTo(neighbor);
                    var addProbability = 1.0 - (distanceFromCenter / (double)patchSize * 0.5);
                    
                    if (patchHexes.Add(neighbor) && _random.NextDouble() < addProbability)
                    {
                        hexesToAdd.Enqueue(neighbor);
                    }
                }
            }

            foreach (var hex in patchHexes)
            {
                _forestHexes.Add(hex);
            }
        }
    }

    public Hex Generate(HexCoordinates coordinates)
    {
        if (coordinates.Q < 1 || coordinates.Q >= _width+1 ||
            coordinates.R < 1 || coordinates.R >= _height+1)
        {
            throw new HexOutsideOfMapBoundariesException(coordinates, _width, _height);
        }

        return new Hex(coordinates).WithTerrain(
            _forestHexes.Contains(coordinates)
                ? (_random.NextDouble() < _lightWoodsProbability
                    ? new LightWoodsTerrain()
                    : new HeavyWoodsTerrain())
                : new ClearTerrain());
    }
}
