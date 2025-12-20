using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Units;

namespace Sanet.MakaMek.Bots.Models.DecisionEngines;

/// <summary>
/// Represents the tactical score for a candidate movement position with a specific movement type
/// </summary>
public readonly record struct PositionScore
{
    /// <summary>
    /// The position being evaluated
    /// </summary>
    public required HexPosition Position { get; init; }
    
    /// <summary>
    /// The movement type used to reach this position
    /// </summary>
    public required MovementType MovementType { get; init; }
    
    /// <summary>
    /// The number of hexes traveled to reach this position
    /// </summary>
    public required int HexesTraveled { get; init; }
    
    /// <summary>
    /// Defensive threat index - lower is better (less vulnerable to enemy fire)
    /// </summary>
    public required double DefensiveIndex { get; init; }
    
    /// <summary>
    /// Offensive potential index - higher is better (more damage potential)
    /// </summary>
    public required double OffensiveIndex { get; init; }
    
    /// <summary>
    /// Combined score using basic normalization.
    /// Higher is better overall (maximizes offense while minimizing defense).
    /// </summary>
    /// <returns>Combined tactical score</returns>
    public double GetCombinedScore()
    {
        // Normalize and combine (can be enhanced with strategy coefficients later)
        // Offensive is positive contribution, defensive is negative
        return OffensiveIndex - DefensiveIndex;
    }
}
