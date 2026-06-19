using Sanet.MakaMek.Map.Data;

namespace Sanet.MakaMek.Map.Models;

/// <summary>
/// Contains the reachable hexes for a unit with surface and cost information
/// </summary>
public readonly record struct ReachableArea(
    IReadOnlyList<HexReachabilityData> ForwardReachableHexes,
    IReadOnlyList<HexReachabilityData> BackwardReachableHexes)
{
    private readonly Lazy<IReadOnlyList<HexReachabilityData>> _allReachableHexes = 
        new(() => ForwardReachableHexes.Union(BackwardReachableHexes).ToList());
    
    private readonly Lazy<HashSet<HexCoordinates>> _forwardReachableCoordinates = 
        new(() => ForwardReachableHexes.Select(x => x.Coordinates).ToHashSet());
    
    private readonly Lazy<HashSet<HexCoordinates>> _backwardReachableCoordinates = 
        new(() => BackwardReachableHexes.Select(x => x.Coordinates).ToHashSet());
    
    private readonly Lazy<HashSet<HexCoordinates>> _allReachableCoordinates = 
        new(() => ForwardReachableHexes.Select(x => x.Coordinates)
            .Concat(BackwardReachableHexes.Select(x => x.Coordinates))
            .ToHashSet());
    
    /// <summary>
    /// All reachable hexes (union of forward and backward)
    /// </summary>
    public IReadOnlyList<HexReachabilityData> AllReachableHexes => _allReachableHexes.Value;
    
    /// <summary>
    /// Unique reachable coordinates (deduplicated)
    /// </summary>
    public IReadOnlySet<HexCoordinates> AllReachableCoordinates => _allReachableCoordinates.Value;
    
    /// <summary>
    /// Checks if a hex is reachable (either forward or backward) on any surface
    /// </summary>
    public bool IsHexReachable(HexCoordinates hex) => _allReachableCoordinates.Value.Contains(hex);
    
    /// <summary>
    /// Checks if a hex is reachable by forward movement on any surface
    /// </summary>
    public bool IsForwardReachable(HexCoordinates hex) => _forwardReachableCoordinates.Value.Contains(hex);
    
    /// <summary>
    /// Checks if a hex is reachable by backward movement on any surface
    /// </summary>
    public bool IsBackwardReachable(HexCoordinates hex) => _backwardReachableCoordinates.Value.Contains(hex);
    
    /// <summary>
    /// Gets all reachable surface entries for a given coordinate
    /// </summary>
    public IEnumerable<HexReachabilityData> GetReachableSurfacesForCoordinate(HexCoordinates coords)
    {
        return ForwardReachableHexes.Concat(BackwardReachableHexes)
            .Where(x => x.Coordinates == coords);
    }
}