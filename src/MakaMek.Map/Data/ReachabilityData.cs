using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Map.Data;

/// <summary>
/// Contains the reachable hexes for a unit
/// </summary>
public readonly record struct ReachabilityData(
    IReadOnlyList<HexCoordinates> ForwardReachableHexes,
    IReadOnlyList<HexCoordinates> BackwardReachableHexes)
{
    private readonly Lazy<IReadOnlyList<HexCoordinates>> _allReachableHexes = 
        new(() => ForwardReachableHexes.Union(BackwardReachableHexes).ToList());
    
    /// <summary>
    /// All reachable hexes (union of forward and backward)
    /// </summary>
    public IReadOnlyList<HexCoordinates> AllReachableHexes => _allReachableHexes.Value;
    
    /// <summary>
    /// Checks if a hex is reachable (either forward or backward)
    /// </summary>
    public bool IsHexReachable(HexCoordinates hex) =>
        ForwardReachableHexes.Contains(hex) || BackwardReachableHexes.Contains(hex);
    
    /// <summary>
    /// Checks if a hex is reachable by forward movement
    /// </summary>
    public bool IsForwardReachable(HexCoordinates hex) =>
        ForwardReachableHexes.Contains(hex);
    
    /// <summary>
    /// Checks if a hex is reachable by backward movement
    /// </summary>
    public bool IsBackwardReachable(HexCoordinates hex) =>
        BackwardReachableHexes.Contains(hex);
}