using Sanet.MakaMek.Core.Utils;
using Sanet.MakaMek.Map.Exceptions;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Models.Terrains;

namespace Sanet.MakaMek.Map.Generators;

/// <summary>
/// Internal <see cref="ITerrainGenerator"/> that applies a base terrain, zero or more ordered
/// terrain overlays, and a level provider for every generated hex.
/// Created by <see cref="MapGeneratorBuilder.Build"/>.
/// </summary>
internal class CompositeGenerator : ITerrainGenerator
{
    private readonly int _width;
    private readonly int _height;
    private readonly Terrain _baseTerrain;
    private readonly ILevelProvider _levelProvider;
    private readonly List<(HashSet<HexCoordinates> Hexes, Func<HexCoordinates, Random, Terrain> Selector)> _overlays;
    private readonly Random _random;

    internal CompositeGenerator(
        int width,
        int height,
        Terrain baseTerrain,
        ILevelProvider levelProvider,
        List<(HashSet<HexCoordinates>, Func<HexCoordinates, Random, Terrain>)> overlays,
        Random random)
    {
        _width = width;
        _height = height;
        _baseTerrain = baseTerrain;
        _levelProvider = levelProvider;
        _overlays = overlays;
        _random = random;
    }

    public Hex Generate(HexCoordinates coordinates)
    {
        if (coordinates.Q < 1 || coordinates.Q >= _width + 1 ||
            coordinates.R < 1 || coordinates.R >= _height + 1)
        {
            throw new HexOutsideOfMapBoundariesException(coordinates, _width, _height);
        }

        // Start with the base terrain; last matching overlay wins
        var terrain = _baseTerrain;
        foreach (var (hexes, selector) in _overlays)
        {
            if (hexes.Contains(coordinates))
                terrain = selector(coordinates, _random);
        }

        var level = _levelProvider.GetLevel(coordinates);

        return new Hex(coordinates)
            .WithTerrain(terrain)
            .WithLevel(level);
    }
}

