using Sanet.MakaMek.Map.Data;
using Sanet.MakaMek.Map.Generators.Levels;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Models.Terrains;

namespace Sanet.MakaMek.Map.Generators;

/// <summary>
/// Fluent builder that composes terrain overlays and level generation into a single
/// <see cref="ITerrainGenerator"/>.  Call <see cref="Build"/> after configuring
/// the desired options.
/// </summary>
public class MapGeneratorBuilder
{
    private readonly int _width;
    private readonly int _height;
    private Terrain _baseTerrain = new ClearTerrain();
    private LevelConfiguration? _levelConfig;
    private int? _seed;

    // Each overlay entry: (patch-hex-set factory, terrain selector given (coords, rng))
    private readonly List<(
        Func<Random, Dictionary<HexCoordinates, int>> PatchFactory,
        Func<HexCoordinates, Random, Terrain> TerrainSelector)> _overlays = [];

    public MapGeneratorBuilder(int width, int height)
    {
        if (width < 1)
            throw new ArgumentOutOfRangeException(nameof(width), "Width must be at least 1.");
        if (height < 1)
            throw new ArgumentOutOfRangeException(nameof(height), "Height must be at least 1.");
        _width = width;
        _height = height;
    }

    /// <summary>Sets the default terrain for every hex (default: <see cref="ClearTerrain"/>).</summary>
    public MapGeneratorBuilder WithBaseTerrain(Terrain terrain)
    {
        _baseTerrain = terrain;
        return this;
    }

    /// <summary>Adds a terrain overlay patch covering the given fraction of the map.</summary>
    public MapGeneratorBuilder WithTerrain<TTerrain>(double coverage) where TTerrain : Terrain, new()
    {
        if (coverage is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(coverage), "Coverage must be between 0.0 and 1.0.");

        _overlays.Add((
            rng => new PatchGenerator(_width, _height, rng).GeneratePatches(coverage),
            (_, _) => new TTerrain()
        ));
        return this;
    }

    /// <summary>
    /// Convenience method for forest generation.
    /// Produces organic forest patches mixing light and heavy woods.
    /// </summary>
    public MapGeneratorBuilder WithForestPatches(double coverage, double lightWoodsProbability)
    {
        if (coverage is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(coverage), "Coverage must be between 0.0 and 1.0.");
        if (lightWoodsProbability is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(lightWoodsProbability),
                "Light woods probability must be between 0.0 and 1.0.");

        var lp = lightWoodsProbability;
        _overlays.Add((
            rng => new PatchGenerator(_width, _height, rng).GeneratePatches(coverage),
            (_, rng) => rng.NextDouble() < lp ? new LightWoodsTerrain() : new HeavyWoodsTerrain()
        ));
        return this;
    }

    /// <summary>Configures the hill level provider.</summary>
    public MapGeneratorBuilder WithHills(double coverage, int maxElevation)
    {
        _levelConfig = new LevelConfiguration(coverage, maxElevation, null);
        return this;
    }

    /// <summary>Sets a random seed for reproducible map generation.</summary>
    public MapGeneratorBuilder WithSeed(int seed)
    {
        _seed = seed;
        return this;
    }

    /// <summary>Builds and returns the configured <see cref="ITerrainGenerator"/>.</summary>
    public ITerrainGenerator Build()
    {
        var rngOffset = 0;
        Random CreateRng() => _seed.HasValue ? new Random(_seed.Value + rngOffset++) : new Random();

        // Materialize overlays: each gets its own Random so they don't interfere
        var builtOverlays = _overlays
            .Select(o => (new HashSet<HexCoordinates>(o.PatchFactory(CreateRng()).Keys), o.TerrainSelector))
            .ToList();

        ILevelProvider levelProvider = _levelConfig is not null
            ? new HillLevelProvider(_width, _height, _levelConfig.HillCoverage, _levelConfig.MaxElevation,
                _levelConfig.Seed.HasValue ? new Random(_levelConfig.Seed.Value) : CreateRng())
            : new FlatLevelProvider();

        return new CompositeGenerator(_width, _height, _baseTerrain, levelProvider, builtOverlays, CreateRng());
    }
}

