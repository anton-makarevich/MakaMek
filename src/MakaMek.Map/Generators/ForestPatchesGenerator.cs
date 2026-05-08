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

        if (forestCoverage >= 1.0)
        {
            // For full coverage, add all hexes to forest
            _forestHexes = [];
            for (var q = 1; q < width + 1; q++)
                for (var r = 1; r < height + 1; r++)
                    _forestHexes.Add(new HexCoordinates(q, r));
            return;
        }

        var patchGen = new PatchGenerator(width, height, _random);
        var patches = patchGen.GeneratePatches(forestCoverage, minPatchSize, maxPatchSize);
        _forestHexes = new HashSet<HexCoordinates>(patches.Keys);
    }

    public Hex Generate(HexCoordinates coordinates)
    {
        if (coordinates.Q < 1 || coordinates.Q >= _width + 1 ||
            coordinates.R < 1 || coordinates.R >= _height + 1)
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
