using System.Reactive.Linq;
using System.Reactive.Subjects;
using Sanet.MakaMek.Map.Data;
using Sanet.MakaMek.Map.Models.Terrains;

namespace Sanet.MakaMek.Map.Models;

/// <summary>
/// Represents a single hex on the game map
/// </summary>
public class Hex : IDisposable
{
    public HexCoordinates Coordinates { get; }
    public int Level { get; private set; }
    private readonly Dictionary<MakaMekTerrains, Terrain> _terrains = new();

    /// <summary>
    /// The biome identifier for this hex, inherited from the map when added via BattleMap.AddHex()
    /// </summary>
    public string Biome { get; internal set; } = string.Empty;

    public Hex(HexCoordinates coordinates, int level = 0)
    {
        Coordinates = coordinates;
        Level = level;
    }

    public void AddTerrain(Terrain terrain)
    {
        _terrains[terrain.Id] = terrain;
    }

    public void RemoveTerrain(MakaMekTerrains terrainId)
    {
        _terrains.Remove(terrainId);
    }
    
    public void ReplaceTerrains(List<Terrain> terrains)
    {
        _terrains.Clear();
        foreach (var terrain in terrains)
        {
            AddTerrain(terrain);
        }
    }

    public bool HasTerrain(MakaMekTerrains terrainId) => _terrains.ContainsKey(terrainId);

    public Terrain? GetTerrain(MakaMekTerrains terrainId) =>
        _terrains.GetValueOrDefault(terrainId);

    public IEnumerable<Terrain> GetTerrains() => _terrains.Values;

    public int GetCeiling()
    {
        var maxTerrainHeight = _terrains.Count != 0
            ? _terrains.Values.Max(t => t.Height) 
            : 0;
        return Level + maxTerrainHeight;
    }

    /// <summary>
    /// Gets the movement cost for entering this hex (highest terrain factor)
    /// </summary>
    public int MovementCost => _terrains.Count != 0 
        ? _terrains.Values.Max(t => t.MovementCost)
        : 1; // Default cost for empty hex

    private readonly Subject<bool> _isHighlightedSubject = new();
    private bool _disposed;

    /// <summary>
    /// Observable that emits when the highlight state changes
    /// </summary>
    public IObservable<bool> IsHighlightedChanged => _isHighlightedSubject.AsObservable();

    /// <summary>
    /// Gets or sets whether this hex is highlighted
    /// </summary>
    public bool IsHighlighted
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            if (_disposed) return;
            _isHighlightedSubject.OnNext(value);
        }
    }

    public MakaMekTerrains[] GetTerrainTypes()
    {
        return _terrains.Values.Select(t => t.Id).ToArray(); 
    }
    
    /// <summary>
    /// Gets the level difference between this hex and another hex
    /// </summary>
    /// <param name="hex">The other hex to compare with</param>
    /// <returns>The difference in levels (this.Level - hex.Level)</returns>
    public int GetLevelDifference(Hex hex)
    {
        return Level - hex.Level;
    }

    public HexData ToData()
    {
        return new HexData
        {
            Coordinates = Coordinates.ToData(),
            TerrainTypes = GetTerrainTypes(),
            Level = Level
        };
    }

    /// <summary>
    /// Disposes the Hex and completes the observable subject
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _isHighlightedSubject.OnCompleted();
        _isHighlightedSubject.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}