using Sanet.MakaMek.Core.Models.Map;

namespace Sanet.MakaMek.Core.Data.Map;

/// <summary>
/// Contains the reachable hexes for a unit based on movement type and available movement points
/// </summary>
public readonly record struct UnitReachabilityData(
    IReadOnlyList<HexCoordinates> ForwardReachableHexes,
    IReadOnlyList<HexCoordinates> BackwardReachableHexes)
{
    /// <summary>
    /// All reachable hexes (union of forward and backward)
    /// </summary>
    public IReadOnlyList<HexCoordinates> AllReachableHexes => 
        ForwardReachableHexes.Union(BackwardReachableHexes).ToList();
    
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