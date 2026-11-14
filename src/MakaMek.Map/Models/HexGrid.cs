using Sanet.MakaMek.Map.Exceptions;

namespace Sanet.MakaMek.Map.Models;

/// <summary>
/// Owns the hex storage of a battle map: dimensions, biome stamping and boundary checks.
/// <see cref="BattleMap"/> delegates its storage queries here so the traversal algorithms can
/// depend on the narrow <see cref="IHexGrid"/> instead of the whole map façade.
/// </summary>
internal sealed class HexGrid(int width, int height, string biome) : IHexGrid
{
    private readonly Dictionary<HexCoordinates, Hex> _hexes = new();

    public int Width { get; } = width;

    public int Height { get; } = height;

    public string Biome { get; } = biome;

    /// <summary>
    /// Adds a hex to the map. Throws <see cref="HexOutsideOfMapBoundariesException"/> if hex
    /// coordinates are outside map boundaries. Stamps the map's biome onto the hex.
    /// </summary>
    public void AddHex(Hex hex)
    {
        if (!IsOnMap(hex.Coordinates))
        {
            throw new HexOutsideOfMapBoundariesException(hex.Coordinates, Width, Height);
        }

        hex.Biome = Biome;
        _hexes[hex.Coordinates] = hex;
    }

    public Hex? GetHex(HexCoordinates coordinates)
    {
        return _hexes.GetValueOrDefault(coordinates);
    }

    public IEnumerable<Hex> GetHexes()
    {
        return _hexes.Values;
    }

    public bool IsOnMap(HexCoordinates coordinates)
    {
        return coordinates.Q >= 1 && coordinates.Q <= Width &&
               coordinates.R >= 1 && coordinates.R <= Height;
    }

    /// <summary>
    /// Returns the hex at <paramref name="changedCoord"/> and all of its neighbors that are on the map.
    /// </summary>
    public IEnumerable<HexCoordinates> GetAffectedCoordinates(HexCoordinates changedCoord)
    {
        if (IsOnMap(changedCoord))
            yield return changedCoord;

        foreach (var neighbor in changedCoord.GetAllNeighbours())
        {
            if (IsOnMap(neighbor))
                yield return neighbor;
        }
    }
}
