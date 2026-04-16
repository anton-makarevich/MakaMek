using System.Reactive.Linq;
using System.Reactive.Subjects;
using Sanet.MakaMek.Map.Data;
using Sanet.MakaMek.Map.Models.Highlights;
using Sanet.MakaMek.Map.Models.Terrains;

namespace Sanet.MakaMek.Map.Models;

/// <summary>
/// Represents a single hex on the game map
/// </summary>
public class Hex : IDisposable
{
    public HexCoordinates Coordinates { get; }
    public int Level { get; internal set; }
    private readonly Dictionary<MakaMekTerrains, Terrain> _terrains = new();
    private readonly HashSet<IHexHighlightType> _highlights = [];
    private readonly Subject<IReadOnlyCollection<IHexHighlightType>> _highlightsSubject = new();
    private bool _disposed;

    private IReadOnlyCollection<IHexHighlightType> HighlightsSnapshot  => _highlights.ToArray();

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

    /// <summary>
    /// Observable that emits when the highlight collection changes
    /// </summary>
    public IObservable<IReadOnlyCollection<IHexHighlightType>> HighlightsChanged => _highlightsSubject.AsObservable();

    /// <summary>
    /// Gets the current highlights on this hex
    /// </summary>
    public IReadOnlyCollection<IHexHighlightType> Highlights => HighlightsSnapshot ;

    /// <summary>
    /// Adds a highlight to this hex if not already present
    /// </summary>
    /// <param name="highlight">The highlight type to add</param>
    public void AddHighlight(IHexHighlightType highlight)
    {
        if (_disposed) return;
        if (_highlights.Add(highlight))
        {
            _highlightsSubject.OnNext(HighlightsSnapshot );
        }
    }

    /// <summary>
    /// Removes any highlight of the specified type from this hex
    /// </summary>
    /// <typeparam name="T">The type of highlight to remove</typeparam>
    public void RemoveHighlight<T>() where T : IHexHighlightType
    {
        if (_disposed) return;
        var removed = _highlights.RemoveWhere(h => h is T);
        if (removed > 0)
        {
            _highlightsSubject.OnNext(HighlightsSnapshot );
        }
    }

    /// <summary>
    /// Checks if this hex has a specific highlight type active
    /// </summary>
    /// <typeparam name="T">The type of highlight to check for</typeparam>
    /// <returns>True if the highlight type is active</returns>
    public bool HasHighlight<T>() where T : IHexHighlightType
    {
        return _highlights.OfType<T>().Any();
    }

    /// <summary>
    /// Clears all highlights from this hex
    /// </summary>
    public void ClearHighlights()
    {
        if (_disposed) return;
        if (_highlights.Count <= 0) return;
        _highlights.Clear();
        _highlightsSubject.OnNext(HighlightsSnapshot );
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
            Terrains = GetTerrains().Select(t => t.ToData()).ToArray(),
            Level = Level
        };
    }

    /// <summary>
    /// Disposes the Hex and completes the observable subject
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _highlightsSubject.OnCompleted();
        _highlightsSubject.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}