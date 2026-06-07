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
    private Dictionary<HexCoordinates, int>? _lakePatches;
    private Dictionary<HexCoordinates, int>? _riverPatches;
    private Dictionary<HexCoordinates, int>? _roadPatches;

    // Each overlay entry: (patch-hex-set factory with distance map, terrain selector given (coords, distance, rng), additive)
    private readonly List<(
        Func<Random, Dictionary<HexCoordinates, int>> PatchFactory,
        Func<HexCoordinates, int, Random, Terrain> TerrainSelector,
        bool Additive)> _overlays = [];

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
            (_, _, _) => new TTerrain(),
            false
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
            (_, _, rng) => rng.NextDouble() < lp ? new LightWoodsTerrain() : new HeavyWoodsTerrain(),
            false
        ));
        return this;
    }

    /// <summary>
    /// Convenience method for lake generation.
    /// Produces organic lake patches with depth increasing toward the center.
    /// </summary>
    /// <param name="coverage">Fraction of map hexes to cover (0.0–1.0).</param>
    /// <param name="maxDepth">Maximum depth at patch centers (1–3). Deeper water costs more MP.</param>
    public MapGeneratorBuilder WithLakes(double coverage, int maxDepth)
    {
        if (coverage is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(coverage), "Coverage must be between 0.0 and 1.0.");
        if (maxDepth < 1)
            throw new ArgumentOutOfRangeException(nameof(maxDepth), "MaxDepth must be at least 1.");

        _overlays.Add((
            rng =>
            {
                var patches = new PatchGenerator(_width, _height, rng).GeneratePatches(coverage);
                _lakePatches = patches;
                return patches;
            },
            (coords, distance, rng) =>
            {
                // Depth tapers from maxDepth at center (dist=0) toward 1 at patch edges.
                var depth = -Math.Max(1, maxDepth - distance);
                return new WaterTerrain(depth);
            },
            false
        ));
        return this;
    }

    /// <summary>
    /// Adds river generation. Rivers flow from the map edge inward with a
    /// directional probability of 50% straight / 25% clockwise / 25% counter-clockwise.
    /// Rivers terminate at the map edge, at a lake hex, or at another river.
    /// Must be called after <see cref="WithLakes"/> if both are used.
    /// </summary>
    /// <param name="riverCount">Number of rivers to generate.</param>
    public MapGeneratorBuilder WithRivers(int riverCount)
    {
        _overlays.Add((
            rng =>
            {
                var generator = new RiverPathGenerator(
                    _width, _height, rng,
                    _lakePatches?.Keys.ToHashSet());
                var rivers = generator.GenerateRivers(riverCount);
                _riverPatches = rivers;
                return rivers;
            },
            (_, _, _) => new WaterTerrain(-1),
            false
        ));
        return this;
    }

    /// <summary>
    /// Adds road generation. Roads grow as branching networks inward from a random
    /// map edge. Where a road hex coincides with water (a lake or river hex produced
    /// by an earlier overlay) a <see cref="BridgeTerrain"/> is placed instead of a
    /// plain <see cref="RoadTerrain"/>, modelling a bridge spanning the water.
    /// Must be called after <see cref="WithLakes"/> and <see cref="WithRivers"/> if
    /// bridges over those water bodies are desired, so the last-overlay-wins pattern
    /// lets roads/bridges override the underlying water.
    /// </summary>
    /// <param name="roadCount">Number of roads to generate.</param>
    public MapGeneratorBuilder WithRoads(int roadCount)
    {
        if (roadCount < 0)
            throw new ArgumentOutOfRangeException(nameof(roadCount), "Road count must be non-negative.");

        // Combined water set is captured when the patch factory runs during Build().
        // By then any earlier lake/river factories have already populated their patches.
        HashSet<HexCoordinates> waterHexes = [];
        _overlays.Add((
            rng =>
            {
                waterHexes = (_lakePatches?.Keys ?? Enumerable.Empty<HexCoordinates>())
                    .Concat(_riverPatches?.Keys ?? Enumerable.Empty<HexCoordinates>())
                    .ToHashSet();
                var generator = new RoadPathGenerator(_width, _height, rng);
                var roads = generator.GenerateRoads(roadCount);
                _roadPatches = roads;
                return roads;
            },
            (coords, _, _) => waterHexes.Contains(coords)
                ? new BridgeTerrain(1, 40)
                : new RoadTerrain(),
            true
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
            .Select(o => (new Dictionary<HexCoordinates, int>(o.PatchFactory(CreateRng())), o.TerrainSelector, o.Additive))
            .ToList();

        ILevelProvider levelProvider = _levelConfig is not null
            ? new HillLevelProvider(_width, _height, _levelConfig.HillCoverage, _levelConfig.MaxElevation,
                _levelConfig.Seed.HasValue ? new Random(_levelConfig.Seed.Value) : CreateRng())
            : new FlatLevelProvider();

        return new CompositeGenerator(_width, _height, _baseTerrain, levelProvider, builtOverlays, CreateRng());
    }
}

