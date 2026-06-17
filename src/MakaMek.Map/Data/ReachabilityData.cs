using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Map.Data;

/// <summary>
/// Contains the reachable hexes for a unit with surface information
/// </summary>
public readonly record struct ReachabilityData(
    IReadOnlyList<(HexCoordinates coordinates, HexSurface surface)> ForwardReachableHexes,
    IReadOnlyList<(HexCoordinates coordinates, HexSurface surface)> BackwardReachableHexes)
{
    private readonly Lazy<IReadOnlyList<(HexCoordinates coordinates, HexSurface surface)>> _allReachableHexes = 
        new(() => ForwardReachableHexes.Union(BackwardReachableHexes).ToList());
    
    private readonly Lazy<HashSet<HexCoordinates>> _forwardReachableCoordinates = 
        new(() => ForwardReachableHexes.Select(x => x.coordinates).ToHashSet());
    
    private readonly Lazy<HashSet<HexCoordinates>> _backwardReachableCoordinates = 
        new(() => BackwardReachableHexes.Select(x => x.coordinates).ToHashSet());
    
    private readonly Lazy<HashSet<HexCoordinates>> _allReachableCoordinates = 
        new(() => ForwardReachableHexes.Select(x => x.coordinates)
            .Concat(BackwardReachableHexes.Select(x => x.coordinates))
            .ToHashSet());
    
    /// <summary>
    /// All reachable hexes (union of forward and backward)
    /// </summary>
    public IReadOnlyList<(HexCoordinates coordinates, HexSurface surface)> AllReachableHexes => _allReachableHexes.Value;
    
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
}